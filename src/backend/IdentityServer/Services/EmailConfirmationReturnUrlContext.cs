using System.Security.Cryptography;
using System.Text.Json;

namespace IdentityServer.Services;

public static class EmailConfirmationReturnUrlContext
{
    public const string LoginProvider = "MicroservicesDemo.EmailConfirmation";

    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public static string CreateContextId()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();

    public static string BuildTokenName(string contextId)
        => $"return-url:{contextId}";

    public static string Serialize(string returnUrl)
    {
        var payload = new ReturnUrlPayload(returnUrl, DateTimeOffset.UtcNow.Add(Lifetime));
        return JsonSerializer.Serialize(payload);
    }

    public static bool TryDeserialize(string? value, out string? returnUrl)
    {
        returnUrl = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        ReturnUrlPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ReturnUrlPayload>(value);
        }
        catch (JsonException)
        {
            return false;
        }

        if (payload == null ||
            string.IsNullOrWhiteSpace(payload.ReturnUrl) ||
            payload.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        returnUrl = payload.ReturnUrl;
        return true;
    }

    private sealed record ReturnUrlPayload(string ReturnUrl, DateTimeOffset ExpiresUtc);
}
