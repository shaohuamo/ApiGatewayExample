namespace IdentityServer.Options;

public sealed class PostgresOptions
{
    public const string SectionName = "POSTGRES";

    public string Host { get; set; } = null!;
    public string Port { get; set; } = null!;
    public string Database { get; set; } = null!;
    public string User { get; set; } = null!;
    public string Password { get; set; } = null!;
    public int MaxRetryCount { get; set; }
    public int MaxRetryDelaySeconds { get; set; }
}
