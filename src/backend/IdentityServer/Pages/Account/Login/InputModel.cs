using System.ComponentModel.DataAnnotations;

namespace IdentityServer.Pages.Account.Login;

public class InputModel
{
    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Username")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "The {0} field is required.")]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    [Display(Name = "Remember My Login")]
    public bool RememberLogin { get; set; }

    public string? ReturnUrl { get; set; }
    public string? Button { get; set; }
}
