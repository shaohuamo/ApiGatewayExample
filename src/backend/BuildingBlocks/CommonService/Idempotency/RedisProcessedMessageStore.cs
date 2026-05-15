using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CommonService.Idempotency;

public class RedisProcessedMessageStore : IProcessedMessageStore
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisProcessedMessageStore> _logger;
    private readonly MessageIdempotencyOptions _options;

    public RedisProcessedMessageStore(
        IDistributedCache cache,
        IOptions<MessageIdempotencyOptions> options,
        ILogger<RedisProcessedMessageStore> logger)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<bool> HasProcessedAsync(
        string consumerName,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildCacheKey(consumerName, messageId);
        string? cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);

        return !string.IsNullOrWhiteSpace(cachedValue);
    }

    public async Task MarkProcessedAsync(
        string consumerName,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        string cacheKey = BuildCacheKey(consumerName, messageId);
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(
                Math.Max(_options.ProcessedMessageTtlMinutes, 1))
        };

        await _cache.SetStringAsync(cacheKey, "1", cacheOptions, cancellationToken);

        _logger.LogDebug(
            "Marked message {MessageId} as processed for consumer {ConsumerName}",
            messageId,
            consumerName);
    }

    private string BuildCacheKey(string consumerName, string messageId)
    {
        if (string.IsNullOrWhiteSpace(consumerName))
        {
            throw new ArgumentException("Consumer name must be provided.", nameof(consumerName));
        }

        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("MessageId must be provided.", nameof(messageId));
        }

        return $"{_options.KeyPrefix}:{consumerName}:{messageId}";
    }
}