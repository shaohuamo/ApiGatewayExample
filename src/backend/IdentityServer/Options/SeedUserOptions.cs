namespace IdentityServer.Options;

public sealed class SeedUserOptions
{
    public const string SectionName = "SeedUser";

    public bool Enabled { get; set; }
    public string UserName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Password { get; set; } = null!;
}
