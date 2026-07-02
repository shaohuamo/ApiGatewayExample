using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroService.API.Controllers;

namespace ProductsMicroservice.Tests;

public class ProductsControllerTests
{
    private readonly Mock<IProductsGetterService> _getterMock = new();
    private readonly Mock<IProductsAdderService> _adderMock = new();
    private readonly Mock<IProductsDeleterService> _deleterMock = new();
    private readonly Mock<IProductsUpdaterService> _updaterMock = new();
    private readonly ProductsController _controller;

    public ProductsControllerTests()
    {
        _controller = new ProductsController(
            _updaterMock.Object,
            _getterMock.Object,
            _adderMock.Object,
            _deleterMock.Object);
    }

    #region Get Products

    [Fact]
    public async Task GetAllProductsAsync_ShouldReturnProducts()
    {
        List<ProductResponse?> products = [new ProductResponse(), null];
        _getterMock.Setup(x => x.GetProductsAsync()).ReturnsAsync(products);

        var result = await _controller.GetAllProductsAsync();

        result.Should().BeSameAs(products);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnProduct_WhenFound()
    {
        var productId = Guid.NewGuid();
        var product = new ProductResponse(productId, "Product", 10, 2);
        _getterMock.Setup(x => x.GetProductByProductIdAsync(productId)).ReturnsAsync(product);

        var result = await _controller.GetProductByProductIdAsync(productId);

        result.Value.Should().BeSameAs(product);
    }

    [Fact]
    public async Task GetProductByProductIdAsync_ShouldReturnNotFound_WhenMissing()
    {
        var productId = Guid.NewGuid();
        _getterMock.Setup(x => x.GetProductByProductIdAsync(productId))
            .ReturnsAsync((ProductResponse?)null);

        var result = await _controller.GetProductByProductIdAsync(productId);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Add Product

    [Fact]
    public async Task AddNewProductAsync_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        var result = await _controller.AddNewProductAsync(null);

        result.Should().BeOfType<BadRequestObjectResult>()
            .Which.Value.Should().Be("The request body cannot be empty and must be a valid JSON.");
        _adderMock.Verify(x => x.AddProductAsync(It.IsAny<ProductAddRequest>()), Times.Never);
    }

    [Fact]
    public async Task AddNewProductAsync_ShouldReturnProblem_WhenServiceFails()
    {
        var request = new ProductAddRequest();
        _adderMock.Setup(x => x.AddProductAsync(request)).ReturnsAsync((ProductResponse?)null);

        var result = await _controller.AddNewProductAsync(request);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task AddNewProductAsync_ShouldReturnCreatedAtAction_WhenSuccessful()
    {
        var request = new ProductAddRequest();
        var response = new ProductResponse(Guid.NewGuid(), "Product", 10, 2);
        _adderMock.Setup(x => x.AddProductAsync(request)).ReturnsAsync(response);

        var result = await _controller.AddNewProductAsync(request);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(ProductsController.GetProductByProductIdAsync));
        created.RouteValues.Should().ContainKey("productId").WhoseValue.Should().Be(response.ProductId);
        created.Value.Should().BeSameAs(response);
    }

    #endregion

    #region Update Product

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnBadRequest_WhenRequestIsNull()
    {
        var result = await _controller.UpdateProductAsync(null);

        result.Should().BeOfType<BadRequestObjectResult>();
        _updaterMock.Verify(x => x.UpdateProductAsync(It.IsAny<ProductUpdateRequest>()), Times.Never);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnProblem_WhenProductIsMissing()
    {
        var request = new ProductUpdateRequest();
        _updaterMock.Setup(x => x.UpdateProductAsync(request)).ReturnsAsync((ProductResponse?)null);

        var result = await _controller.UpdateProductAsync(request);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task UpdateProductAsync_ShouldReturnOk_WhenSuccessful()
    {
        var request = new ProductUpdateRequest();
        var response = new ProductResponse();
        _updaterMock.Setup(x => x.UpdateProductAsync(request)).ReturnsAsync(response);

        var result = await _controller.UpdateProductAsync(request);

        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(response);
    }

    #endregion

    #region Delete Product

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteProductAsync_ShouldReturnExpectedResult(bool isDeleted)
    {
        var productId = Guid.NewGuid();
        _deleterMock.Setup(x => x.DeleteProductAsync(productId)).ReturnsAsync(isDeleted);

        var result = await _controller.DeleteProductAsync(productId);

        if (isDeleted)
        {
            result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(true);
        }
        else
        {
            result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
        }
    }

    #endregion
}
