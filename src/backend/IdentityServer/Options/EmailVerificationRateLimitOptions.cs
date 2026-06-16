namespace IdentityServer.Options;

public sealed class EmailVerificationRateLimitOptions
{
    public const string SectionName = "EmailVerificationRateLimit";

    public bool Enabled { get; set; } = true;
    public bool FailOpenOnRedisError { get; set; } = true;

    public int RegisterIpLimit { get; set; } = 5;
    public int RegisterIpWindowMinutes { get; set; } = 10;
    public int RegisterIpDailyLimit { get; set; } = 20;
    public int RegisterIpDailyWindowHours { get; set; } = 24;

    public int ResendIpLimit { get; set; } = 3;
    public int ResendIpWindowMinutes { get; set; } = 10;
    public int ResendEmailCooldownMinutes { get; set; } = 5;
    public int ResendEmailDailyLimit { get; set; } = 5;
    public int ResendEmailDailyWindowHours { get; set; } = 24;
}
