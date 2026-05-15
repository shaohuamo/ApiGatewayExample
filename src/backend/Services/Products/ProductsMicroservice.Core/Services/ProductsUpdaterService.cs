using AutoMapper;
using Microsoft.Extensions.Logging;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.MessageQueue.Abstractions;
using ProductsMicroservice.Core.MessageQueue.Messages;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsUpdaterService: IProductsUpdaterService
{
    private readonly IMapper _mapper;
    private readonly IProductsRepository _productsRepository;
    private readonly IProductUpdateMessagePublisher _productUpdateMessagePublisher;
    private readonly ILogger<ProductsUpdaterService> _logger;

    public ProductsUpdaterService(
        IProductsRepository productsRepository,
        IMapper mapper,
        IProductUpdateMessagePublisher productUpdateMessagePublisher,
        ILogger<ProductsUpdaterService> logger)
    {
        _productsRepository = productsRepository;
        _mapper = mapper;
        _productUpdateMessagePublisher = productUpdateMessagePublisher;
        _logger = logger;
    }

    public async Task<ProductResponse?> UpdateProductAsync(ProductUpdateRequest productUpdateRequest)
    {
        ArgumentNullException.ThrowIfNull(productUpdateRequest);//defend against null input

        _logger.LogInformation("Updating product: {ProductId}", productUpdateRequest.ProductId);

        Product product = _mapper.Map<Product>(productUpdateRequest);

        //update the product
        Product? updatedProduct = await _productsRepository.UpdateProductAsync(product);

        if (updatedProduct == null)
        {
            _logger.LogWarning("Product update returned null from repository for {ProductId}",
                productUpdateRequest.ProductId);
            return null;
        }

        var productUpdatedMessage = new ProductUpdatedMessage(
            updatedProduct.ProductId,
            updatedProduct.ProductName,
            updatedProduct.UnitPrice,
            updatedProduct.QuantityInStock,
            updatedProduct.Version);

        await _productUpdateMessagePublisher.PublishAsync(productUpdatedMessage);

        _logger.LogInformation("Product updated event published successfully for {ProductId}",
            updatedProduct.ProductId);

        return _mapper.Map<ProductResponse>(updatedProduct);
    }
}