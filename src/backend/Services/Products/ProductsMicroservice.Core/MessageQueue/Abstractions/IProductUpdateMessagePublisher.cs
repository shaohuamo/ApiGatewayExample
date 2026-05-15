using ProductsMicroservice.Core.MessageQueue.Messages;

namespace ProductsMicroservice.Core.MessageQueue.Abstractions;

public interface IProductUpdateMessagePublisher
{
    Task PublishAsync(ProductUpdatedMessage message, CancellationToken cancellationToken = default);
}