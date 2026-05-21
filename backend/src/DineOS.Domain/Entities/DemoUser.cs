using DineOS.Domain.Common;
using DineOS.Domain.Enums;

namespace DineOS.Domain.Entities;

/// <summary>
/// A visitor who requested a demo access account (#216). One row per email;
/// re-requests reset <c>ExpiresAt</c> + reissue a fresh password instead of
/// inserting another row. The matching Keycloak user is identified by
/// <see cref="KeycloakUserId"/> and is disabled when the row's
/// <see cref="ExpiresAt"/> elapses.
/// </summary>
public class DemoUser : BaseAuditingEntity
{
    public string Email { get; set; } = string.Empty;
    public string? KeycloakUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastEmailSentAt { get; set; }
    public string? IpAddress { get; set; }
    public DemoUserStatus Status { get; set; } = DemoUserStatus.Pending;
}
