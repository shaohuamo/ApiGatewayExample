using Duende.IdentityServer.Events;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer;
using IdentityServer.Models;
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
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IEventService _events;

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public Index(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IIdentityServerInteractionService interaction,
        IEventService events)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _interaction = interaction;
        _events = events;
    }

    public IActionResult OnGet(string? returnUrl)
    {
        Input = new InputModel
        {
            ReturnUrl = returnUrl
        };

        return Page();
    }

    public async Task<IActionResult> OnPost()
    {
        var context = await _interaction.GetAuthorizationContextAsync(Input.ReturnUrl);

        if (!ModelState.IsValid)
        {
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

        await _events.RaiseAsync(new UserLoginSuccessEvent(user.UserName, user.Id, user.UserName, clientId: context?.Client.ClientId));
        Telemetry.Metrics.UserRegister(context?.Client.ClientId);
        Telemetry.Metrics.UserLogin(context?.Client.ClientId, IdentityServerConstants.LocalIdentityProvider);
        await _signInManager.SignInAsync(user, isPersistent: false);

        if (context != null)
        {
            ArgumentNullException.ThrowIfNull(Input.ReturnUrl, nameof(Input.ReturnUrl));

            if (context.IsNativeClient())
            {
                return this.LoadingPage(Input.ReturnUrl);
            }

            return Redirect(Input.ReturnUrl);
        }

        if (Url.IsLocalUrl(Input.ReturnUrl))
        {
            return Redirect(Input.ReturnUrl);
        }

        return Redirect("~/");
    }
}
