namespace DineOS.Application.DTOs;

/// <summary>
/// A single invoice record returned by <c>GET /api/v1/billing/invoices</c>.
/// </summary>
public class TenantInvoiceDto
{
    public long      Id               { get; set; }
    public decimal   Amount           { get; set; }
    public string    Currency         { get; set; } = string.Empty;
    public string    Status           { get; set; } = string.Empty;
    public DateTime? PaidAt           { get; set; }
    public string?   HostedInvoiceUrl { get; set; }
    public DateTime  CreatedAt        { get; set; }
}
