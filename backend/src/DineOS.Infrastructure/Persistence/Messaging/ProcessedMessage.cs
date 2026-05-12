namespace DineOS.Infrastructure.Persistence.Messaging;

public class ProcessedMessage
{
    public string MessageId { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public long TenantId { get; set; }
    public DateTime ProcessedAt { get; set; }
}
