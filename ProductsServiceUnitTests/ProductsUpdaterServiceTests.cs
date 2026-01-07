using AutoMapper;
using Moq;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.Services;

namespace ProductsUnitTests;

public class ProductsUpdaterServiceTests
{
    private readonly Mock<IProductsRepository> _productsRepositoryMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly ProductsUpdaterService _service;

    public ProductsUpdaterServiceTests()
    {
        _productsRepositoryMock = new Mock<IProductsRepository>();
        _mapperMock = new Mock<IMapper>();

        _service = new ProductsUpdaterService(
            _productsRepositoryMock.Object,
            _mapperMock.Object
        );
    }

    [Fact]
    public async Task UpdateProduct_ValidRequest_ReturnsUpdatedProductResponse()
    {
        // Arrange
        var updateRequest = new ProductUpdateRequest
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Updated Product",
            UnitPrice = 200,
            QuantityInStock = 25
        };

        var mappedProduct = new Product
        {
            ProductId = updateRequest.ProductId,
            ProductName = updateRequest.ProductName,
            UnitPrice = updateRequest.UnitPrice,
            QuantityInStock = updateRequest.QuantityInStock
        };

        var updatedProduct = new Product
        {
            ProductId = updateRequest.ProductId,
            ProductName = "Updated Product",
            UnitPrice = 200,
            QuantityInStock = 25
        };

        var expectedResponse = new ProductResponse
        {
            ProductId = updatedProduct.ProductId,
            ProductName = updatedProduct.ProductName,
            UnitPrice = updatedProduct.UnitPrice,
            QuantityInStock = updatedProduct.QuantityInStock
        };

        _mapperMock
            .Setup(m => m.Map<Product>(updateRequest))
            .Returns(mappedProduct);

        _productsRepositoryMock
            .Setup(r => r.UpdateProduct(mappedProduct))
            .ReturnsAsync(updatedProduct);

        _mapperMock
            .Setup(m => m.Map<ProductResponse>(updatedProduct))
            .Returns(expectedResponse);

        // Act
        var result = await _service.UpdateProduct(updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse.ProductId, result!.ProductId);
        Assert.Equal(expectedResponse.ProductName, result.ProductName);

        _productsRepositoryMock.Verify(
            r => r.UpdateProduct(mappedProduct),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateProduct_ProductNotFound_ReturnsNull()
    {
        // Arrange
        var updateRequest = new ProductUpdateRequest
        {
            ProductId = Guid.NewGuid()
        };

        var mappedProduct = new Product
        {
            ProductId = updateRequest.ProductId
        };

        _mapperMock
            .Setup(m => m.Map<Product>(updateRequest))
            .Returns(mappedProduct);

        _productsRepositoryMock
            .Setup(r => r.UpdateProduct(mappedProduct))
            .ReturnsAsync((Product?)null);

        _mapperMock
            .Setup(m => m.Map<ProductResponse>(null))
            .Returns((ProductResponse?)null);

        // Act
        var result = await _service.UpdateProduct(updateRequest);

        // Assert
        Assert.Null(result);

        _mapperMock.Verify(
            m => m.Map<ProductResponse>(It.IsAny<Product>()),
            Times.Once
        );
    }
}