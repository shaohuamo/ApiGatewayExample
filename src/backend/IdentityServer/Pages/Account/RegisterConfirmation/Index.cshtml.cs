using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServer.Pages.Account.RegisterConfirmation;

[SecurityHeaders]
[AllowAnonymous]
public class Index : PageModel
{
    public string? Email { get; private set; }

    public string? ReturnUrl { get; private set; }

    public void OnGet(string? email, string? returnUrl)
    {
        Email = email;
        ReturnUrl = returnUrl;
    }
}
