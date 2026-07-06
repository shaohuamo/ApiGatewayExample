using CommonService.Health;

namespace TestMicroservice.API.RabbitMQ
{
    public class RabbitMQProductAddHostedService : BackgroundService
    {
        private readonly IRabbitMQProductAddConsumer _productAddConsumer;
        private readonly IStartupReadinessState _readinessState;
        private readonly ILogger<RabbitMQProductAddHostedService> _logger;

        public RabbitMQProductAddHostedService(
            IRabbitMQProductAddConsumer productAddConsumer,
            IStartupReadinessState readinessState,
            ILogger<RabbitMQProductAddHostedService> logger)
        {
            _productAddConsumer = productAddConsumer;
            _readinessState = readinessState;
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
                    await _productAddConsumer.ConsumeAsync();
                    _readinessState.MarkReady("RabbitMQ consumer started.");
                    _logger.LogInformation("RabbitMQ product add consumer started.");

                    await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _readinessState.MarkNotReady("RabbitMQ consumer is still retrying.");
                    await _productAddConsumer.DisposeAsync();

                    var delay = CalculateRetryDelay(attempt);
                    _logger.LogWarning(ex,
                        "RabbitMQ product add consumer start attempt {Attempt} failed. Retrying in {DelaySeconds:n1}s.",
                        attempt,
                        delay.TotalSeconds);
                    await Task.Delay(delay, stoppingToken);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await base.StopAsync(cancellationToken);
            await _productAddConsumer.DisposeAsync();
        }

        private static TimeSpan CalculateRetryDelay(int attempt)
        {
            var exponentialSeconds = Math.Min(Math.Pow(2, Math.Max(attempt - 1, 0)), 30);
            var jitterSeconds = Random.Shared.NextDouble();

            return TimeSpan.FromSeconds(exponentialSeconds + jitterSeconds);
        }
    }
}
