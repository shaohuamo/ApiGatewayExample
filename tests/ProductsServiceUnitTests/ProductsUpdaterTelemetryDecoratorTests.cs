using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Infrastructure.Decorators.Observability;

namespace ProductsMicroservice.Tests;

public class ProductsUpdaterTelemetryDecoratorTests
{
    private readonly Mock<IProductsUpdaterService> _innerMock = new();
    private readonly Mock<ILogger<ProductsUpdaterTelemetryDecorator>> _loggerMock = new();
    private readonly ProductsUpdaterTelemetryDecorator _decorator;

    public ProductsUpdaterTelemetryDecoratorTests()
    {
        _decorator = new ProductsUpdaterTelemetryDecorator(_innerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldThrow_WhenRequestIsNull()
    {
        Func<Task> act = () => _decorator.UpdateProductAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateProductAsync_ShouldReturnInnerResult(bool updated)
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid(), ProductName = "Product" };
        ProductResponse? response = updated ? new ProductResponse() : null;
        _innerMock.Setup(x => x.UpdateProductAsync(request)).ReturnsAsync(response);

        var result = await _decorator.UpdateProductAsync(request);

        result.Should().BeSameAs(response);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldRethrowInnerException()
    {
        var request = new ProductUpdateRequest { ProductId = Guid.NewGuid(), ProductName = "Product" };
        _innerMock.Setup(x => x.UpdateProductAsync(request))
            .ThrowsAsync(new InvalidOperationException("failure"));

        Func<Task> act = () => _decorator.UpdateProductAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("failure");
    }
}
