using CommonService.Idempotency;
using CommonService.Middlewares;
using CommonService.RabbitMQ;
using CommonService.ServiceBus;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Steeltoe.Discovery.Consul;
using TestMicroservice.API.Diagnostics;
using TestMicroservice.API.RabbitMQ;
using TestMicroservice.API.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

//Add Observability services (OpenTelemetry)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(DiagnosticsConfig.ServiceName))
    .WithLogging(logging => logging.AddOtlpExporter(), options =>
    {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    })
    .WithTracing(tracerBuilder => tracerBuilder
        .AddSource(DiagnosticsConfig.ServiceName)
        .AddSource(RabbitMQTelemetry.ActivitySource.Name)
        .AddSource(ServiceBusTelemetry.ActivitySource.Name)
        .AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = context =>
                !context.Request.Path.StartsWithSegments("/health");
        })
        .AddHttpClientInstrumentation(options =>
        {
            options.FilterHttpRequestMessage = (req) => !req.RequestUri!.Host.Contains("consul");
        })
        .AddOtlpExporter())
    .WithMetrics(meterBuilder => meterBuilder
        //custom metric
        .AddMeter(DiagnosticsConfig.TestMeter.Name)
        .AddProcessInstrumentation()
        .AddRuntimeInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .SetExemplarFilter(ExemplarFilterType.TraceBased)
        .AddOtlpExporter());

// Add services to the container.
builder.Services.AddControllers();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = builder.Configuration["Redis:InstanceName"];
});

builder.Services.Configure<MessageIdempotencyOptions>(
    builder.Configuration.GetSection(MessageIdempotencyOptions.SectionName));
builder.Services.AddSingleton<IProcessedMessageStore, RedisProcessedMessageStore>();

//Register Consul service discovery (only when deployed with Docker; K8s uses its own service discovery)
if (builder.Configuration.GetValue<bool>("UseConsul", false))
{
    builder.Services.AddConsulDiscoveryClient();
}

builder.Services.AddSingleton<IRabbitMQConnectionProvider, RabbitMQConnectionProvider>();
builder.Services.AddSingleton<IRabbitMQProductAddConsumer, RabbitMQProductAddConsumer>();
builder.Services.AddSingleton<IServiceBusProductUpdatesConsumer, ServiceBusProductUpdatesConsumer>();
builder.Services.AddHostedService<RabbitMQProductAddHostedService>();
builder.Services.AddHostedService<ServiceBusProductUpdatesHostedService>();

builder.Services.Configure<RabbitMQOptions>(
    builder.Configuration.GetSection("RabbitMQ"));
builder.Services.Configure<ServiceBusOptions>(
    builder.Configuration.GetSection(ServiceBusOptions.SectionName));

// Add health checks for Kubernetes probes
builder.Services.AddHealthChecks();

var app = builder.Build();

app.TraceContextMiddleware();

// Map health check endpoint for Kubernetes readiness/liveness probes
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();
