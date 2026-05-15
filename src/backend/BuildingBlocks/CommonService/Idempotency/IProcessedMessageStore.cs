namespace CommonService.Idempotency;

public interface IProcessedMessageStore
{
    Task<bool> HasProcessedAsync(
        string consumerName,
        string messageId,
        CancellationToken cancellationToken = default);

    Task MarkProcessedAsync(
        string consumerName,
        string messageId,
        CancellationToken cancellationToken = default);
}