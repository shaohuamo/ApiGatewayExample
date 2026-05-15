using System.Transactions;
using Azure.Messaging.ServiceBus;
using CommonService.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ProductUpdatesReprocessor.Functions.Services;

public class ProductUpdatesDlqReprocessor
{
    private readonly ServiceBusOptions _serviceBusOptions;
    private readonly ILogger<ProductUpdatesDlqReprocessor> _logger;

    public ProductUpdatesDlqReprocessor(
        IOptions<ServiceBusOptions> serviceBusOptions,
        ILogger<ProductUpdatesDlqReprocessor> logger)
    {
        _serviceBusOptions = serviceBusOptions.Value;
        _logger = logger;
    }

    public async Task ReprocessAsync(
        ServiceBusReceivedMessage deadLetterMessage,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadLetterMessage);
        ArgumentNullException.ThrowIfNull(messageActions);

        ValidateOptions();

        await using var client = CreateClient();
        await using var sender = client.CreateSender(_serviceBusOptions.ProductsUpdatesReprocessTopic);

        ServiceBusMessage reprocessMessage = CreateReprocessMessage(deadLetterMessage);

        using var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

        await sender.SendMessageAsync(reprocessMessage, cancellationToken);
        await messageActions.CompleteMessageAsync(deadLetterMessage, cancellationToken);

        transactionScope.Complete();

        _logger.LogInformation(
            "Reprocessed dead-letter message {OriginalMessageId} to topic {ReprocessTopic}",
            deadLetterMessage.MessageId,
            _serviceBusOptions.ProductsUpdatesReprocessTopic);
    }

    private ServiceBusClient CreateClient()
    {
        return new ServiceBusClient(
            _serviceBusOptions.ConnectionString,
            new ServiceBusClientOptions
            {
                EnableCrossEntityTransactions = true
            });
    }

    private ServiceBusMessage CreateReprocessMessage(ServiceBusReceivedMessage deadLetterMessage)
    {
        string reprocessMessageId = GetReprocessMessageId(deadLetterMessage);

        var message = new ServiceBusMessage(deadLetterMessage.Body)
        {
            ContentType = deadLetterMessage.ContentType,
            CorrelationId = deadLetterMessage.CorrelationId,
            MessageId = reprocessMessageId,
            ReplyTo = deadLetterMessage.ReplyTo,
            ReplyToSessionId = deadLetterMessage.ReplyToSessionId,
            SessionId = deadLetterMessage.SessionId,
            Subject = deadLetterMessage.Subject,
            To = deadLetterMessage.To
        };

        foreach ((string key, object? value) in deadLetterMessage.ApplicationProperties)
        {
            message.ApplicationProperties[key] = value;
        }

        message.ApplicationProperties["OriginalMessageId"] = deadLetterMessage.MessageId;
        message.ApplicationProperties["DeadLetterReason"] = deadLetterMessage.DeadLetterReason ?? string.Empty;
        message.ApplicationProperties["DeadLetterErrorDescription"] =
            deadLetterMessage.DeadLetterErrorDescription ?? string.Empty;

        return message;
    }

    private static string GetReprocessMessageId(ServiceBusReceivedMessage deadLetterMessage)
    {
        if (string.IsNullOrWhiteSpace(deadLetterMessage.MessageId))
        {
            throw new InvalidOperationException("Dead-letter message is missing MessageId.");
        }

        return deadLetterMessage.MessageId;
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

        if (string.IsNullOrWhiteSpace(_serviceBusOptions.ProductsUpdatesSubscription))
        {
            throw new InvalidOperationException("ServiceBus:ProductsUpdatesSubscription must be configured.");
        }

        if (string.IsNullOrWhiteSpace(_serviceBusOptions.ProductsUpdatesReprocessTopic))
        {
            throw new InvalidOperationException(
                "ServiceBus:ProductsUpdatesReprocessTopic must be configured.");
        }
    }
}