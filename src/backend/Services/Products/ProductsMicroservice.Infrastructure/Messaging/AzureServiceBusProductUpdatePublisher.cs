using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using CommonService.ServiceBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using ProductsMicroservice.Core.MessageQueue.Abstractions;
using ProductsMicroservice.Core.MessageQueue.Messages;

namespace ProductsMicroservice.Infrastructure.Messaging;

public class AzureServiceBusProductUpdatePublisher :
    IProductUpdateMessagePublisher,
    IProductUpdateMessagePublisherWarmup,
    IAsyncDisposable
{
    private readonly ServiceBusOptions _serviceBusOptions;
    private readonly ILogger<AzureServiceBusProductUpdatePublisher> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;

    public AzureServiceBusProductUpdatePublisher(
        IOptions<ServiceBusOptions> serviceBusOptions,
        ILogger<AzureServiceBusProductUpdatePublisher> logger)
    {
        _serviceBusOptions = serviceBusOptions.Value;
        _logger = logger;

        ValidateOptions();

        _client = new ServiceBusClient(_serviceBusOptions.ConnectionString);
        _sender = _client.CreateSender(_serviceBusOptions.ProductsUpdatesTopic);
    }

    public async Task PublishAsync(ProductUpdatedMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        string payload = JsonSerializer.Serialize(message);
        string messageId = $"product.update:{message.ProductId:N}:{message.Version}";

        var serviceBusMessage = new ServiceBusMessage(BinaryData.FromString(payload))
        {
            ContentType = "application/json",
            Subject = "product.update",
            MessageId = messageId
        };

        serviceBusMessage.ApplicationProperties["event"] = "product.update";
        serviceBusMessage.ApplicationProperties["version"] = message.Version;
        serviceBusMessage.ApplicationProperties["RowCount"] = 1;

        using Activity? activity = ServiceBusTelemetry.ActivitySource.StartActivity(
            "servicebus.publish",
            ActivityKind.Producer);

        activity?.SetTag("messaging.system", "azure_service_bus");
        activity?.SetTag("messaging.destination", _serviceBusOptions.ProductsUpdatesTopic);
        activity?.SetTag("messaging.destination_kind", "topic");
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("messaging.message_id", messageId);
        activity?.SetTag("messaging.servicebus.subject", serviceBusMessage.Subject);
        activity?.SetTag("product.id", message.ProductId);
        activity?.SetTag("product.version", message.Version);

        var userId = Baggage.GetBaggage("user_id");
        if (!string.IsNullOrWhiteSpace(userId))
        {
            activity?.SetTag("messaging.user_id", userId);
            serviceBusMessage.ApplicationProperties["user_id"] = userId;
        }

        Propagators.DefaultTextMapPropagator.Inject(
            new PropagationContext(activity?.Context ?? default, Baggage.Current),
            serviceBusMessage.ApplicationProperties,
            (properties, key, value) => properties[key] = value);

        try
        {
            _logger.LogInformation("Publishing product update event to Service Bus topic {Topic}",
                _serviceBusOptions.ProductsUpdatesTopic);

            await _sender.SendMessageAsync(serviceBusMessage, cancellationToken);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            _logger.LogError(ex, "Failed to publish product update event to Service Bus topic {Topic}",
                _serviceBusOptions.ProductsUpdatesTopic);

            throw;
        }

        _logger.LogInformation("Published product update event to Service Bus topic {Topic}",
            _serviceBusOptions.ProductsUpdatesTopic);
    }

    public async Task WarmupAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Warming up Service Bus sender for topic {Topic}",
            _serviceBusOptions.ProductsUpdatesTopic);

        using ServiceBusMessageBatch messageBatch =
            await _sender.CreateMessageBatchAsync(cancellationToken);

        _logger.LogInformation("Service Bus sender warmup completed for topic {Topic}, MaxSizeInBytes: {MaxSizeInBytes}",
            _serviceBusOptions.ProductsUpdatesTopic,
            messageBatch.MaxSizeInBytes);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(_serviceBusOptions.ConnectionString))
        {
            throw new InvalidOperationException("ServiceBus:ConnectionString must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_serviceBusOptions.ProductsUpdatesTopic))
        {
            throw new InvalidOperationException("ServiceBus:ProductsUpdatesTopic must be configured.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
    }
}
