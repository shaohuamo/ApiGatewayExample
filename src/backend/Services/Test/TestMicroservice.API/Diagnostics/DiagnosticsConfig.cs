using System.Diagnostics.Metrics;

namespace TestMicroservice.API.Diagnostics
{
    public static class DiagnosticsConfig
    {
        public const string ServiceName = "Test.Api";

        public static readonly Meter TestMeter = new(ServiceName);

        /// <summary>
        /// Counts number of messages consumed
        /// tags: status = success | error | invalid
        /// </summary>
        public static readonly Counter<long> RabbitMqConsumeCounter =
            TestMeter.CreateCounter<long>(
                name: "rabbitmq.consume.count",
                unit: "messages",
                description: "Number of RabbitMQ messages consumed");

        /// <summary>
        /// Measures processing duration per message
        /// </summary>
        public static readonly Histogram<double> RabbitMqProcessingHistogram =
            TestMeter.CreateHistogram<double>(
                name: "rabbitmq.processing.duration",
                unit: "seconds",
                description: "Time taken to process RabbitMQ messages");

        /// <summary>
        /// Counts number of Service Bus messages consumed
        /// tags: status = success | error | invalid | duplicate
        /// </summary>
        public static readonly Counter<long> ServiceBusConsumeCounter =
            TestMeter.CreateCounter<long>(
                name: "servicebus.consume.count",
                unit: "messages",
                description: "Number of Service Bus messages consumed");

        /// <summary>
        /// Measures processing duration per Service Bus message
        /// </summary>
        public static readonly Histogram<double> ServiceBusProcessingHistogram =
            TestMeter.CreateHistogram<double>(
                name: "servicebus.processing.duration",
                unit: "seconds",
                description: "Time taken to process Service Bus messages");
    }
}
