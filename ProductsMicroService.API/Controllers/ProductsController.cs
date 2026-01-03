using Microsoft.AspNetCore.Mvc;
using ProductsMicroservice.Core.DTO;
using ProductsMicroservice.Core.ServiceContracts;

namespace ProductsMicroService.API.Controllers
{
    /// <summary>
    /// Products Controller
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductsGetterService _productsGetterService;
        private readonly IProductsAdderService _productsAdderService;
        private readonly IProductsDeleterService _productsDeleterService;
        private readonly IProductsUpdaterService _productsUpdaterService;
        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="productsUpdaterService"></param>
        /// <param name="productsGetterService"></param>
        /// <param name="productsAdderService"></param>
        /// <param name="productsDeleterService"></param>
        public ProductsController(IProductsUpdaterService productsUpdaterService, 
            IProductsGetterService productsGetterService, 
            IProductsAdderService productsAdderService, 
            IProductsDeleterService productsDeleterService)
        {
            _productsUpdaterService = productsUpdaterService;
            _productsGetterService = productsGetterService;
            _productsAdderService = productsAdderService;
            _productsDeleterService = productsDeleterService;
        }

        //GET /api/products
        /// <summary>
        /// get all products
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IEnumerable<ProductResponse?>> GetAllProducts()
        {
            var products = await _productsGetterService.GetProducts();
            return products;
        }

        //GET /api/products/search/product-id/xxxxxxxxxxxxxxxxxxx
        /// <summary>
        /// get products by productId
        /// </summary>
        /// <returns></returns>
        [HttpGet("search/product-id/{productId:guid}")]
        public async Task<ActionResult<ProductResponse>> GetProductByProductId(Guid productId)
        {
            ProductResponse? product = await _productsGetterService.GetProductByCondition(
                temp => temp.ProductId == productId);

            if (product == null)
                return NotFound();

            return product;
        }

        //POST /api/products
        /// <summary>
        /// add a new product
        /// </summary>
        /// <param name="productAddRequest"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Post(ProductAddRequest productAddRequest)
        {
            var addedProductResponse = await _productsAdderService.AddProduct(productAddRequest);

            if (addedProductResponse == null)
            {
                return Problem("Error in adding product");
            }

            //add location header in response like below
            //api/products/search/product-id/xxxxxxxxxxxxxxxxxxx
            return CreatedAtAction(nameof(GetProductByProductId),
                new{ productId = addedProductResponse.ProductId}, addedProductResponse);
        }

        //PUT /api/products
        /// <summary>
        /// update a product
        /// </summary>
        /// <param name="productUpdateRequest"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Put(ProductUpdateRequest productUpdateRequest)
        {
            var updatedProductResponse = await _productsUpdaterService.UpdateProduct(productUpdateRequest);

            if (updatedProductResponse != null)
                return Ok(updatedProductResponse);
            else
                return Problem("Invalid ProductId");
        }


        //DELETE /api/products/xxxxxxxxxxxxxxxxxxx
        /// <summary>
        /// delete a product by productId
        /// </summary>
        /// <param name="productId"></param>
        /// <returns></returns>
        [HttpDelete("{ProductID:guid}")]
        public async Task<IActionResult> Delete(Guid productId)
        {
            bool isDeleted = await _productsDeleterService.DeleteProduct(productId);
            if (isDeleted)
                return Ok(true);
            else
                return Problem("Invalid ProductId");
        }
    }
}
