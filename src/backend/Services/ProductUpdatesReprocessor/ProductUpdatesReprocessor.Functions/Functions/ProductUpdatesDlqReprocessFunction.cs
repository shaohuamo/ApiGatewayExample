using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using ProductUpdatesReprocessor.Functions.Services;

namespace ProductUpdatesReprocessor.Functions.Functions;

public class ProductUpdatesDlqReprocessFunction
{
    private readonly ProductUpdatesDlqReprocessor _reprocessor;
    private readonly ILogger<ProductUpdatesDlqReprocessFunction> _logger;

    public ProductUpdatesDlqReprocessFunction(
        ProductUpdatesDlqReprocessor reprocessor,
        ILogger<ProductUpdatesDlqReprocessFunction> logger)
    {
        _reprocessor = reprocessor;
        _logger = logger;
    }

    [Function("ProductUpdatesDlqReprocessFunction")]
    public async Task RunAsync(
        [ServiceBusTrigger(
            "%ProductsUpdatesDeadLetterPath%",
            Connection = "ServiceBusConnectionString",
            AutoCompleteMessages = false)]
        ServiceBusReceivedMessage deadLetterMessage,
        ServiceBusMessageActions messageActions,
        CancellationToken cancellationToken)
    {
        await _reprocessor.ReprocessAsync(deadLetterMessage, messageActions, cancellationToken);

        _logger.LogInformation(
            "Dead-letter reprocess cycle completed for message {MessageId}",
            deadLetterMessage.MessageId);
    }
}
