using AutoMapper;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsUpdaterService: IProductsUpdaterService
{
    private readonly IMapper _mapper;
    private readonly IProductsRepository _productsRepository;

    public ProductsUpdaterService(IProductsRepository productsRepository, IMapper mapper)
    {
        _productsRepository = productsRepository;
        _mapper = mapper;
    }

    public async Task<ProductResponse?> UpdateProduct(ProductUpdateRequest productUpdateRequest)
    {
        //Map from ProductUpdateRequest to Product type
        Product product = _mapper.Map<Product>(productUpdateRequest); 

        Product? updatedProduct = await _productsRepository.UpdateProduct(product);

        ProductResponse? updatedProductResponse = _mapper.Map<ProductResponse>(updatedProduct);

        return updatedProductResponse;
    }
}