using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductsMicroservice.Core.Domain.RepositoryContracts;
using ProductsMicroservice.Infrastructure.DbContext;
using ProductsMicroservice.Infrastructure.Repositories;

namespace ProductsMicroservice.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection ProductsMicroserviceInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            //Get PostgresConnection
            string connectionStringTemplate = configuration.GetConnectionString("DefaultConnection")!;
            string connectionString = connectionStringTemplate
                .Replace("$MYSQL_HOST", configuration["MYSQL_HOST"])
                .Replace("$MYSQL_PASSWORD", configuration["MYSQL_PASSWORD"])
                .Replace("$MYSQL_DATABASE", configuration["MYSQL_DATABASE"])
                .Replace("$MYSQL_PORT", configuration["MYSQL_PORT"])
                .Replace("$MYSQL_USER", configuration["MYSQL_USER"]);

            //Add ProductsMicroservice.Infrastructure Layer services into the IoC container
            services.AddDbContext<ApplicationDbContext>(options => {
                options.UseMySQL(connectionString);
            });

            services.AddScoped<IProductsRepository, ProductsRepository>();
            return services;
        }
    }
}
