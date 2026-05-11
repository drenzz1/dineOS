namespace DineOS.Application.DTOs;

public class RestaurantTableDto
{
    public long Id { get; set; }
    public int Number { get; set; }
    public int Capacity { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public long TenantId { get; set; }
}
