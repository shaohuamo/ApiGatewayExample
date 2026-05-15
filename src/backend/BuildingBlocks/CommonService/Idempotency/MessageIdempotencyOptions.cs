namespace CommonService.Idempotency;

public class MessageIdempotencyOptions
{
    public const string SectionName = "MessageIdempotency";

    public string KeyPrefix { get; set; } = "processed-message";

    public int ProcessedMessageTtlMinutes { get; set; } = 1440;
}