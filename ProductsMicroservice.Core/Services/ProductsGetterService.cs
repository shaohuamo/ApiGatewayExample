using System.Linq.Expressions;
using System.Text.Json;
using AutoMapper;
using Microsoft.Extensions.Caching.Distributed;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsGetterService: IProductsGetterService
{
    private readonly IProductsRepository _productsRepository;
    private readonly IMapper _mapper;
    private readonly IDistributedCache _distributedCache;
    public ProductsGetterService(IProductsRepository productsRepository, IMapper mapper, IDistributedCache distributedCache)
    {
        _productsRepository = productsRepository;
        _mapper = mapper;
        _distributedCache = distributedCache;
    }
    public async Task<IEnumerable<ProductResponse?>> GetProducts()
    {
        //get all products from cache
        string cacheKey = "all-products";
        string? cachedProducts = await _distributedCache.GetStringAsync(cacheKey);

        if (cachedProducts != null)
        {
            var productsFromCache = JsonSerializer.Deserialize<List<ProductResponse?>>(cachedProducts);
            return productsFromCache!;
        }

        //get all products from db
        IEnumerable<Product?> products = await _productsRepository.GetProducts();

        //Invokes ProductToProductResponseMappingProfile
        IEnumerable<ProductResponse?> productResponses = _mapper.Map<IEnumerable<ProductResponse>>(products);
        List<ProductResponse?> productsList = productResponses.ToList();

        //store all products in cache
        string productsJson = JsonSerializer.Serialize(productsList);

        DistributedCacheEntryOptions options = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromSeconds(300))
            .SetSlidingExpiration(TimeSpan.FromSeconds(100));

        await _distributedCache.SetStringAsync(cacheKey, productsJson, options);

        return productsList;
    }

    public async Task<ProductResponse?> GetProductByCondition(Expression<Func<Product, bool>> conditionExpression)
    {
        Product? product = await _productsRepository.GetProductByCondition(conditionExpression);

        if (product == null)
        {
            return null;
        }

        //Invokes ProductToProductResponseMappingProfile
        ProductResponse productResponse = _mapper.Map<ProductResponse>(product); 
        return productResponse;
    }
}