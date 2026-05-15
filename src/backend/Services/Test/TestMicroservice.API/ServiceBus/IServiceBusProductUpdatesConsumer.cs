namespace TestMicroservice.API.ServiceBus;

public interface IServiceBusProductUpdatesConsumer : IAsyncDisposable
{
    Task ConsumeAsync();
}