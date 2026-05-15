using CommonService.Idempotency;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace ProductsMicroservice.Tests;

public class RedisProcessedMessageStoreTests
{
    private readonly Mock<IDistributedCache> _cacheMock = new();
    private readonly Mock<ILogger<RedisProcessedMessageStore>> _loggerMock = new();

    [Fact]
    public async Task HasProcessedAsync_ShouldReturnTrue_WhenCacheContainsMessageKey()
    {
        var options = CreateOptions();
        var store = CreateStore(options);

        _cacheMock.Setup(x => x.GetAsync(
                "processed-message:test-consumer:message-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([49]);

        bool result = await store.HasProcessedAsync("test-consumer", "message-1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task MarkProcessedAsync_ShouldStoreMessageKey_WithConfiguredTtl()
    {
        var options = CreateOptions();
        var store = CreateStore(options);
        byte[] expectedValue = [(byte)'1'];

        await store.MarkProcessedAsync("test-consumer", "message-2");

        _cacheMock.Verify(x => x.SetAsync(
            "processed-message:test-consumer:message-2",
            It.Is<byte[]>(value => value.SequenceEqual(expectedValue)),
            It.Is<DistributedCacheEntryOptions>(cacheOptions =>
                cacheOptions.AbsoluteExpirationRelativeToNow ==
                TimeSpan.FromMinutes(options.ProcessedMessageTtlMinutes)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private RedisProcessedMessageStore CreateStore(MessageIdempotencyOptions options)
    {
        return new RedisProcessedMessageStore(
            _cacheMock.Object,
            Options.Create(options),
            _loggerMock.Object);
    }

    private static MessageIdempotencyOptions CreateOptions()
    {
        return new MessageIdempotencyOptions
        {
            KeyPrefix = "processed-message",
            ProcessedMessageTtlMinutes = 60
        };
    }
}