using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroservice.Core.Services;

public class ProductsDeleterService: IProductsDeleterService
{
    private readonly IProductsRepository _productsRepository;

    public ProductsDeleterService(IProductsRepository productsRepository)
    {
        _productsRepository = productsRepository;
    }

    public async Task<bool> DeleteProduct(Guid productId)
    {
        //Attempt to delete product
        bool isDeleted = await _productsRepository.DeleteProduct(productId);

        return isDeleted;
    }
}