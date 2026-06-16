namespace IdentityServer.Options;

public sealed class ResendEmailOptions
{
    public const string SectionName = "ResendEmail";

    public string? ApiToken { get; set; }

    public string? From { get; set; }

    public string? PublicBaseUrl { get; set; }

    public string ConfirmationSubject { get; set; } = "Confirm your MicroservicesDemo account";
}
