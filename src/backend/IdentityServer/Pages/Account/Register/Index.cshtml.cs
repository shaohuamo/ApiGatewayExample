using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using IdentityServer.Models;
using IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServer.Pages.Account.Register;

[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEmailConfirmationService _emailConfirmationService;
    private readonly IEmailVerificationRateLimiter _emailVerificationRateLimiter;
    private readonly ILogger<Index> _logger;

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public Index(
        UserManager<ApplicationUser> userManager,
        IIdentityServerInteractionService interaction,
        IEmailConfirmationService emailConfirmationService,
        IEmailVerificationRateLimiter emailVerificationRateLimiter,
        ILogger<Index> logger)
    {
        _userManager = userManager;
        _interaction = interaction;
        _emailConfirmationService = emailConfirmationService;
        _emailVerificationRateLimiter = emailVerificationRateLimiter;
        _logger = logger;
    }

    public IActionResult OnGet(string? returnUrl)
    {
        Input = new InputModel
        {
            ReturnUrl = returnUrl
        };

        return Page();
    }

    public async Task<IActionResult> OnGetIsEmailAvailable([FromQuery(Name = "Input.Email")] string? email)
    {
        email ??= Request.Query["Email"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(email))
        {
            return new JsonResult(true);
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        return new JsonResult(user == null ? true : "Email is already registered.");
    }

    public async Task<IActionResult> OnPost()
    {
        var context = await _interaction.GetAuthorizationContextAsync(Input.ReturnUrl);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(Input.Email);

        var rateLimitResult = await _emailVerificationRateLimiter.CheckRegisterAsync(
            HttpContext,
            HttpContext.RequestAborted);
        if (!rateLimitResult.IsAllowed)
        {
            _logger.LogWarning(
                "Register request was rate limited. Reason: {RateLimitReason}.",
                rateLimitResult.Reason);
            ModelState.AddModelError(
                string.Empty,
                "Too many registration attempts. Please try again later.");
            Telemetry.Metrics.UserRegisterFailure(context?.Client.ClientId, "rate_limited");
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.Username,
            Email = Input.Email
        };

        var result = await _userManager.CreateAsync(user, Input.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            Telemetry.Metrics.UserRegisterFailure(context?.Client.ClientId, result.Errors.FirstOrDefault()?.Code ?? "registration_failed");
            return Page();
        }

        try
        {
            await _emailConfirmationService.SendConfirmationEmailAsync(
                user,
                Input.ReturnUrl,
                Url,
                HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email confirmation for user {UserId}.", user.Id);
        }

        Telemetry.Metrics.UserRegister(context?.Client.ClientId);
        return RedirectToPage(
            "/Account/RegisterConfirmation/Index",
            new { email = Input.Email, returnUrl = Input.ReturnUrl });
    }
}
