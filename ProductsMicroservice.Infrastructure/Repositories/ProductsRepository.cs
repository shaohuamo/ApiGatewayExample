using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ProductsMicroservice.Core.Domain.Entities;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Infrastructure.DbContext;

namespace ProductsMicroservice.Infrastructure.Repositories
{
    public class ProductsRepository : IProductsRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public ProductsRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IEnumerable<Product>> GetProducts()
        {
            return await _dbContext.Products.ToListAsync();
        }

        public async Task<Product?> GetProductByCondition(Expression<Func<Product, bool>> conditionExpression)
        {
            return await _dbContext.Products.FirstOrDefaultAsync(conditionExpression);
        }

        public async Task<Product?> AddProduct(Product product)
        {
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();
            return product;
        }

        public async Task<Product?> UpdateProduct(Product product)
        {
            Product? existingProduct = await _dbContext.Products.FirstOrDefaultAsync(
                temp => temp.ProductId == product.ProductId);
            if (existingProduct == null)
            {
                return null;
            }

            existingProduct.ProductName = product.ProductName;
            existingProduct.UnitPrice = product.UnitPrice;
            existingProduct.QuantityInStock = product.QuantityInStock;
            existingProduct.Version++;

            try
            {
                int affectedRowsCount = await _dbContext.SaveChangesAsync();

                return affectedRowsCount > 0 ? existingProduct : null;
            }
            catch (DbUpdateConcurrencyException e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public async Task<bool> DeleteProduct(Guid productId)
        {
            var existingProduct = await _dbContext.Products.FirstOrDefaultAsync(
                temp => temp.ProductId == productId);
            if (existingProduct == null)
            {
                return false;
            }

            _dbContext.Products.Remove(existingProduct);
            int affectedRowsCount = await _dbContext.SaveChangesAsync();
            return affectedRowsCount > 0;
        }
    }
}
