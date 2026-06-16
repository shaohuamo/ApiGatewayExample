using Microsoft.AspNetCore.Http;

namespace IdentityServer.Services;

public interface IEmailVerificationRateLimiter
{
    Task<EmailVerificationRateLimitResult> CheckRegisterAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default);

    Task<EmailVerificationRateLimitResult> CheckResendAsync(
        HttpContext httpContext,
        string email,
        CancellationToken cancellationToken = default);
}
