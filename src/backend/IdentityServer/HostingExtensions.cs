using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer;
using IdentityServer.Data;
using IdentityServer.Extensions;
using IdentityServer.Models;
using IdentityServer.Options;
using IdentityServer.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Filters;

namespace IdentityServer;

internal static class HostingExtensions
{
    public static WebApplicationBuilder ConfigureLogging(this WebApplicationBuilder builder)
    {
        // Write most logs to the console but diagnostic data to a file.
        // See https://docs.duendesoftware.com/identityserver/diagnostics/data
        builder.Services.AddSerilog(lc =>
        {
            lc.WriteTo.Logger(consoleLogger =>
            {
                consoleLogger.WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                    formatProvider: CultureInfo.InvariantCulture);
                if (builder.Environment.IsDevelopment())
                {
                    consoleLogger.Filter.ByExcluding(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
                }
            });
            if (builder.Environment.IsDevelopment())
            {
                lc.WriteTo.Logger(fileLogger =>
                {
                    fileLogger
                        .WriteTo.File("./diagnostics/diagnostic.log", rollingInterval: RollingInterval.Day,
                            fileSizeLimitBytes: 1024 * 1024 * 10, // 10 MB
                            rollOnFileSizeLimit: true,
                            outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}",
                            formatProvider: CultureInfo.InvariantCulture)
                        .Filter
                        .ByIncludingOnly(Matching.FromSource("Duende.IdentityServer.Diagnostics.Summary"));
                }).Enrich.FromLogContext().ReadFrom.Configuration(builder.Configuration);
            }
        });
        return builder;
    }

    public static WebApplication ConfigureServices(this WebApplicationBuilder builder)
    {
        builder.AddObservability();

        builder.Services.AddRazorPages();

        builder.Services.Configure<PostgresOptions>(builder.Configuration.GetSection(PostgresOptions.SectionName));
        builder.Services.Configure<SeedUserOptions>(builder.Configuration.GetSection(SeedUserOptions.SectionName));
        builder.Services.Configure<SigningCredentialOptions>(
            builder.Configuration.GetSection(SigningCredentialOptions.SectionName));

        var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
        {
            builder.Services
                .AddDataProtection()
                .SetApplicationName("MicroservicesDemo.IdentityServer")
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
        }

        builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            var postgresOptions = serviceProvider.GetRequiredService<IOptions<PostgresOptions>>().Value;
            var connectionStringTemplate = builder.Configuration.GetConnectionString("PostgresConnection")!;
            var connectionString = connectionStringTemplate
                .Replace("$POSTGRES_HOST", postgresOptions.Host)
                .Replace("$POSTGRES_PASSWORD", postgresOptions.Password)
                .Replace("$POSTGRES_DATABASE", postgresOptions.Database)
                .Replace("$POSTGRES_PORT", postgresOptions.Port)
                .Replace("$POSTGRES_USER", postgresOptions.User);

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: postgresOptions.MaxRetryCount,
                    maxRetryDelay: TimeSpan.FromSeconds(postgresOptions.MaxRetryDelaySeconds),
                    errorCodesToAdd: null);
            });

            if (builder.Environment.IsDevelopment())
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.Configure<IdentityOptions>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = false;
        });

        var identityServerBuilder = builder.Services
            .AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;
                options.Authentication.CookieSameSiteMode = SameSiteMode.Lax;
                options.Authentication.CheckSessionCookieSameSiteMode = SameSiteMode.Lax;
                options.KeyManagement.Enabled = false;

                var issuerUri = builder.Configuration["IdentityServer:IssuerUri"];
                if (!string.IsNullOrWhiteSpace(issuerUri))
                {
                    options.IssuerUri = issuerUri;
                }
            });

        ConfigureSigningCredential(identityServerBuilder, builder)
            .AddInMemoryIdentityResources(Config.IdentityResources)
            .AddInMemoryApiResources(Config.ApiResources)
            .AddInMemoryApiScopes(Config.ApiScopes)
            .AddInMemoryClients(Config.GetClients(builder.Configuration))
            .AddAspNetIdentity<ApplicationUser>()
            .AddProfileService<ApplicationProfileService>();

        builder.Services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
        });

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddHealthChecks();

        return builder.Build();
    }

    private static IIdentityServerBuilder ConfigureSigningCredential(
        IIdentityServerBuilder identityServerBuilder,
        WebApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment())
        {
            return identityServerBuilder.AddDeveloperSigningCredential(persistKey: true);
        }

        var signingCredentialOptions = builder.Configuration
            .GetSection(SigningCredentialOptions.SectionName)
            .Get<SigningCredentialOptions>() ?? new SigningCredentialOptions();

        if (string.IsNullOrWhiteSpace(signingCredentialOptions.CertificatePath))
        {
            throw new InvalidOperationException(
                "IdentityServer signing certificate is required outside Development. " +
                "Set IdentityServer:SigningCredential:CertificatePath.");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(
            signingCredentialOptions.CertificatePath,
            signingCredentialOptions.CertificatePassword);

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException(
                "IdentityServer signing certificate must include a private key.");
        }

        return identityServerBuilder.AddSigningCredential(certificate);
    }

    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseForwardedHeaders();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseIdentityServer();
        app.UseAuthorization();

        app.MapHealthChecks("/health");

        app.MapRazorPages()
            .RequireAuthorization();

        return app;
    }
}
