using ApiGateway.ConsulServiceBuilder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using System.Diagnostics;
using System.Security.Claims;

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

builder.Configuration.AddEnvironmentVariables();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"] ?? "gateway-api";
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Authentication:RequireHttpsMetadata", false);
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var jti = GetClaimValue(context.Principal, "jti");

                if (string.IsNullOrWhiteSpace(jti))
                {
                    return;
                }

                var redis = context.HttpContext.RequestServices.GetService<IConnectionMultiplexer>();

                if (redis is null)
                {
                    return;
                }

                try
                {
                    var denylistPrefix = context.HttpContext.RequestServices
                        .GetRequiredService<IConfiguration>()["Authentication:AccessTokenDenylistPrefix"]
                        ?? "admin-web:access-token-denylist";
                    var isDenied = await redis.GetDatabase().KeyExistsAsync($"{denylistPrefix}:{jti}");

                    if (isDenied)
                    {
                        context.Fail("Access token has been revoked.");
                    }
                }
                catch
                {
                    var failClosed = context.HttpContext.RequestServices
                        .GetRequiredService<IConfiguration>()
                        .GetValue("Authentication:DenylistFailClosed", true);

                    if (failClosed)
                    {
                        context.Fail("Access token denylist is unavailable.");
                    }
                }
            }
        };

        var metadataAddress = builder.Configuration["Authentication:MetadataAddress"];
        if (!string.IsNullOrWhiteSpace(metadataAddress))
        {
            options.MetadataAddress = metadataAddress;
        }
    });

var redisConnectionString = builder.Configuration["Authentication:DenylistRedisConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var redisConfiguration = ConfigurationOptions.Parse(redisConnectionString);
        redisConfiguration.AbortOnConnectFail = false;
        redisConfiguration.ConnectRetry = 5;
        redisConfiguration.ConnectTimeout = 5000;
        redisConfiguration.SyncTimeout = 5000;

        return ConnectionMultiplexer.Connect(redisConfiguration);
    });
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Ocelot.ApiGateway"))
    .WithLogging(logging => logging.AddOtlpExporter(), options =>
    {
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
    })
    .WithTracing(tracerBuilder => tracerBuilder
        .AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = context =>
                !IsNoisyGatewayPath(context.Request.Path);
        })
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
app.UseAuthentication();
app.Use(async (context, next) =>
{
    context.Request.Headers.Remove("X-User-Id");

    var userId = GetClaimValue(context.User, "sub")
                 ?? GetClaimValue(context.User, ClaimTypes.NameIdentifier)
                 ?? GetClaimValue(context.User, "nameidentifier");

    if (!string.IsNullOrEmpty(userId))
    {
        context.Request.Headers["X-User-Id"] = userId;
        Baggage.SetBaggage("user_id", userId);
        Activity.Current?.SetTag("user_id", userId);
    }

    await next();
});
await app.UseOcelot();

app.Run();

static string? GetClaimValue(ClaimsPrincipal? user, string claimType)
{
    return user?.FindFirst(claimType)?.Value;
}

static bool IsNoisyGatewayPath(PathString path)
{
    return path == "/"
        || path.StartsWithSegments("/favicon.ico")
        || path.StartsWithSegments("/___proxy_subdomain_cpanel")
        || path.StartsWithSegments("/wp-json")
        || path.StartsWithSegments("/xmlrpc.php")
        || path.StartsWithSegments("/console")
        || path == "/server"
        || path.StartsWithSegments("/server-status");
}
