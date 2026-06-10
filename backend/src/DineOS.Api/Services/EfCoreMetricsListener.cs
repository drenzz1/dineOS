using System.Diagnostics;
using Prometheus;

namespace DineOS.Api.Services;

/// <summary>
/// Hosted service that subscribes to the EF Core DiagnosticSource and records
/// <see cref="CommandDuration"/> — the wall-clock time each database command takes
/// from the moment EF Core sends it to the driver until a result or error is received.
/// Registered as a singleton <see cref="IHostedService"/> so it lives for the full
/// application lifetime and uses no request-scoped dependencies.
/// </summary>
public sealed class EfCoreMetricsListener
    : IHostedService,
      IObserver<DiagnosticListener>,
      IObserver<KeyValuePair<string, object?>>
{
    private const string EfCoreDiagnosticSource = "Microsoft.EntityFrameworkCore";
    private const string CommandExecutedEvent    = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted";

    /// <summary>
    /// Histogram of EF Core database command durations in seconds, labelled by the
    /// SQL verb (select / insert / update / delete / other). Buckets span 1 ms to 5 s
    /// to cover both fast index lookups and slow analytical queries.
    /// </summary>
    private static readonly Histogram CommandDuration = Metrics.CreateHistogram(
        "dineos_ef_command_duration_seconds",
        "Wall-clock duration of EF Core database commands sent to PostgreSQL.",
        new HistogramConfiguration
        {
            Buckets    = [0.001, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0],
            LabelNames = ["command_type"]
        });

    private IDisposable? _allListenersSubscription;

    // Guards _efSubscriptions: OnNext(DiagnosticListener) is invoked on background
    // threads by DiagnosticListener.AllListeners and can race StopAsync during host
    // shutdown. Without the lock, the foreach in StopAsync throws "Collection was
    // modified" when a listener is added mid-teardown.
    private readonly object _subscriptionsLock = new();
    private readonly List<IDisposable> _efSubscriptions = [];

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Subscribe to every DiagnosticListener registered in the process.
        // OnNext(DiagnosticListener) is called for each existing and future listener.
        _allListenersSubscription = DiagnosticListener.AllListeners.Subscribe(this);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop receiving new listeners first, then dispose a snapshot taken under
        // the lock so a concurrent OnNext add can't invalidate the enumeration.
        _allListenersSubscription?.Dispose();

        IDisposable[] subscriptions;
        lock (_subscriptionsLock)
        {
            subscriptions = _efSubscriptions.ToArray();
            _efSubscriptions.Clear();
        }

        foreach (var sub in subscriptions)
            sub.Dispose();
        return Task.CompletedTask;
    }

    // ── IObserver<DiagnosticListener> ──────────────────────────────────────────────

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (listener.Name != EfCoreDiagnosticSource)
            return;

        var subscription = listener.Subscribe(this);
        lock (_subscriptionsLock)
            _efSubscriptions.Add(subscription);
    }

    void IObserver<DiagnosticListener>.OnError(Exception error) { }
    void IObserver<DiagnosticListener>.OnCompleted() { }

    // ── IObserver<KeyValuePair<string, object?>> ───────────────────────────────────

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> kvp)
    {
        if (kvp.Key != CommandExecutedEvent || kvp.Value is null)
            return;

        var payloadType = kvp.Value.GetType();

        // CommandExecutedEventData.Duration — public property, safe via reflection.
        if (payloadType.GetProperty("Duration")?.GetValue(kvp.Value) is not TimeSpan elapsed)
            return;

        // CommandExecutedEventData.Command — DbCommand; read CommandText to classify.
        var command     = payloadType.GetProperty("Command")?.GetValue(kvp.Value);
        var commandText = command?.GetType()
                              .GetProperty("CommandText")?.GetValue(command) as string
                          ?? string.Empty;

        var commandType = commandText
            .TrimStart()
            .Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.ToUpperInvariant() switch
        {
            "SELECT" => "select",
            "INSERT" => "insert",
            "UPDATE" => "update",
            "DELETE" => "delete",
            _        => "other"
        };

        CommandDuration.WithLabels(commandType).Observe(elapsed.TotalSeconds);
    }

    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error) { }
    void IObserver<KeyValuePair<string, object?>>.OnCompleted() { }
}
