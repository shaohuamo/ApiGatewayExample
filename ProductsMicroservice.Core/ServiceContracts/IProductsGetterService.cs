using ProductsMicroservice.Core.DTO;
using System.Linq.Expressions;
using ProductsMicroservice.Core.Domain.Entities;

namespace ProductsMicroservice.Core.ServiceContracts
{
    public interface IProductsGetterService
    {
        /// <summary>
        /// Retrieves products from the products repository
        /// </summary>
        /// <returns>Returns list of ProductResponse objects</returns>
        Task<IEnumerable<ProductResponse?>> GetProducts();


        /// <summary>
        /// Returns a single product that matches with given condition
        /// </summary>
        /// <param name="conditionExpression">Express that represents the condition to check</param>
        /// <returns>Returns matching product or null</returns>
        Task<ProductResponse?> GetProductByCondition(Expression<Func<Product, bool>> conditionExpression);
    }
}
