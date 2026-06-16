using IdentityServer.Models;
using IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServer.Pages.Account.ConfirmEmail;

[SecurityHeaders]
[AllowAnonymous]
public class Index(UserManager<ApplicationUser> userManager) : PageModel
{
    public ViewModel View { get; private set; } = new();

    public async Task OnGet(string? userId, string? code, string? returnUrl, string? contextId = null)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
        {
            View.ReturnUrl = returnUrl;
            SetFailure("The email confirmation link is invalid.");
            return;
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            View.ReturnUrl = returnUrl;
            SetFailure("The email confirmation link is invalid.");
            return;
        }

        View.Email = user.Email;
        View.ReturnUrl = await ResolveReturnUrlAsync(user, returnUrl, contextId);

        if (await userManager.IsEmailConfirmedAsync(user))
        {
            View.IsSuccess = true;
            View.Message = "Your email address is already confirmed. You can sign in now.";
            return;
        }

        string decodedCode;
        try
        {
            decodedCode = EmailConfirmationLinkFactory.DecodeToken(code);
        }
        catch (FormatException)
        {
            SetFailure("The email confirmation link is invalid.");
            return;
        }

        var result = await userManager.ConfirmEmailAsync(user, decodedCode);
        if (result.Succeeded)
        {
            await RemoveReturnUrlContextAsync(user, contextId);
            View.IsSuccess = true;
            View.Message = "Your email address has been confirmed. You can sign in now.";
            return;
        }

        SetFailure("The email confirmation link is invalid or has expired.");
    }

    private void SetFailure(string message)
    {
        View.IsSuccess = false;
        View.Message = message;
    }

    private async Task<string?> ResolveReturnUrlAsync(
        ApplicationUser user,
        string? returnUrl,
        string? contextId)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) || string.IsNullOrWhiteSpace(contextId))
        {
            return returnUrl;
        }

        var tokenValue = await userManager.GetAuthenticationTokenAsync(
            user,
            EmailConfirmationReturnUrlContext.LoginProvider,
            EmailConfirmationReturnUrlContext.BuildTokenName(contextId));

        if (!EmailConfirmationReturnUrlContext.TryDeserialize(tokenValue, out var storedReturnUrl))
        {
            await RemoveReturnUrlContextAsync(user, contextId);
            return null;
        }

        return storedReturnUrl;
    }

    private Task<IdentityResult> RemoveReturnUrlContextAsync(ApplicationUser user, string? contextId)
    {
        if (string.IsNullOrWhiteSpace(contextId))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        return userManager.RemoveAuthenticationTokenAsync(
            user,
            EmailConfirmationReturnUrlContext.LoginProvider,
            EmailConfirmationReturnUrlContext.BuildTokenName(contextId));
    }
}
