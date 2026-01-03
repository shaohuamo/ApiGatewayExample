using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductsMicroservice.Core.Mappers;
using ProductsMicroservice.Core.RabbitMQ;
using ProductsMicroservice.Core.ServiceContracts;
using ProductsMicroservice.Core.Services;

namespace ProductsMicroservice.Core
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddProductsMicroserviceCore(this IServiceCollection services, IConfiguration configuration)
        {
            //Add service into Ioc Container
            services.AddScoped<IProductsAdderService, ProductsAdderService>();
            services.AddScoped<IProductsDeleterService, ProductsDeleterService>();
            services.AddScoped<IProductsGetterService, ProductsGetterService>();
            services.AddScoped<IProductsUpdaterService, ProductsUpdaterService>();

            services.AddAutoMapper(typeof(ProductToProductResponseMappingProfile).Assembly);

            services.AddSingleton<IRabbitMQConnectionProvider, RabbitMQConnectionProvider>();
            services.AddSingleton<IRabbitMQPublisher, RabbitMQPublisher>();

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = $"{configuration["REDIS_HOST"]}:{configuration["REDIS_PORT"]}";
                options.InstanceName = configuration["REDIS_INSTANCE_NAME"];
            });

            return services;
        }
    }
}
