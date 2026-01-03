namespace ProductsMicroservice.Core.RabbitMQ
{
    public record ProductAddMessage(Guid ProductId, string? ProductName, double? UnitPrice, int? QuantityInStock);
}
