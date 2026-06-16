using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Pages.Account.ResendConfirmation;

public sealed class InputModel
{
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    public string? ReturnUrl { get; set; }
}
