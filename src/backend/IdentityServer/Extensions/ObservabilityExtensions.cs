using IdentityServer.Pages;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace IdentityServer.Extensions;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(Telemetry.ServiceName)
                .AddHostDetector()
                .AddContainerDetector())
            .WithLogging(logging => logging.AddOtlpExporter(), options =>
            {
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
            })
            .WithTracing(tracerBuilder => tracerBuilder
                .AddSource(Duende.IdentityServer.IdentityServerConstants.Tracing.Basic)
                .AddSource(Duende.IdentityServer.IdentityServerConstants.Tracing.Services)
                .AddSource(Duende.IdentityServer.IdentityServerConstants.Tracing.Stores)
                .AddNpgsql()
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health")
                        && !IsStaticAssetPath(context.Request.Path);
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter())
            .WithMetrics(meterBuilder => meterBuilder
                .AddMeter(Telemetry.ServiceName)
                .AddProcessInstrumentation()
                .AddRuntimeInstrumentation()
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsqlInstrumentation()
                .SetExemplarFilter(ExemplarFilterType.TraceBased)
                .AddOtlpExporter());

        return builder;
    }

    private static bool IsStaticAssetPath(PathString path)
    {
        if (path.StartsWithSegments("/lib") || path.StartsWithSegments("/js"))
        {
            return true;
        }

        var value = path.Value;

        return value is not null
            && (value.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".map", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".woff", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase));
    }
}
