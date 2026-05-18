namespace DineOS.Application.Notifications;

public sealed record AccountVerificationEmailModel(
    string OwnerName,
    string RestaurantName,
    string Code,
    int    CodeTtlMinutes);

public sealed record DailyPaymentSummaryEmailModel(
    string OwnerName,
    string RestaurantName,
    DateOnly Date,
    decimal TotalRevenue,
    int    PaymentCount,
    IReadOnlyList<DailyPaymentSummaryEmailModel.LineItem> ByMethod)
{
    public sealed record LineItem(string Method, int Count, decimal Total);
}

public sealed record OverduePaymentEmailModel(
    string OwnerName,
    string RestaurantName,
    int    OverdueCount,
    decimal OverdueTotal,
    int    ThresholdMinutes,
    IReadOnlyList<OverduePaymentEmailModel.Row> Items)
{
    public sealed record Row(long PaymentId, long OrderId, decimal Amount, int AgeMinutes);
}

public sealed record SubscriptionActivatedEmailModel(
    string    OwnerName,
    string    RestaurantName,
    string    BillingCycle,
    DateTime? CurrentPeriodEnd);

public sealed record PaymentFailedEmailModel(
    string  OwnerName,
    string  RestaurantName,
    decimal Amount,
    string  Currency,
    string? HostedInvoiceUrl);

public sealed record SubscriptionCanceledEmailModel(
    string OwnerName,
    string RestaurantName);
