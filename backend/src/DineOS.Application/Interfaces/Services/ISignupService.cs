using DineOS.Application.Common;
using DineOS.Application.Signup;

namespace DineOS.Application.Interfaces.Services;

public interface ISignupService
{
    /// <summary>
    /// Public-facing signup. Creates (or reuses) a PendingPayment tenant for
    /// the supplied owner email and returns a Stripe Checkout URL the
    /// frontend will redirect to. Idempotent: a second call with the same
    /// owner email while the tenant is still Incomplete returns the same
    /// tenant id and a fresh checkout session.
    /// </summary>
    Task<ServiceResult<SignupResponse>> StartSignupAsync(
        SignupRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Looks up the public-signup status for a given Stripe Checkout session id.
    /// Returns "PendingPayment", "Active", or "Failed".
    /// </summary>
    Task<ServiceResult<SignupStatusResponse>> GetStatusAsync(
        string sessionId,
        CancellationToken ct = default);

    /// <summary>
    /// Completes the dineOS-hosted set-password flow: validates the one-time
    /// Redis-backed token, calls the Keycloak admin API to set the user's
    /// password, clears UPDATE_PASSWORD / VERIFY_EMAIL required actions, and
    /// invalidates the token so the email link can't be replayed.
    /// </summary>
    Task<ServiceResult<string>> CompleteSetupAsync(
        SetPasswordRequest request,
        CancellationToken ct = default);
}
