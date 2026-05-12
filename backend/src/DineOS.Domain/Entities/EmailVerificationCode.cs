using DineOS.Domain.Common;
using DineOS.Domain.Enums;

namespace DineOS.Domain.Entities;

/// <summary>
/// One-time verification code issued to a restaurant owner.
/// Stored as a SHA-256 hash; the plaintext only exists during the sending
/// of the email and is never persisted.
/// </summary>
public class EmailVerificationCode : BaseAuditingEntity
{
    public string                  Email          { get; set; } = string.Empty;
    public EmailVerificationPurpose Purpose       { get; set; } = EmailVerificationPurpose.AccountVerification;
    public string                  CodeHash       { get; set; } = string.Empty;
    public DateTime                ExpiresAt      { get; set; }
    public DateTime?               ConsumedAt     { get; set; }
    public int                     FailedAttempts { get; set; }
    public long?                   TenantId       { get; set; }
}
