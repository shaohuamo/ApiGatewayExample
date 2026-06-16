using IdentityServer.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Services;

public sealed class EmailConfirmationService(
    UserManager<ApplicationUser> userManager,
    EmailConfirmationLinkFactory linkFactory,
    IIdentityEmailSender emailSender) : IEmailConfirmationService
{
    public async Task SendConfirmationEmailAsync(
        ApplicationUser user,
        string? returnUrl,
        IUrlHelper url,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            throw new InvalidOperationException("Cannot send an email confirmation message without a user email address.");
        }

        var confirmationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = linkFactory.CreateEmailConfirmationLink(url, user, confirmationToken, returnUrl);

        await emailSender.SendEmailConfirmationAsync(user.Email, confirmationLink, cancellationToken);
    }
}
