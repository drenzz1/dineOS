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

    /// <summary>
    /// Marks the tenant owner's email verified WITHOUT a code, for cases where
    /// email ownership is proven by another means — e.g. completing the
    /// first-login password change, which is only possible if the owner
    /// received the emailed temporary credentials. Idempotent: no-op when the
    /// owner is already verified or no matching tenant exists.
    /// </summary>
    Task MarkOwnerEmailVerifiedAsync(string ownerEmail, CancellationToken ct = default);

    /// <summary>
    /// Generates a fresh password-reset code for the given email, persists the
    /// hash, expires any earlier pending reset codes for the same email, and
    /// returns the plaintext to hand off to the email job. Does NOT check that
    /// an account exists — callers do that, so issuance stays usable from the
    /// enumeration-safe background job.
    /// </summary>
    Task<string> IssuePasswordResetCodeAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Validates a submitted password-reset code and consumes it on success so
    /// it cannot be replayed. Every failure shape (missing, expired, attempt
    /// cap, mismatch) returns the same constant message so the response cannot
    /// be used to probe which emails have pending resets.
    /// </summary>
    Task<ServiceResult<bool>> ConsumePasswordResetCodeAsync(
        string email,
        string code,
        CancellationToken ct = default);
}
