using System.Diagnostics;

namespace CommonService.ServiceBus;

public static class ServiceBusTelemetry
{
    public static readonly ActivitySource ActivitySource = new("AzureServiceBus");
}
