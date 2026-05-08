namespace DineOS.Application.DTOs;

public class OrderDto
{
    public long Id { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public int? TableNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string? Notes { get; set; }
    public long TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
}
