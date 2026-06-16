using System.Net;
using System.Security.Cryptography;
using System.Text;
using IdentityServer.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace IdentityServer.Services;

public sealed class RedisEmailVerificationRateLimiter : IEmailVerificationRateLimiter
{
    private readonly IOptions<EmailVerificationRateLimitOptions> _rateLimitOptions;
    private readonly IOptions<RedisOptions> _redisOptions;
    private readonly ILogger<RedisEmailVerificationRateLimiter> _logger;
    private readonly Lazy<Task<IConnectionMultiplexer>> _connection;

    public RedisEmailVerificationRateLimiter(
        IOptions<EmailVerificationRateLimitOptions> rateLimitOptions,
        IOptions<RedisOptions> redisOptions,
        ILogger<RedisEmailVerificationRateLimiter> logger)
    {
        _rateLimitOptions = rateLimitOptions;
        _redisOptions = redisOptions;
        _logger = logger;
        _connection = new Lazy<Task<IConnectionMultiplexer>>(ConnectAsync);
    }

    public Task<EmailVerificationRateLimitResult> CheckRegisterAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        return CheckAsync(async database =>
        {
            var options = _rateLimitOptions.Value;
            var ipHash = Hash(GetClientIp(httpContext));

            var shortWindowAllowed = await IncrementAndCheckAsync(
                database,
                BuildKey("register", "ip", ipHash, "window"),
                options.RegisterIpLimit,
                TimeSpan.FromMinutes(options.RegisterIpWindowMinutes));

            if (!shortWindowAllowed)
            {
                return EmailVerificationRateLimitResult.Limited("register_ip_window");
            }

            var dailyAllowed = await IncrementAndCheckAsync(
                database,
                BuildKey("register", "ip", ipHash, "daily"),
                options.RegisterIpDailyLimit,
                TimeSpan.FromHours(options.RegisterIpDailyWindowHours));

            return dailyAllowed
                ? EmailVerificationRateLimitResult.Allowed
                : EmailVerificationRateLimitResult.Limited("register_ip_daily");
        }, cancellationToken);
    }

    public Task<EmailVerificationRateLimitResult> CheckResendAsync(
        HttpContext httpContext,
        string email,
        CancellationToken cancellationToken = default)
    {
        return CheckAsync(async database =>
        {
            var options = _rateLimitOptions.Value;
            var ipHash = Hash(GetClientIp(httpContext));
            var emailHash = Hash(email.Trim().ToUpperInvariant());

            var ipAllowed = await IncrementAndCheckAsync(
                database,
                BuildKey("resend", "ip", ipHash, "window"),
                options.ResendIpLimit,
                TimeSpan.FromMinutes(options.ResendIpWindowMinutes));

            if (!ipAllowed)
            {
                return EmailVerificationRateLimitResult.Limited("resend_ip_window");
            }

            var cooldownAllowed = await SetCooldownAsync(
                database,
                BuildKey("resend", "email", emailHash, "cooldown"),
                TimeSpan.FromMinutes(options.ResendEmailCooldownMinutes));

            if (!cooldownAllowed)
            {
                return EmailVerificationRateLimitResult.Limited("resend_email_cooldown");
            }

            var dailyAllowed = await IncrementAndCheckAsync(
                database,
                BuildKey("resend", "email", emailHash, "daily"),
                options.ResendEmailDailyLimit,
                TimeSpan.FromHours(options.ResendEmailDailyWindowHours));

            return dailyAllowed
                ? EmailVerificationRateLimitResult.Allowed
                : EmailVerificationRateLimitResult.Limited("resend_email_daily");
        }, cancellationToken);
    }

    private async Task<EmailVerificationRateLimitResult> CheckAsync(
        Func<IDatabase, Task<EmailVerificationRateLimitResult>> check,
        CancellationToken cancellationToken)
    {
        var options = _rateLimitOptions.Value;
        if (!options.Enabled)
        {
            return EmailVerificationRateLimitResult.Allowed;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var connection = await _connection.Value;
            var database = connection.GetDatabase();
            return await check(database);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (options.FailOpenOnRedisError)
        {
            _logger.LogError(
                ex,
                "Email verification rate limiting failed; allowing request because fail-open is enabled.");
            return EmailVerificationRateLimitResult.Allowed;
        }
    }

    private async Task<IConnectionMultiplexer> ConnectAsync()
    {
        var redisOptions = _redisOptions.Value;
        var configuration = ConfigurationOptions.Parse(redisOptions.ConnectionString);
        configuration.ConnectRetry = redisOptions.ConnectRetry;
        configuration.ConnectTimeout = redisOptions.ConnectTimeout;
        configuration.SyncTimeout = redisOptions.SyncTimeout;
        configuration.AbortOnConnectFail = redisOptions.AbortOnConnectFail;
        configuration.ReconnectRetryPolicy = new ExponentialRetry(
            redisOptions.InitialReconnectDelay,
            redisOptions.MaxReconnectDelay);

        return await ConnectionMultiplexer.ConnectAsync(configuration);
    }

    private async Task<bool> IncrementAndCheckAsync(
        IDatabase database,
        string key,
        int limit,
        TimeSpan window)
    {
        var count = await database.StringIncrementAsync(key);
        if (count == 1)
        {
            await database.KeyExpireAsync(key, window);
        }

        return count <= limit;
    }

    private static Task<bool> SetCooldownAsync(IDatabase database, string key, TimeSpan expiry)
    {
        return database.StringSetAsync(key, "1", expiry, When.NotExists);
    }

    private string BuildKey(params string[] parts)
    {
        var prefix = _redisOptions.Value.InstanceName;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = "IdentityServer_";
        }

        return $"{prefix}email-confirmation:{string.Join(':', parts)}";
    }

    private static string GetClientIp(HttpContext httpContext)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return "unknown";
        }

        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        return remoteIp.Equals(IPAddress.IPv6Loopback)
            ? IPAddress.Loopback.ToString()
            : remoteIp.ToString();
    }

    private static string Hash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
