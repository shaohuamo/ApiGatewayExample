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
        var returnUrlContextId = await StoreReturnUrlAsync(user, returnUrl);
        var confirmationLink = linkFactory.CreateEmailConfirmationLink(
            url,
            user,
            confirmationToken,
            returnUrlContextId);

        await emailSender.SendEmailConfirmationAsync(user.Email, confirmationLink, cancellationToken);
    }

    private async Task<string?> StoreReturnUrlAsync(ApplicationUser user, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return null;
        }

        var contextId = EmailConfirmationReturnUrlContext.CreateContextId();
        var result = await userManager.SetAuthenticationTokenAsync(
            user,
            EmailConfirmationReturnUrlContext.LoginProvider,
            EmailConfirmationReturnUrlContext.BuildTokenName(contextId),
            EmailConfirmationReturnUrlContext.Serialize(returnUrl));

        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Could not store the email confirmation return URL context.");
        }

        return contextId;
    }
}
