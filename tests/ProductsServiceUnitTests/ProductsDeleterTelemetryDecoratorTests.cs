using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Observability;

namespace ProductsMicroservice.Tests;

public class ProductsDeleterTelemetryDecoratorTests
{
    private readonly Mock<IProductsDeleterService> _innerMock = new();
    private readonly Mock<ILogger<ProductsDeleterTelemetryDecorator>> _loggerMock = new();
    private readonly ProductsDeleterTelemetryDecorator _decorator;

    public ProductsDeleterTelemetryDecoratorTests()
    {
        _decorator = new ProductsDeleterTelemetryDecorator(_innerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldThrow_WhenProductIdIsEmpty()
    {
        Func<Task> act = () => _decorator.DeleteProductAsync(Guid.Empty);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteProductAsync_ShouldReturnInnerResult(bool deleted)
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId)).ReturnsAsync(deleted);

        var result = await _decorator.DeleteProductAsync(productId);

        result.Should().Be(deleted);
    }

    [Fact]
    public async Task DeleteProductAsync_ShouldRethrowInnerException()
    {
        var productId = Guid.NewGuid();
        _innerMock.Setup(x => x.DeleteProductAsync(productId))
            .ThrowsAsync(new InvalidOperationException("failure"));

        Func<Task> act = () => _decorator.DeleteProductAsync(productId);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure");
    }
}
