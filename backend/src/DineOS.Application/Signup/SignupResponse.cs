namespace DineOS.Application.Signup;

public class SignupResponse
{
    public string CheckoutUrl { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public long TenantId { get; set; }
}

public class SignupStatusResponse
{
    public string Status { get; set; } = string.Empty;
}
