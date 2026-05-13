using DineOS.Application.Common;
using DineOS.Application.Restaurants;

namespace DineOS.Application.Interfaces.Services;

public interface IEmailVerificationService
{
    /// <summary>
    /// Generates a fresh verification code for the given tenant owner, persists
    /// the hash, expires any earlier pending codes for the same email/purpose,
    /// and returns the plaintext to hand off to the email job.
    /// </summary>
    Task<string> IssueAccountVerificationCodeAsync(long tenantId, CancellationToken ct = default);

    /// <summary>
    /// Validates a submitted code. On success marks the tenant's owner email
    /// verified and consumes the code.
    /// </summary>
    Task<ServiceResult<bool>> ConfirmAccountVerificationCodeAsync(
        long tenantId,
        ConfirmEmailVerificationRequest request,
        CancellationToken ct = default);
}
