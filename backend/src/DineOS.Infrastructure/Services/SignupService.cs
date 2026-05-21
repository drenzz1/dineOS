using System.Text.RegularExpressions;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Application.Signup;
using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace DineOS.Infrastructure.Services;

public class SignupService(
    AppDbContext db,
    BillingService billingService,
    IValidator<SignupRequest> validator,
    IValidator<SetPasswordRequest> setPasswordValidator,
    IKeycloakAdminClient keycloakAdmin,
    ISetupTokenStore setupTokens,
    IOptions<StripeOptions> stripeOptions,
    ILogger<SignupService> logger) : ISignupService
{
    private readonly StripeOptions _stripe = stripeOptions.Value;

    private static readonly string[] RequiredActionsToClear =
        new[] { "UPDATE_PASSWORD", "VERIFY_EMAIL" };

    public async Task<ServiceResult<SignupResponse>> StartSignupAsync(
        SignupRequest request,
        CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<SignupResponse>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        if (!_stripe.IsConfigured)
        {
            logger.LogWarning("Public signup requested but Stripe is not configured.");
            return ServiceResult<SignupResponse>.UnprocessableEntity(
                "Billing provider is unavailable. Please try again later.");
        }

        var normalizedEmail = request.OwnerEmail.Trim();

        // Idempotency: reuse an existing pending-payment tenant for the same owner email.
        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.OwnerEmail == normalizedEmail && t.BillingStatus == BillingStatus.Incomplete,
                ct);

        if (tenant is null)
        {
            tenant = new Tenant
            {
                Name           = request.RestaurantName,
                Slug           = await GenerateUniqueSlugAsync(request.RestaurantName, ct),
                IsActive       = true,
                OwnerName      = request.OwnerName,
                OwnerEmail     = normalizedEmail,
                Phone          = request.Phone,
                City           = request.City,
                Plan           = SubscriptionPlan.Pro,
                BillingStatus  = BillingStatus.Incomplete,
                BillingCycle   = Domain.Enums.BillingCycle.Monthly,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "Public signup created pending tenant: TenantId={TenantId} Slug={Slug} OwnerEmail={OwnerEmail}",
                tenant.Id, tenant.Slug, tenant.OwnerEmail);
        }
        else
        {
            logger.LogInformation(
                "Public signup reusing pending tenant (idempotent): TenantId={TenantId} OwnerEmail={OwnerEmail}",
                tenant.Id, tenant.OwnerEmail);
        }

        ServiceResult<Session> sessionResult;
        try
        {
            sessionResult = await billingService.BuildCheckoutSessionAsync(
                tenant,
                Domain.Enums.BillingCycle.Monthly,
                ct);
        }
        catch (StripeException ex)
        {
            logger.LogError(ex,
                "Stripe error while starting public signup: TenantId={TenantId}", tenant.Id);
            return ServiceResult<SignupResponse>.UnprocessableEntity(
                "Billing provider is unavailable. Please try again later.");
        }

        if (!sessionResult.IsSuccess || sessionResult.Value is null)
        {
            return ServiceResult<SignupResponse>.UnprocessableEntity(
                sessionResult.Message ?? "Billing provider is unavailable. Please try again later.");
        }

        var session = sessionResult.Value;
        tenant.StripeSessionId = session.Id;
        await db.SaveChangesAsync(ct);

        return ServiceResult<SignupResponse>.Created(new SignupResponse
        {
            CheckoutUrl = session.Url,
            SessionId   = session.Id,
            TenantId    = tenant.Id,
        });
    }

    public async Task<ServiceResult<SignupStatusResponse>> GetStatusAsync(
        string sessionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return ServiceResult<SignupStatusResponse>.BadRequest("sessionId is required.");

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.StripeSessionId == sessionId, ct);

        if (tenant is null)
            return ServiceResult<SignupStatusResponse>.NotFound("Signup session not found.");

        var status = tenant.BillingStatus switch
        {
            BillingStatus.Incomplete => "PendingPayment",
            BillingStatus.Active     => "Active",
            BillingStatus.Trialing   => "Active",
            _                        => "Failed",
        };

        return ServiceResult<SignupStatusResponse>.Ok(new SignupStatusResponse { Status = status });
    }

    public async Task<ServiceResult<string>> CompleteSetupAsync(
        SetPasswordRequest request,
        CancellationToken ct = default)
    {
        var validation = await setPasswordValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return ServiceResult<string>.ValidationFailed(
                "Validation failed",
                validation.Errors.Select(e => e.ErrorMessage).ToList());
        }

        var tenantId = await setupTokens.ConsumeAsync(request.Token, ct);
        if (tenantId is null)
        {
            logger.LogWarning(
                "Set-password attempted with invalid or expired token: {TokenPrefix}…",
                request.Token.Length > 6 ? request.Token[..6] : request.Token);
            return ServiceResult<string>.NotFound("This link is invalid or has expired.");
        }

        var tenant = await db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value && t.DeletedAt == null, ct);

        if (tenant?.KeycloakUserId is null)
        {
            logger.LogWarning(
                "Set-password token resolved but tenant or Keycloak user is missing. TenantId={TenantId}",
                tenantId);
            return ServiceResult<string>.NotFound("This account is not ready yet. Please try again in a few minutes.");
        }

        try
        {
            await keycloakAdmin.SetPasswordAsync(tenant.KeycloakUserId, request.NewPassword, ct);
            // Consuming the one-time setup token proves the owner controls
            // the email, so we flip emailVerified=true in the same PUT.
            await keycloakAdmin.ClearRequiredActionsAsync(
                tenant.KeycloakUserId, RequiredActionsToClear, emailVerified: true, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Keycloak rejected set-password for TenantId={TenantId} KeycloakUserId={KeycloakUserId}",
                tenant.Id, tenant.KeycloakUserId);
            return ServiceResult<string>.UnprocessableEntity(
                "We couldn't update your password. Please try again or contact support.");
        }

        logger.LogInformation(
            "Set-password completed: TenantId={TenantId} Email={OwnerEmail}",
            tenant.Id, tenant.OwnerEmail);

        return ServiceResult<string>.Ok(tenant.OwnerEmail, "Password set.");
    }

    private async Task<string> GenerateUniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Regex.Replace(name.ToLower().Trim(), @"[^a-z0-9]+", "-").Trim('-');
        if (string.IsNullOrEmpty(baseSlug)) baseSlug = "restaurant";

        var slug = baseSlug;
        var suffix = 1;
        while (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Slug == slug, ct))
        {
            suffix++;
            slug = $"{baseSlug}-{suffix}";
        }
        return slug;
    }
}
