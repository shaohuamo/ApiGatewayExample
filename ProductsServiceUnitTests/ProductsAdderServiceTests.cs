using AutoMapper;
using Microsoft.Extensions.Configuration;
using Moq;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.RabbitMQ;
using ProductsMicroservice.Core.Services;

namespace ProductsUnitTests;

public class ProductsAdderServiceTests
{
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IProductsRepository> _productsRepositoryMock;
    private readonly Mock<IRabbitMQPublisher> _rabbitMQPublisherMock;
    private readonly Mock<IConfiguration> _configurationMock;

    private readonly ProductsAdderService _productAdderService;

    public ProductsAdderServiceTests()
    {
        _mapperMock = new Mock<IMapper>();
        _productsRepositoryMock = new Mock<IProductsRepository>();
        _rabbitMQPublisherMock = new Mock<IRabbitMQPublisher>();
        _configurationMock = new Mock<IConfiguration>();

        _configurationMock
            .Setup(c => c["RabbitMQ_Products_RoutingKey"])
            .Returns("products.add");

        _productAdderService = new ProductsAdderService(
            _mapperMock.Object,
            _productsRepositoryMock.Object,
            _rabbitMQPublisherMock.Object,
            _configurationMock.Object
        );
    }

    [Fact]
    public async Task AddProduct_NullRequest_ThrowsArgumentNullException()
    {
        // Arrange
        ProductAddRequest? request = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _productAdderService.AddProduct(request!)
        );
    }

    [Fact]
    public async Task AddProduct_RepositoryReturnsNull_ReturnsNull()
    {
        // Arrange
        var request = new ProductAddRequest();

        var mappedProduct = new Product();

        _mapperMock
            .Setup(m => m.Map<Product>(request))
            .Returns(mappedProduct);

        _productsRepositoryMock
            .Setup(r => r.AddProduct(mappedProduct))
            .ReturnsAsync((Product?)null);

        // Act
        var result = await _productAdderService.AddProduct(request);

        // Assert
        Assert.Null(result);

        _rabbitMQPublisherMock.Verify(
            p => p.Publish(It.IsAny<string>(), It.IsAny<ProductAddMessage>()),
            Times.Never
        );
    }

    [Fact]
    public async Task AddProduct_ValidRequest_AddsProductAndPublishesMessage()
    {
        // Arrange
        var request = new ProductAddRequest
        {
            ProductName = "Test Product",
            UnitPrice = 100,
            QuantityInStock = 10
        };

        var mappedProduct = new Product
        {
            ProductName = "Test Product",
            UnitPrice = 100,
            QuantityInStock = 10
        };

        var addedProduct = new Product
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Test Product",
            UnitPrice = 100,
            QuantityInStock = 10
        };

        var expectedResponse = new ProductResponse
        {
            ProductId = addedProduct.ProductId,
            ProductName = addedProduct.ProductName,
            UnitPrice = addedProduct.UnitPrice,
            QuantityInStock = addedProduct.QuantityInStock
        };

        _mapperMock
            .Setup(m => m.Map<Product>(request))
            .Returns(mappedProduct);

        _productsRepositoryMock
            .Setup(r => r.AddProduct(mappedProduct))
            .ReturnsAsync(addedProduct);

        _mapperMock
            .Setup(m => m.Map<ProductResponse>(addedProduct))
            .Returns(expectedResponse);

        // Act
        var result = await _productAdderService.AddProduct(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedResponse.ProductId, result!.ProductId);
        Assert.Equal(expectedResponse.ProductName, result.ProductName);

        _rabbitMQPublisherMock.Verify(
            p => p.Publish(
                "products.add",
                It.Is<ProductAddMessage>(m =>
                    m.ProductId == addedProduct.ProductId &&
                    m.ProductName == addedProduct.ProductName &&
                    m.UnitPrice == addedProduct.UnitPrice &&
                    m.QuantityInStock == addedProduct.QuantityInStock
                )
            ),
            Times.Once
        );
    }
}