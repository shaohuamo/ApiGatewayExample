using System.Text;
using System.Text.Json;
using FluentAssertions;
using Medallion.Threading;
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

public class ProductsGetterCachingDecoratorTests
{
    private readonly Mock<IProductsGetterService> _innerMock = new();
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<IDistributedLockProvider> _lockProviderMock = new();
    private readonly Mock<ILogger<ProductsGetterCachingDecorator>> _loggerMock = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly CacheOptions _options = new()
    {
        DefaultExpirationMinutes = 30,
        NegativeCacheExpirationMinutes = 2,
        NullValuePlaceholder = "missing"
    };

    private ProductsGetterCachingDecorator CreateDecorator()
    {
        return new ProductsGetterCachingDecorator(
            _innerMock.Object,
            _cacheMock.Object,
            _lockProviderMock.Object,
            Options.Create(_options),
            _loggerMock.Object,
            _scopeFactoryMock.Object);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnCachedProduct_WithoutCallingInnerService()
    {
        var productId = Guid.NewGuid();
        var cached = new ProductResponse(productId, "Cached", 12, 3);
        SetupCachedString(ProductCacheKeys.GetDetailsKey(productId), JsonSerializer.Serialize(cached));

        var result = await CreateDecorator().GetProductByProductIdAsync(productId);

        result.Should().BeEquivalentTo(cached);
        _innerMock.Verify(x => x.GetProductByProductIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnNull_ForNegativeCacheHit()
    {
        var productId = Guid.NewGuid();
        SetupCachedString(ProductCacheKeys.GetDetailsKey(productId), _options.NullValuePlaceholder);

        var result = await CreateDecorator().GetProductByProductIdAsync(productId);

        result.Should().BeNull();
        _innerMock.Verify(x => x.GetProductByProductIdAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldCacheAndReturnProduct_OnCacheMiss()
    {
        var productId = Guid.NewGuid();
        var response = new ProductResponse(productId, "Database", 20, 4);
        SetupCachedString(ProductCacheKeys.GetDetailsKey(productId), null);
        _innerMock.Setup(x => x.GetProductByProductIdAsync(productId)).ReturnsAsync(response);

        var result = await CreateDecorator().GetProductByProductIdAsync(productId);

        result.Should().BeSameAs(response);
        _cacheMock.Verify(x => x.SetAsync(
            ProductCacheKeys.GetDetailsKey(productId),
            It.Is<byte[]>(value => Encoding.UTF8.GetString(value).Contains("Database")),
            It.Is<DistributedCacheEntryOptions>(options =>
                options.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(_options.DefaultExpirationMinutes)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldSetNegativeCache_WhenProductIsMissing()
    {
        var productId = Guid.NewGuid();
        SetupCachedString(ProductCacheKeys.GetDetailsKey(productId), null);
        _innerMock.Setup(x => x.GetProductByProductIdAsync(productId))
            .ReturnsAsync((ProductResponse?)null);

        var result = await CreateDecorator().GetProductByProductIdAsync(productId);

        result.Should().BeNull();
        _cacheMock.Verify(x => x.SetAsync(
            ProductCacheKeys.GetDetailsKey(productId),
            It.Is<byte[]>(value => Encoding.UTF8.GetString(value) == _options.NullValuePlaceholder),
            It.Is<DistributedCacheEntryOptions>(options =>
                options.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(_options.NegativeCacheExpirationMinutes)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldReturnFreshCachedProducts_WithoutCallingInnerService()
    {
        List<ProductResponse> products = [new ProductResponse(Guid.NewGuid(), "Cached", 15, 2)];
        var wrapper = new RedisDataWrapper<List<ProductResponse>>
        {
            Data = products,
            LogicExpireTime = DateTime.Now.AddMinutes(5)
        };
        SetupCachedString(ProductCacheKeys.AllProductsKey, JsonSerializer.Serialize(wrapper));

        var result = await CreateDecorator().GetProductsAsync();

        result.Should().BeEquivalentTo(products);
        _innerMock.Verify(x => x.GetProductsAsync(), Times.Never);
    }

    private void SetupCachedString(string key, string? value)
    {
        _cacheMock.Setup(x => x.GetAsync(key, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value == null ? null : Encoding.UTF8.GetBytes(value));
    }
}
