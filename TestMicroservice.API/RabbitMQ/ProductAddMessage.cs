namespace TestMicroservice.API.RabbitMQ
{
    public record ProductAddMessage(Guid ProductId, string? ProductName, double? UnitPrice, int? QuantityInStock);
}
