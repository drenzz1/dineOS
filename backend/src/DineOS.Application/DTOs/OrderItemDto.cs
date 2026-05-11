namespace DineOS.Application.DTOs;

public class OrderItemDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}
