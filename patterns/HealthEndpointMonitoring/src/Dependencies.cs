namespace HealthEndpointMonitoring;

/// <summary>Checks that a database answers.</summary>
public sealed class DatabaseCheck : IHealthCheck
{
    private readonly ManualClock clock;
    private readonly TimeSpan latency;

    /// <summary>Creates a check that takes <paramref name="latency"/> to answer.</summary>
    public DatabaseCheck(ManualClock clock, TimeSpan latency)
    {
        ArgumentNullException.ThrowIfNull(clock);
        this.clock = clock;
        this.latency = latency;
    }

    /// <inheritdoc/>
    public string Name => "sql";

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckAsync()
    {
        // Advancing the clock is how latency is modelled without sleeping. The
        // endpoint measures duration from the same clock, so a slow dependency
        // is genuinely observed rather than declared.
        clock.Advance(latency);
        return Task.FromResult(
            new HealthCheckResult(HealthStatus.Healthy, "connection opened, query returned"));
    }
}

/// <summary>Checks a cache that is optional to the application.</summary>
public sealed class CacheCheck : IHealthCheck
{
    private readonly HealthStatus status;

    /// <summary>Creates a check reporting <paramref name="status"/>.</summary>
    public CacheCheck(HealthStatus status) => this.status = status;

    /// <inheritdoc/>
    public string Name => "redis";

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckAsync() => Task.FromResult(
        new HealthCheckResult(
            status,
            status == HealthStatus.Healthy
                ? "responding"
                : "evictions high, falling back to the database"));
}

/// <summary>
/// A check that is itself broken — a misconfigured connection string, a null
/// reference in the probe's own code.
///
/// It exists because this is the case that takes real endpoints down: the
/// application is fine and the *checking* is not.
/// </summary>
public sealed class BrokenBrokerCheck : IHealthCheck
{
    /// <inheritdoc/>
    public string Name => "servicebus";

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckAsync() =>
        throw new InvalidOperationException("no connection string configured for servicebus");
}
