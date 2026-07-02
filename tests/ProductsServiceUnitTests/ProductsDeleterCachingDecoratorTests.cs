using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.CacheKeys;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Caching;

namespace ProductsMicroservice.Tests;

public class ProductsDeleterCachingDecoratorTests
{
    private readonly Mock<IProductsDeleterService> _innerMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<ProductsDeleterCachingDecorator>> _loggerMock = new();
    private readonly ProductsDeleterCachingDecorator _decorator;

    public ProductsDeleterCachingDecoratorTests()
    {
        _decorator = new ProductsDeleterCachingDecorator(
            _innerMock.Object,
            _cacheMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldThrow_WhenProductIdIsEmpty()
    {
        Func<Task> act = () => _decorator.DeleteProductAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldInvalidateDetailAndListCaches_WhenSuccessful()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId)).ReturnsAsync(true);

        var result = await _decorator.DeleteProductAsync(productId);

        result.Should().BeTrue();
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.GetDetailsKey(productId),
            It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.RemoveAsync(
            ProductCacheKeys.AllProductsKey,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldNotInvalidateCache_WhenInnerReturnsFalse()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId)).ReturnsAsync(false);

        var result = await _decorator.DeleteProductAsync(productId);

        result.Should().BeFalse();
        _cacheMock.Verify(x => x.RemoveAsync(
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldReturnTrue_WhenCacheInvalidationFails()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId)).ReturnsAsync(true);
        _cacheMock.Setup(x => x.RemoveAsync(
                ProductCacheKeys.GetDetailsKey(productId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache unavailable"));

        var result = await _decorator.DeleteProductAsync(productId);

        result.Should().BeTrue();
    }
}
