using CommonService.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using ProductUpdatesReprocessor.Functions.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddOptions<ServiceBusOptions>()
            .Bind(context.Configuration.GetSection(ServiceBusOptions.SectionName));
        services.PostConfigure<ServiceBusOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                options.ConnectionString = context.Configuration["ServiceBusConnectionString"] ?? string.Empty;
            }
        });
        services.AddSingleton<ProductUpdatesDlqReprocessor>();
    })
    .Build();

await host.RunAsync();
