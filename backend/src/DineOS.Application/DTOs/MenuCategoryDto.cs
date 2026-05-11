namespace DineOS.Application.DTOs;

public class MenuCategoryDto
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public long TenantId { get; set; }
}
