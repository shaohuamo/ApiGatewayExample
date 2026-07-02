using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Observability;

namespace ProductsMicroservice.Tests;

public class ProductsAdderTelemetryDecoratorTests
{
    private readonly Mock<IProductsAdderService> _innerMock = new();
    private readonly Mock<ILogger<ProductsAdderTelemetryDecorator>> _loggerMock = new();
    private readonly ProductsAdderTelemetryDecorator _decorator;

    public ProductsAdderTelemetryDecoratorTests()
    {
        _decorator = new ProductsAdderTelemetryDecorator(_innerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task AddProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = () => _decorator.AddProductAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task AddProductAsync_ShouldReturnInnerResponse()
    {
        var request = new ProductAddRequest { ProductName = "Product" };
        var response = new ProductResponse(Guid.NewGuid(), "Product", 10, 2);
        _innerMock.Setup(x => x.AddProductAsync(request)).ReturnsAsync(response);

        var result = await _decorator.AddProductAsync(request);

        result.Should().BeSameAs(response);
    }

    [Fact]
    public async Task AddProductAsync_ShouldRethrowInnerException()
    {
        var request = new ProductAddRequest { ProductName = "Product" };
        _innerMock.Setup(x => x.AddProductAsync(request))
            .ThrowsAsync(new InvalidOperationException("failure"));

        Func<Task> act = () => _decorator.AddProductAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure");
    }
}
