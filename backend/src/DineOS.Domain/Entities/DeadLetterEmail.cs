using DineOS.Domain.Common;

namespace DineOS.Domain.Entities;

/// <summary>
/// Permanent record of an email that exhausted its Hangfire retry budget.
/// Written by the dead-letter state filter when a job transitions to FailedState.
/// </summary>
public class DeadLetterEmail : BaseAuditingEntity
{
    public string ToAddress    { get; set; } = string.Empty;
    public string Subject      { get; set; } = string.Empty;
    public string Body         { get; set; } = string.Empty;
    public string JobId        { get; set; } = string.Empty;
    public string JobType      { get; set; } = string.Empty;
    public int    AttemptCount { get; set; }
    public string FailureReason{ get; set; } = string.Empty;
    public string? ExceptionDetails { get; set; }
    public DateTime FailedAt   { get; set; }
    public long?  TenantId     { get; set; }
}
