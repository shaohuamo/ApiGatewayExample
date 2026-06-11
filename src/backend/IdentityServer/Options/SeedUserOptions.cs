namespace IdentityServer.Options;

public sealed class SeedUserOptions
{
    public const string SectionName = "SeedUser";

    public bool Enabled { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
