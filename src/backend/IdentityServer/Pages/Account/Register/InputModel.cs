using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Pages.Account.Register;

public class InputModel
{
    [Required]
    [Display(Name = "Username")]
    public string? Username { get; set; }

    [Required]
    [EmailAddress]
    [PageRemote(
        PageName = "/Account/Register/Index",
        PageHandler = "IsEmailAvailable",
        HttpMethod = "GET",
        ErrorMessage = "Email is already registered.")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "The password must be at least 12 and at most 128 characters long.")]
    [RegularExpression(
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).+$",
        ErrorMessage = "The password must include uppercase, lowercase, digit, and non-alphanumeric characters.")]
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
