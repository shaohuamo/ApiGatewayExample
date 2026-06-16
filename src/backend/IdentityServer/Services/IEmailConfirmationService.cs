using IdentityServer.Models;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Services;

public interface IEmailConfirmationService
{
    Task SendConfirmationEmailAsync(
        ApplicationUser user,
        string? returnUrl,
        IUrlHelper url,
        CancellationToken cancellationToken = default);
}
