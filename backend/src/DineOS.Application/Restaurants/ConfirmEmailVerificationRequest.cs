namespace DineOS.Application.Restaurants;

public sealed class ConfirmEmailVerificationRequest
{
    public string Code { get; init; } = string.Empty;
}
