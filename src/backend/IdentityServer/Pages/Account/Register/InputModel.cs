using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Pages.Account.Register;

public class InputModel
{
    [Required]
    [Display(Name = "Username")]
    public string? Username { get; set; }

    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    public string? ConfirmPassword { get; set; }

    public string? ReturnUrl { get; set; }
}
