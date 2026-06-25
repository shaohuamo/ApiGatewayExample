using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Pages.Account.ResendConfirmation;

public sealed class InputModel
{
    [Required(ErrorMessage = "The {0} field is required.")]
    [EmailAddress(ErrorMessage = "The {0} field is not a valid email address.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    public string? ReturnUrl { get; set; }
}
