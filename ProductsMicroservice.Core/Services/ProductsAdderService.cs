using AutoMapper;
using Microsoft.Extensions.Configuration;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.RabbitMQ;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsAdderService: IProductsAdderService
{
    private readonly IMapper _mapper;
    private readonly IProductsRepository _productsRepository;
    private readonly IRabbitMQPublisher _rabbitMQPublisher;
    private readonly IConfiguration _configuration;

    public ProductsAdderService(IMapper mapper, IProductsRepository productsRepository, IRabbitMQPublisher rabbitMQPublisher, IConfiguration configuration)
    {
        _mapper = mapper;
        _productsRepository = productsRepository;
        _rabbitMQPublisher = rabbitMQPublisher;
        _configuration = configuration;
    }

    public async Task<ProductResponse?> AddProduct(ProductAddRequest productAddRequest)
    {
        if (productAddRequest == null)
        {
            throw new ArgumentNullException(nameof(productAddRequest));
        }

        //Attempt to add product
        //Map productAddRequest into 'Product' type (it invokes ProductAddRequestToProductMappingProfile)
        Product productInput = _mapper.Map<Product>(productAddRequest); 
        Product? addedProduct = await _productsRepository.AddProduct(productInput);

        if (addedProduct == null)
        {
            return null;
        }

        //publish message to RabbitMQ
        string routingKey = _configuration["RabbitMQ_Products_RoutingKey"]!;
        var productAddMessage = new ProductAddMessage(addedProduct.ProductId, addedProduct.ProductName,
            addedProduct.UnitPrice, addedProduct.QuantityInStock);

        await _rabbitMQPublisher.Publish(routingKey, productAddMessage);

        //Map addedProduct into 'ProductResponse' type (it invokes ProductToProductResponseMappingProfile)
        ProductResponse addedProductResponse = _mapper.Map<ProductResponse>(addedProduct);

        return addedProductResponse;
    }
}