namespace DineOS.Infrastructure.Auth;

public sealed class KeycloakAdminException : Exception
{
    public int StatusCode { get; }

    public KeycloakAdminException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
