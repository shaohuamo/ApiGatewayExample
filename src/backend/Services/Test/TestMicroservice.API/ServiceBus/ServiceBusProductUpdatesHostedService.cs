namespace TestMicroservice.API.ServiceBus;

public class ServiceBusProductUpdatesHostedService : IHostedService
{
    private readonly IServiceBusProductUpdatesConsumer _consumer;

    public ServiceBusProductUpdatesHostedService(IServiceBusProductUpdatesConsumer consumer)
    {
        _consumer = consumer;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _consumer.ConsumeAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _consumer.DisposeAsync();
    }
}