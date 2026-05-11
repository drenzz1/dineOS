namespace DineOS.Application.DTOs;

public class ShiftNoteDto
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public long TenantId { get; set; }
    public DateTime CreatedAt { get; set; }
}
