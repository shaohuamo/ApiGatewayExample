using IdentityServer.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServer.Pages.Culture;

[AllowAnonymous]
[SecurityHeaders]
public sealed class SetModel : PageModel
{
    public IActionResult OnGet(string? culture, string? returnUrl)
    {
        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "~/";

        if (!CultureCookieHelper.IsSupportedCulture(culture))
        {
            return LocalRedirect(safeReturnUrl);
        }

        var selectedCulture = culture!;
        Response.Cookies.Append(
            CultureCookieHelper.CookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(selectedCulture, selectedCulture)),
            CultureCookieHelper.CreateCookieOptions(Request));

        return LocalRedirect(safeReturnUrl);
    }
}
