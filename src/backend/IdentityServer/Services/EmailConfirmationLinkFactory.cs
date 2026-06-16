using System.Text;
using IdentityServer.Models;
using IdentityServer.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace IdentityServer.Services;

public sealed class EmailConfirmationLinkFactory(IOptions<ResendEmailOptions> options)
{
    private readonly ResendEmailOptions _options = options.Value;

    public string CreateEmailConfirmationLink(
        IUrlHelper url,
        ApplicationUser user,
        string confirmationToken,
        string? returnUrl)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(user);

        var encodedCode = EncodeToken(confirmationToken);
        var confirmationPath = url.Page(
            "/Account/ConfirmEmail/Index",
            pageHandler: null,
            values: new { userId = user.Id, code = encodedCode, returnUrl },
            protocol: null);

        if (string.IsNullOrWhiteSpace(confirmationPath))
        {
            throw new InvalidOperationException("Could not generate the email confirmation path.");
        }

        var publicBaseUrl = ResolvePublicBaseUrl(url);
        return new Uri(new Uri(EnsureTrailingSlash(publicBaseUrl)), confirmationPath.TrimStart('/')).ToString();
    }

    public static string EncodeToken(string token)
        => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string DecodeToken(string encodedToken)
        => Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));

    private string ResolvePublicBaseUrl(IUrlHelper url)
    {
        if (!string.IsNullOrWhiteSpace(_options.PublicBaseUrl))
        {
            return _options.PublicBaseUrl;
        }

        var request = url.ActionContext.HttpContext.Request;
        return $"{request.Scheme}://{request.Host}{request.PathBase}";
    }

    private static string EnsureTrailingSlash(string value)
        => value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
}
