namespace CommonService.ServiceBus;

public class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string ConnectionString { get; set; } = string.Empty;

    public string ProductsUpdatesTopic { get; set; } = "products.updates";

    public string ProductsUpdatesSubscription { get; set; } = "products.updates.test";

    public string ProductsUpdatesReprocessTopic { get; set; } = "products.updates.Reprocess";
}