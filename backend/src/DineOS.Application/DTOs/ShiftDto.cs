namespace DineOS.Application.DTOs;

public class ShiftDto
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public long StaffMemberId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? Notes { get; set; }
}
