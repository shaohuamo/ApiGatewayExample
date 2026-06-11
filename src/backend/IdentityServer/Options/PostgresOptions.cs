namespace IdentityServer.Options;

public sealed class PostgresOptions
{
    public const string SectionName = "POSTGRES";

    public string Host { get; set; } = "localhost";
    public string Port { get; set; } = "5432";
    public string Database { get; set; } = "identitydatabase";
    public string User { get; set; } = "postgres";
    public string Password { get; set; } = "admin";
    public int MaxRetryCount { get; set; } = 5;
    public int MaxRetryDelaySeconds { get; set; } = 10;
}
