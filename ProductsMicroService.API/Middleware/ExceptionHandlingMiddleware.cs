namespace ProductsMicroService.API.Middleware
{
    /// <summary>
    /// Custom Exception Handle Middleware
    /// </summary>
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="next">next middleware</param>
        /// <param name="logger"></param>
        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }


        /// <summary>
        /// Middleware Logical 
        /// </summary>
        /// <param name="httpContext"></param>
        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                {
                    _logger.LogError("{ExceptionType} {ExceptionMessage}",
                        ex.InnerException.GetType().ToString(), ex.InnerException.Message);
                }
                else
                {
                    _logger.LogError("{ExceptionType} {ExceptionMessage}",
                        ex.GetType().ToString(), ex.Message);
                }

                httpContext.Response.StatusCode = 500;
                await httpContext.Response.WriteAsJsonAsync(
                    new { ex.Message, Type = ex.GetType().ToString() });
            }
        }
    }

    /// <summary>
    /// ExceptionHandlingMiddleware Helper class
    /// </summary>
    public static class ExceptionHandlingMiddlewareExtensions
    {
        /// <summary>
        /// Add ExceptionHandlingMiddleware to middleware pipeline
        /// </summary>
        /// <param name="builder">applicationBuilder</param>
        /// <returns></returns>
        public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ExceptionHandlingMiddleware>();
        }
    }
}
