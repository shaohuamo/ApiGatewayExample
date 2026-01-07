using System.Linq.Expressions;
using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Services;

namespace ProductsUnitTests;

public class ProductsGetterServiceTests
{
    private readonly Mock<IProductsRepository> _productsRepositoryMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IDistributedCache> _distributedCacheMock;

    private readonly ProductsGetterService _service;

    public ProductsGetterServiceTests()
    {
        _productsRepositoryMock = new Mock<IProductsRepository>();
        _mapperMock = new Mock<IMapper>();
        _distributedCacheMock = new Mock<IDistributedCache>();

        _service = new ProductsGetterService(
            _productsRepositoryMock.Object,
            _mapperMock.Object,
            _distributedCacheMock.Object
        );
    }

    [Fact]
    public async Task GetProducts_CacheHit_ReturnsProductsFromCache()
    {
        // Arrange
        var cachedProducts = new List<ProductResponse?>
        {
            new ProductResponse
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Cached Product"
            }
        };

        byte[] cachedBytes = System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(cachedProducts)
        );

        _distributedCacheMock
            .Setup(c => c.GetAsync("all-products", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedBytes);

        // Act
        var result = await _service.GetProducts();

        // Assert
        Assert.Single(result);
        Assert.Equal("Cached Product", result.First()!.ProductName);

        _productsRepositoryMock.Verify(
            r => r.GetProducts(),
            Times.Never
        );
    }


    [Fact]
    public async Task GetProducts_CacheMiss_FetchesFromRepositoryAndStoresInCache()
    {
        // Arrange
        _distributedCacheMock
            .Setup(c => c.GetAsync("all-products", It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var productsFromDb = new List<Product?>
        {
            new Product
            {
                ProductId = Guid.NewGuid(),
                ProductName = "DB Product"
            }
        };

        var mappedResponses = new List<ProductResponse?>
        {
            new ProductResponse
            {
                ProductId = productsFromDb[0]!.ProductId,
                ProductName = "DB Product"
            }
        };

        _productsRepositoryMock
            .Setup(r => r.GetProducts())
            .ReturnsAsync(productsFromDb);

        _mapperMock
            .Setup(m => m.Map<IEnumerable<ProductResponse>>(productsFromDb))
            .Returns(mappedResponses);

        // Act
        var result = await _service.GetProducts();

        // Assert
        Assert.Single(result);
        Assert.Equal("DB Product", result.First()!.ProductName);

        _distributedCacheMock.Verify(
            c => c.SetAsync(
                "all-products",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }


    [Fact]
    public async Task GetProductByCondition_ProductExists_ReturnsMappedResponse()
    {
        // Arrange
        Expression<Func<Product, bool>> condition = p => p.ProductName == "Test";

        var product = new Product
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Test"
        };

        var response = new ProductResponse
        {
            ProductId = product.ProductId,
            ProductName = product.ProductName
        };

        _productsRepositoryMock
            .Setup(r => r.GetProductByCondition(condition))
            .ReturnsAsync(product);

        _mapperMock
            .Setup(m => m.Map<ProductResponse>(product))
            .Returns(response);

        // Act
        var result = await _service.GetProductByCondition(condition);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test", result!.ProductName);
    }

    [Fact]
    public async Task GetProductByCondition_ProductDoesNotExist_ReturnsNull()
    {
        // Arrange
        Expression<Func<Product, bool>> condition = p => p.ProductName == "Missing";

        _productsRepositoryMock
            .Setup(r => r.GetProductByCondition(condition))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _service.GetProductByCondition(condition);

        // Assert
        Assert.Null(result);

        _mapperMock.Verify(
            m => m.Map<ProductResponse>(It.IsAny<Product>()),
            Times.Never
        );
    }
}