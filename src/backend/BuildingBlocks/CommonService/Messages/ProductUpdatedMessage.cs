namespace ProductsMicroservice.Core.MessageQueue.Messages;

public record ProductUpdatedMessage(
	Guid ProductId,
	string? ProductName,
	double? UnitPrice,
	int? QuantityInStock,
	int Version);