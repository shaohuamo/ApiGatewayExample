namespace IdentityServer.Pages.Account.ConfirmEmail;

public sealed class ViewModel
{
    public bool IsSuccess { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? ReturnUrl { get; set; }
}
