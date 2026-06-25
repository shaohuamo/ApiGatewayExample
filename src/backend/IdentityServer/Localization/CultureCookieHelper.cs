using Microsoft.AspNetCore.Localization;

namespace IdentityServer.Localization;

public static class CultureCookieHelper
{
    public const string CookieName = "MicroservicesDemo.Culture";
    public static readonly string[] SupportedCultures = ["en", "zh-CN"];

    public static string? GetCookieDomain(HttpRequest request)
    {
        var configuredDomain = request.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["CultureCookie:Domain"];

        if (!string.IsNullOrWhiteSpace(configuredDomain))
        {
            return configuredDomain;
        }

        var host = request.Host.Host;
        return host.Equals("250669.xyz", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".250669.xyz", StringComparison.OrdinalIgnoreCase)
            ? ".250669.xyz"
            : null;
    }

    public static CookieOptions CreateCookieOptions(HttpRequest request)
    {
        var options = new CookieOptions
        {
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = !request.HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsDevelopment(),
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
        };

        var domain = GetCookieDomain(request);
        if (!string.IsNullOrWhiteSpace(domain))
        {
            options.Domain = domain;
        }

        return options;
    }

    public static bool IsSupportedCulture(string? culture) =>
        SupportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase);

    public static string FormatCookieValue(string culture) =>
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture, culture));
}
