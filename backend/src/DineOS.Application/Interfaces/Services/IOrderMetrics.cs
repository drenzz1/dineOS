namespace DineOS.Application.Interfaces.Services;

/// <summary>
/// Abstraction for order-level business counters. Keeps prometheus-net out of the
/// Application and Infrastructure layers so the metric implementation is swappable.
/// </summary>
public interface IOrderMetrics
{
    /// <summary>
    /// Records one successfully persisted order. Must be called only after
    /// <c>SaveChangesAsync</c> commits — not on validation or tenant errors.
    /// </summary>
    void IncrementOrdersCreated();
}
