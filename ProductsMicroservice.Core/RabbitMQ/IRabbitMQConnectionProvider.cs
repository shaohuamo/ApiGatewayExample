using RabbitMQ.Client;

namespace ProductsMicroservice.Core.RabbitMQ;

public interface IRabbitMQConnectionProvider : IAsyncDisposable
{
    Task<IConnection> GetConnectionAsync();
}