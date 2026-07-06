namespace TestMicroservice.API.ServiceBus;

public class ServiceBusProductUpdatesHostedService : BackgroundService
{
    private readonly IServiceBusProductUpdatesConsumer _consumer;
    private readonly ILogger<ServiceBusProductUpdatesHostedService> _logger;

    public ServiceBusProductUpdatesHostedService(
        IServiceBusProductUpdatesConsumer consumer,
        ILogger<ServiceBusProductUpdatesHostedService> logger)
    {
        _consumer = consumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                attempt++;
                await _consumer.ConsumeAsync();
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                await _consumer.DisposeAsync();

                var delay = CalculateRetryDelay(attempt);
                _logger.LogWarning(ex,
                    "Service Bus product updates consumer start attempt {Attempt} failed. Retrying in {DelaySeconds:n1}s.",
                    attempt,
                    delay.TotalSeconds);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await _consumer.DisposeAsync();
    }

    private static TimeSpan CalculateRetryDelay(int attempt)
    {
        var exponentialSeconds = Math.Min(Math.Pow(2, Math.Max(attempt - 1, 0)), 30);
        var jitterSeconds = Random.Shared.NextDouble();

        return TimeSpan.FromSeconds(exponentialSeconds + jitterSeconds);
    }
}
