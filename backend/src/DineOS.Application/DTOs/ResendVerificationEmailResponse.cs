namespace DineOS.Application.DTOs;

/// <summary>Structured result of re-enqueuing the owner verification email.</summary>
/// <param name="JobId">The Hangfire background-job id of the queued send.</param>
public record ResendVerificationEmailResponse(string JobId);
