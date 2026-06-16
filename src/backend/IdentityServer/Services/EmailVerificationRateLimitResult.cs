namespace IdentityServer.Services;

public sealed record EmailVerificationRateLimitResult(bool IsAllowed, string? Reason = null)
{
    public static EmailVerificationRateLimitResult Allowed { get; } = new(true);

    public static EmailVerificationRateLimitResult Limited(string reason)
    {
        return new EmailVerificationRateLimitResult(false, reason);
    }
}
