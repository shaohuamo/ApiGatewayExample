using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.MessageQueue.Abstractions;
using ProductsMicroservice.Core.MessageQueue.Messages;
using ProductsMicroservice.Core.Services;

namespace ProductsMicroservice.Tests;

public class ProductsUpdaterServiceTests
{
    private readonly Mock<IProductsRepository> _repoMock = new();
    private readonly Mock<IMapper> _mapperMock = new();
    private readonly Mock<IProductUpdateMessagePublisher> _productUpdatePublisherMock = new();
    private readonly Mock<ILogger<ProductsUpdaterService>> _loggerMock = new();

    private readonly ProductsUpdaterService _service;

    public ProductsUpdaterServiceTests()
    {
        _service = new ProductsUpdaterService(
            _repoMock.Object,
            _mapperMock.Object,
            _productUpdatePublisherMock.Object,
            _loggerMock.Object
        );
    }

    #region Null Input

    [Fact]
    public async Task UpdateProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = async () => await _service.UpdateProductAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Success

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnMappedResponse_WhenSuccessful()
    {
        var request = CreateRequest();

        var product = new Product { ProductId = request.ProductId };
        var updatedProduct = new Product { ProductId = request.ProductId, Version = 7 };
        var response = new ProductResponse();

        _mapperMock.Setup(x => x.Map<Product>(request))
            .Returns(product);

        _repoMock.Setup(x => x.UpdateProductAsync(product))
            .ReturnsAsync(updatedProduct);

        _mapperMock.Setup(x => x.Map<ProductResponse>(updatedProduct))
            .Returns(response);

        var result = await _service.UpdateProductAsync(request);

        result.Should().Be(response);

        _mapperMock.Verify(x => x.Map<Product>(request), Times.Once);
        _repoMock.Verify(x => x.UpdateProductAsync(product), Times.Once);
        _productUpdatePublisherMock.Verify(x => x.PublishAsync(
            It.Is<ProductUpdatedMessage>(message =>
                message.ProductId == updatedProduct.ProductId &&
                message.ProductName == updatedProduct.ProductName &&
                message.UnitPrice == updatedProduct.UnitPrice &&
                message.QuantityInStock == updatedProduct.QuantityInStock &&
                message.Version == updatedProduct.Version),
            It.IsAny<CancellationToken>()), Times.Once);
        _mapperMock.Verify(x => x.Map<ProductResponse>(updatedProduct), Times.Once);
    }

    #endregion

    #region Product Not Found

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnNull_WhenProductNotFound()
    {
        var request = CreateRequest();

        var product = new Product();

        _mapperMock.Setup(x => x.Map<Product>(request))
            .Returns(product);

        _repoMock.Setup(x => x.UpdateProductAsync(product))
            .ReturnsAsync((Product?)null);

        var result = await _service.UpdateProductAsync(request);

        result.Should().BeNull();

        _productUpdatePublisherMock.Verify(x => x.PublishAsync(
            It.IsAny<ProductUpdatedMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _mapperMock.Verify(x => x.Map<ProductResponse>(It.IsAny<Product>()), Times.Never);
    }

    #endregion

    #region Repository Exception

    [Fact]
    public async Task UpdateProductAsync_ShouldThrow_WhenRepositoryThrows()
    {
        var request = CreateRequest();

        var product = new Product();

        _mapperMock.Setup(x => x.Map<Product>(request))
            .Returns(product);

        _repoMock.Setup(x => x.UpdateProductAsync(product))
            .ThrowsAsync(new Exception("DB error"));

        Func<Task> act = async () => await _service.UpdateProductAsync(request);

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("DB error");

        _productUpdatePublisherMock.Verify(x => x.PublishAsync(
            It.IsAny<ProductUpdatedMessage>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Mapper Validation

    [Fact]
    public async Task UpdateProductAsync_ShouldCallMapperWithCorrectInput()
    {
        var request = CreateRequest();

        _mapperMock.Setup(x => x.Map<Product>(request))
            .Returns(new Product());

        _repoMock.Setup(x => x.UpdateProductAsync(It.IsAny<Product>()))
            .ReturnsAsync(new Product());

        _mapperMock.Setup(x => x.Map<ProductResponse>(It.IsAny<Product>()))
            .Returns(new ProductResponse());

        await _service.UpdateProductAsync(request);

        _mapperMock.Verify(x => x.Map<Product>(
            It.Is<ProductUpdateRequest>(r =>
                r.ProductId == request.ProductId &&
                r.ProductName == request.ProductName
            )), Times.Once);
    }

    #endregion

    #region Helpers

    private static ProductUpdateRequest CreateRequest()
    {
        return new ProductUpdateRequest
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Updated Product",
            UnitPrice = 200,
            QuantityInStock = 5
        };
    }

    #endregion
}