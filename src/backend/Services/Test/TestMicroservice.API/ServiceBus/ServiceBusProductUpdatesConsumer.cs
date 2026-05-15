using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CommonService.Idempotency;
using CommonService.ServiceBus;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using ProductsMicroservice.Core.MessageQueue.Messages;
using TestMicroservice.API.Diagnostics;

namespace TestMicroservice.API.ServiceBus;

public class ServiceBusProductUpdatesConsumer : IServiceBusProductUpdatesConsumer
{
    private const string ConsumerName = "test-servicebus-product-updates";

    private readonly ServiceBusOptions _serviceBusOptions;
    private readonly IProcessedMessageStore _processedMessageStore;
    private readonly ILogger<ServiceBusProductUpdatesConsumer> _logger;
    private ServiceBusClient? _client;
    private ServiceBusProcessor? _processor;

    public ServiceBusProductUpdatesConsumer(
        IOptions<ServiceBusOptions> serviceBusOptions,
        IProcessedMessageStore processedMessageStore,
        ILogger<ServiceBusProductUpdatesConsumer> logger)
    {
        _serviceBusOptions = serviceBusOptions.Value;
        _processedMessageStore = processedMessageStore;
        _logger = logger;
    }

    public async Task ConsumeAsync()
    {
        if (string.IsNullOrWhiteSpace(_serviceBusOptions.ConnectionString) ||
            string.IsNullOrWhiteSpace(_serviceBusOptions.ProductsUpdatesTopic) ||
            string.IsNullOrWhiteSpace(_serviceBusOptions.ProductsUpdatesSubscription))
        {
            _logger.LogWarning(
                "Service Bus consumer is disabled because required configuration is missing.");
            return;
        }

        _client = new ServiceBusClient(_serviceBusOptions.ConnectionString);
        _processor = _client.CreateProcessor(
            _serviceBusOptions.ProductsUpdatesTopic,
            _serviceBusOptions.ProductsUpdatesSubscription,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 1
            });

        _processor.ProcessMessageAsync += ProcessMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync();

