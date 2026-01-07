using Moq;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.Services;

namespace ProductsUnitTests;

public class ProductsDeleterServiceTests
{
    private readonly Mock<IProductsRepository> _productsRepositoryMock;
    private readonly ProductsDeleterService _service;

    public ProductsDeleterServiceTests()
    {
        _productsRepositoryMock = new Mock<IProductsRepository>();

        _service = new ProductsDeleterService(
            _productsRepositoryMock.Object
        );
    }

    [Fact]
    public async Task DeleteProduct_ExistingProduct_ReturnsTrue()
    {
        // Arrange
        Guid productId = Guid.NewGuid();

        _productsRepositoryMock
            .Setup(r => r.DeleteProduct(productId))
            .ReturnsAsync(true);

        // Act
        bool result = await _service.DeleteProduct(productId);

        // Assert
        Assert.True(result);

        _productsRepositoryMock.Verify(
            r => r.DeleteProduct(productId),
            Times.Once
        );
    }

    [Fact]
    public async Task DeleteProduct_NonExistingProduct_ReturnsFalse()
    {
        // Arrange
        Guid productId = Guid.NewGuid();

        _productsRepositoryMock
            .Setup(r => r.DeleteProduct(productId))
            .ReturnsAsync(false);

        // Act
        bool result = await _service.DeleteProduct(productId);

        // Assert
        Assert.False(result);

        _productsRepositoryMock.Verify(
            r => r.DeleteProduct(productId),
            Times.Once
        );
    }
}