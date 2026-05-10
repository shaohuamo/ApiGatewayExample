using ApiGateway.ConsulServiceBuilder;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var useConsul = builder.Configuration.GetValue<bool>("UseConsul", false);

// Load ocelot config: Consul mode (Docker) uses ocelot.json stored in Consul;
// K8s mode uses a static file with hardcoded K8s Service DNS hosts
if (!useConsul)
{
    builder.Configuration.AddJsonFile("ocelot.k8s.json", optional: false, reloadOnChange: true);
}
else
{
    builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Ocelot.ApiGateway"))
    .WithLogging(logging => logging.AddOtlpExporter(), options =>
    {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    })
    .WithTracing(tracerBuilder => tracerBuilder
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(meterBuilder => meterBuilder
        .AddProcessInstrumentation()
        .AddRuntimeInstrumentation()
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .SetExemplarFilter(ExemplarFilterType.TraceBased)
        .AddOtlpExporter());

if (useConsul)
{
    builder.Services
        .AddOcelot(builder.Configuration)
        .AddConsul<MyConsulServiceBuilder>()
        .AddConfigStoredInConsul(); // store ocelot.json in consul server
}
else
{
    builder.Services.AddOcelot(builder.Configuration);
}

// Add health checks for Kubernetes probes
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");
await app.UseOcelot();

app.Run();
