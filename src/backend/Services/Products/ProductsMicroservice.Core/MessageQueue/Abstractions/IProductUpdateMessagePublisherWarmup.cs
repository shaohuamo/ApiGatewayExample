namespace ProductsMicroservice.Core.MessageQueue.Abstractions;

public interface IProductUpdateMessagePublisherWarmup
{
    Task WarmupAsync(CancellationToken cancellationToken = default);
}
