using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Observability;

namespace ProductsMicroservice.Tests;

public class ProductsGetterTelemetryDecoratorTests
{
    private readonly Mock<IProductsGetterService> _innerMock = new();
    private readonly Mock<ILogger<ProductsGetterTelemetryDecorator>> _loggerMock = new();
    private readonly ProductsGetterTelemetryDecorator _decorator;

    public ProductsGetterTelemetryDecoratorTests()
    {
        _decorator = new ProductsGetterTelemetryDecorator(_innerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldReturnInnerProducts()
    {
        List<ProductResponse?> products = [new ProductResponse(), null];
        _innerMock.Setup(x => x.GetProductsAsync()).ReturnsAsync(products);

        var result = await _decorator.GetProductsAsync();

        result.Should().BeSameAs(products);
    }

    [Fact]
    public async Task GetProductsAsync_ShouldRethrowInnerException()
    {
        _innerMock.Setup(x => x.GetProductsAsync()).ThrowsAsync(new InvalidOperationException("failure"));

        Func<Task> act = () => _decorator.GetProductsAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure");
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldThrow_WhenProductIdIsEmpty()
    {
        Func<Task> act = () => _decorator.GetProductByProductIdAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetProductByProductIdAsync_ShouldReturnInnerResult(bool found)
    {
        var productId = Guid.NewGuid();
        ProductResponse? response = found ? new ProductResponse(productId, "Product", 10, 2) : null;
        _innerMock.Setup(x => x.GetProductByProductIdAsync(productId)).ReturnsAsync(response);

        var result = await _decorator.GetProductByProductIdAsync(productId);

        result.Should().BeSameAs(response);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldRethrowInnerException()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.GetProductByProductIdAsync(productId))
            .ThrowsAsync(new InvalidOperationException("failure"));

        Func<Task> act = () => _decorator.GetProductByProductIdAsync(productId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure");
    }
}