        _logger.LogInformation(
            "Started Service Bus processor for {Topic}/{Subscription}",
            _serviceBusOptions.ProductsUpdatesTopic,
            _serviceBusOptions.ProductsUpdatesSubscription);
    }

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
    {
        var stopwatch = Stopwatch.StartNew();

        var parentContext = Propagators.DefaultTextMapPropagator.Extract(
            default,
            args.Message.ApplicationProperties,
            (properties, key) =>
            {
                if (properties.TryGetValue(key, out var value) && value is not null)
                {
                    return new[] { value.ToString()! };
                }

                return Array.Empty<string>();
            });

        Baggage.Current = parentContext.Baggage;

        using var activity = ServiceBusTelemetry.ActivitySource.StartActivity(
            "servicebus.consume",
            ActivityKind.Consumer,
            parentContext.ActivityContext);

        activity?.SetTag("messaging.system", "azure_service_bus");
        activity?.SetTag("messaging.destination", _serviceBusOptions.ProductsUpdatesTopic);
        activity?.SetTag("messaging.destination_kind", "topic");
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.servicebus.subscription", _serviceBusOptions.ProductsUpdatesSubscription);
        activity?.SetTag("messaging.servicebus.sequence_number", args.Message.SequenceNumber);
        activity?.SetTag("messaging.servicebus.delivery_count", args.Message.DeliveryCount);
        activity?.SetTag("messaging.servicebus.subject", args.Message.Subject);

        await ProcessServiceBusMessageAsync(args, stopwatch, activity);
    }

    private async Task ProcessServiceBusMessageAsync(
        ProcessMessageEventArgs args,
        Stopwatch stopwatch,
        Activity? activity)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(args.Message.MessageId))
            {
                stopwatch.Stop();

                _logger.LogWarning(
                    "Service Bus message from {Topic}/{Subscription} is missing MessageId and will be dead-lettered.",
                    _serviceBusOptions.ProductsUpdatesTopic,
                    _serviceBusOptions.ProductsUpdatesSubscription);

                DiagnosticsConfig.ServiceBusConsumeCounter.Add(1,
                    new KeyValuePair<string, object?>("status", "invalid"));

                activity?.SetStatus(ActivityStatusCode.Error, "Missing MessageId");

                await args.DeadLetterMessageAsync(
                    args.Message,
                    "MissingMessageId",
                    "Service Bus message is missing MessageId.",
                    args.CancellationToken);

                return;
            }

            activity?.SetTag("messaging.message_id", args.Message.MessageId);

            if (await _processedMessageStore.HasProcessedAsync(
                    ConsumerName,
                    args.Message.MessageId,
                    args.CancellationToken))
            {
                stopwatch.Stop();

                _logger.LogInformation(
                    "Skipping duplicate Service Bus message {MessageId} from {Topic}/{Subscription}",
                    args.Message.MessageId,
                    _serviceBusOptions.ProductsUpdatesTopic,
                    _serviceBusOptions.ProductsUpdatesSubscription);

                DiagnosticsConfig.ServiceBusConsumeCounter.Add(1,
                    new KeyValuePair<string, object?>("status", "duplicate"));

                activity?.SetTag("messaging.duplicate", true);
                activity?.SetStatus(ActivityStatusCode.Ok);

                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                return;
            }

            string body = args.Message.Body.ToString();
            var productUpdatedMessage = JsonSerializer.Deserialize<ProductUpdatedMessage>(body);

            if (productUpdatedMessage is null)
            {
                stopwatch.Stop();

                _logger.LogWarning(
                    "Invalid Service Bus message received from {Topic}/{Subscription}",
                    _serviceBusOptions.ProductsUpdatesTopic,
                    _serviceBusOptions.ProductsUpdatesSubscription);

                DiagnosticsConfig.ServiceBusConsumeCounter.Add(1,
                    new KeyValuePair<string, object?>("status", "invalid"));

                activity?.SetStatus(ActivityStatusCode.Error, "Invalid message");

                await args.DeadLetterMessageAsync(
                    args.Message,
                    "InvalidMessage",
                    "Service Bus message could not be deserialized.",
                    args.CancellationToken);

                return;
            }

            var userId = Baggage.GetBaggage("user_id");

            activity?.SetTag("product.id", productUpdatedMessage.ProductId);
            activity?.SetTag("product.version", productUpdatedMessage.Version);
            activity?.SetTag("messaging.user_id", userId);

            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["ProductId"] = productUpdatedMessage.ProductId,
                       ["MessageId"] = args.Message.MessageId
                   }))
            {
                _logger.LogInformation(
                    "Received Service Bus message {MessageId} from {Topic}/{Subscription}: {MessageBody}",
                    args.Message.MessageId,
                    _serviceBusOptions.ProductsUpdatesTopic,
                    _serviceBusOptions.ProductsUpdatesSubscription,
                    body);

                stopwatch.Stop();

                DiagnosticsConfig.ServiceBusProcessingHistogram
                    .Record(stopwatch.Elapsed.TotalSeconds);

                DiagnosticsConfig.ServiceBusConsumeCounter.Add(1,
                    new KeyValuePair<string, object?>("status", "success"));

                activity?.SetTag("messaging.process.duration.seconds",
                    stopwatch.Elapsed.TotalSeconds);
            }

            await _processedMessageStore.MarkProcessedAsync(
                ConsumerName,
                args.Message.MessageId,
                args.CancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);

            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Error processing Service Bus message");

            DiagnosticsConfig.ServiceBusConsumeCounter.Add(1,
                new KeyValuePair<string, object?>("status", "error"));

            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);

            return;
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Error processing Service Bus message from {EntityPath}. Error source: {ErrorSource}",
            args.EntityPath,
            args.ErrorSource);

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_processor != null)
        {
            await _processor.StopProcessingAsync();
            await _processor.DisposeAsync();
        }

        if (_client != null)
        {
            await _client.DisposeAsync();
        }
    }
}
