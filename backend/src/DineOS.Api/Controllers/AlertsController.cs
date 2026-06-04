using Asp.Versioning;
using DineOS.Application.Alerts;
using DineOS.Application.Common;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DineOS.Api.Controllers;

/// <summary>
/// Alertmanager webhook receiver. This endpoint is intentionally anonymous and
/// never returns a non-200 status for processing errors — Alertmanager treats
/// any non-2xx as a delivery failure and retries, which would flood the pipeline.
///
/// Security: configure AlertWebhook:SharedSecret. Two headers are accepted so both
/// Alertmanager and custom clients work without reconfiguration:
///   - Alertmanager 0.28: http_config.authorization.credentials → "Authorization: Bearer {secret}"
///   - Custom clients / scripts: X-Webhook-Secret: {secret}
/// Mismatches are logged and the payload is silently dropped (still returns 200).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/alerts")]
[Produces("application/json")]
[AllowAnonymous]
public class AlertsController(
    IIncidentTriageService triageService,
    IOptions<AlertWebhookOptions> webhookOptions,
    ILogger<AlertsController> logger) : ControllerBase
{
    public const string SharedSecretHeader      = "X-Webhook-Secret";
    private const string BearerPrefix           = "Bearer ";

    /// <summary>
    /// Receives an Alertmanager webhook payload, triages each firing alert with
    /// the configured AI provider, and returns the triage results.
    /// Always returns 200 — processing failures are logged, not surfaced.
    /// </summary>
    [HttpPost("webhook")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<IncidentTriageResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceiveWebhook(
        [FromBody] AlertmanagerWebhookPayload payload,
        CancellationToken ct)
    {
        var secret = webhookOptions.Value.SharedSecret;
        if (!string.IsNullOrWhiteSpace(secret))
        {
            // Accept the secret from either header so Alertmanager (Authorization: Bearer)
            // and custom clients (X-Webhook-Secret) both work without reconfiguration.
            var customHeader = Request.Headers[SharedSecretHeader].FirstOrDefault();
            var bearerHeader = Request.Headers.Authorization.FirstOrDefault();
            var bearerToken  = bearerHeader?.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) == true
                ? bearerHeader[BearerPrefix.Length..]
                : null;

            if (customHeader != secret && bearerToken != secret)
            {
                logger.LogWarning(
                    "Webhook received with missing or incorrect secret. RemoteIp={RemoteIp}",
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                return Ok(ApiResponse<IReadOnlyList<IncidentTriageResultDto>>.Ok(
                    Array.Empty<IncidentTriageResultDto>(),
                    "Secret mismatch — payload not processed."));
            }
        }

        try
        {
            var results = await triageService.ProcessWebhookAsync(payload, ct);

            return Ok(ApiResponse<IReadOnlyList<IncidentTriageResultDto>>.Ok(results));
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unhandled error processing Alertmanager webhook. " +
                "AlertCount={AlertCount} PayloadStatus={PayloadStatus}",
                payload.Alerts?.Count ?? 0,
                payload.Status);

            return Ok(ApiResponse<IReadOnlyList<IncidentTriageResultDto>>.Ok(
                Array.Empty<IncidentTriageResultDto>(),
                "Triage failed — alert acknowledged."));
        }
    }
}
