using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Caching;

namespace ProductsMicroservice.Tests;

public class ProductsAdderCachingDecoratorTests
{
    private readonly Mock<IProductsAdderService> _innerMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<ProductsAdderCachingDecorator>> _loggerMock = new();
    private readonly ProductsAdderCachingDecorator _decorator;

    public ProductsAdderCachingDecoratorTests()
    {
        _decorator = new ProductsAdderCachingDecorator(
            _innerMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task AddProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = () => _decorator.AddProductAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddProductAsync_ShouldInvalidateAllProductsCache_WhenSuccessful()
    {
        var request = new ProductAddRequest();
        var response = new ProductResponse(Guid.NewGuid(), "Product", 10, 1);
        _innerMock.Setup(x => x.AddProductAsync(request)).ReturnsAsync(response);

        var result = await _decorator.AddProductAsync(request);

        result.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddProductAsync_ShouldNotInvalidateCache_WhenInnerReturnsNull()
    {
        var request = new ProductAddRequest();
        _innerMock.Setup(x => x.AddProductAsync(request)).ReturnsAsync((ProductResponse?)null);

        var result = await _decorator.AddProductAsync(request);

        result.Should().BeNull();
        _cacheMock.Verify(x => x.RemoveAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddProductAsync_ShouldReturnResponse_WhenCacheInvalidationFails()
    {
        var request = new ProductAddRequest();
        var response = new ProductResponse();
        _innerMock.Setup(x => x.AddProductAsync(request)).ReturnsAsync(response);
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.AllProductsKey,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var result = await _decorator.AddProductAsync(request);

        result.Should().BeSameAs(response);
    }
}
