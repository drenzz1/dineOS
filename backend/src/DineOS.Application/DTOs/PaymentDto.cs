namespace DineOS.Application.DTOs;

public class PaymentDto
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
}
