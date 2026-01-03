using Microsoft.AspNetCore.Mvc;

namespace TestMicroservice.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public TestController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// get message from specified instance
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public string Get()
        {
            return $"Hello World From {_configuration["App_Name"]}";
        }
    }
}
