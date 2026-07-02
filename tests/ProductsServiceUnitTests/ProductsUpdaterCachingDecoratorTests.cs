using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Caching;
using ProductsMicroservice.Infrastructure.Options;

namespace ProductsMicroservice.Tests;

public class ProductsUpdaterCachingDecoratorTests
{
    private readonly Mock<IProductsUpdaterService> _innerMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<ProductsUpdaterCachingDecorator>> _loggerMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();

    private ProductsUpdaterCachingDecorator CreateDecorator(int delayedDeleteMs = 0)
    {
        return new ProductsUpdaterCachingDecorator(
            _innerMock.Object,
            _cacheMock.Object,
            Options.Create(new RedisOptions { DelayedDeleteMs = delayedDeleteMs }),
            _loggerMock.Object,
            _scopeFactoryMock.Object);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = () => CreateDecorator().UpdateProductAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldRemoveCachesBeforeCallingInnerService()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var detailKey = ProductCacheKeys.GetDetailsKey(request.ProductId);
        var detailRemoved = false;
        var listRemoved = false;
        _cacheMock.Setup(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()))
            .Callback(() => detailRemoved = true)
            .Returns(Task.CompletedTask);
        _cacheMock.Setup(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()))
            .Callback(() => listRemoved = true)
            .Returns(Task.CompletedTask);
        _innerMock.Setup(x => x.UpdateProductAsync(request))
            .Callback(() =>
            {
                detailRemoved.Should().BeTrue();
                listRemoved.Should().BeTrue();
            })
            .ReturnsAsync((ProductResponse?)null);

        var result = await CreateDecorator().UpdateProductAsync(request);

        result.Should().BeNull();
        _cacheMock.Verify(x => x.RemoveAsync(detailKey, It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(ProductCacheKeys.AllProductsKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldContinue_WhenPreUpdateCacheRemovalFails()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.GetDetailsKey(request.ProductId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ReturnsAsync((ProductResponse?)null);

        var result = await CreateDecorator().UpdateProductAsync(request);

        result.Should().BeNull();
        _innerMock.Verify(x => x.UpdateProductAsync(request), Times.Once);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldDoubleDeleteAndScheduleDelayedDelete_WhenSuccessful()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid() };
        var response = new ProductResponse(request.ProductId, "Updated", 20, 3);
        var delayedDeleteCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scopedCacheMock = new Mock<IDistributedCache>();
        scopedCacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.AllProductsKey,
                It.IsAny<CancellationToken>()))
            .Callback(() => delayedDeleteCompleted.TrySetResult())
            .Returns(Task.CompletedTask);
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IDistributedCache))).Returns(scopedCacheMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ILogger<ProductsUpdaterCachingDecorator>)))
            .Returns(_loggerMock.Object);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.SetupGet(x => x.ServiceProvider).Returns(serviceProviderMock.Object);
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(scopeMock.Object);
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ReturnsAsync(response);

        var result = await CreateDecorator().UpdateProductAsync(request);
        await delayedDeleteCompleted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        result.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        scopedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(request.ProductId),
            It.IsAny<CancellationToken>()), Times.Once);
        scopedCacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
