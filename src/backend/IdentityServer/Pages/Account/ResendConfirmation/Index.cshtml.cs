using IdentityServer.Models;
using IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServer.Pages.Account.ResendConfirmation;

[SecurityHeaders]
[AllowAnonymous]
public class Index(
    UserManager<ApplicationUser> userManager,
    IEmailConfirmationService emailConfirmationService,
    IEmailVerificationRateLimiter emailVerificationRateLimiter,
    ILogger<Index> logger) : PageModel
{
    public bool EmailSent { get; private set; }

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public void OnGet(string? email, string? returnUrl)
    {
        Input = new InputModel
        {
            Email = email,
            ReturnUrl = returnUrl
        };
    }

    public async Task<IActionResult> OnPost()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(Input.Email);

        var rateLimitResult = await emailVerificationRateLimiter.CheckResendAsync(
            HttpContext,
            Input.Email,
            HttpContext.RequestAborted);
        if (!rateLimitResult.IsAllowed)
        {
            logger.LogWarning(
                "Resend confirmation request was rate limited. Reason: {RateLimitReason}.",
                rateLimitResult.Reason);
            EmailSent = true;
            return Page();
        }

        var user = await userManager.FindByEmailAsync(Input.Email);
        if (user != null && !await userManager.IsEmailConfirmedAsync(user))
        {
            try
            {
                await emailConfirmationService.SendConfirmationEmailAsync(
                    user,
                    Input.ReturnUrl,
                    Url,
                    HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to resend email confirmation for user {UserId}.", user.Id);
            }
        }

        EmailSent = true;
        return Page();
    }
}
