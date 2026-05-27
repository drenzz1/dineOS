using DineOS.Application.Interfaces.Services;
using Prometheus;

namespace DineOS.Api.Services;

/// <summary>
/// Prometheus implementation of <see cref="IOrderMetrics"/>. Static counter fields
/// are registered once in the default registry at class-load time, which is safe
/// because prometheus-net's registry is process-global and thread-safe.
/// </summary>
public sealed class PrometheusOrderMetrics : IOrderMetrics
{
    /// <summary>
    /// Total number of orders successfully written to the database.
    /// Incremented only after <c>SaveChangesAsync</c> succeeds inside
    /// <c>OrderService.CreateOrderAsync</c>; validation failures, missing
    /// tenant context, and DB errors are not counted.
    /// </summary>
    private static readonly Counter OrdersCreatedCounter = Metrics.CreateCounter(
        "dineos_orders_created_total",
        "Total number of orders successfully created and persisted to the database.");

    public void IncrementOrdersCreated() => OrdersCreatedCounter.Inc();
}
