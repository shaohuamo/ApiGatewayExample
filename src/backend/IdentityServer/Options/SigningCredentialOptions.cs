namespace IdentityServer.Options;

public sealed class SigningCredentialOptions
{
    public const string SectionName = "IdentityServer:SigningCredential";

    public string? CertificatePath { get; init; }

    public string? CertificatePassword { get; init; }
}
