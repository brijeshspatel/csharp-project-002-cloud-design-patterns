namespace HealthEndpointMonitoring;

/// <summary>
/// Runs every registered check and reports what it found.
///
/// Three rules do the work, and each exists because of a way real health
/// endpoints lie:
///
/// <list type="bullet">
/// <item><b>The worst status wins.</b> An endpoint that averaged, or reported
/// the first result, would call a system healthy while one of its dependencies
/// was down.</item>
/// <item><b>A check that throws is unhealthy, not fatal.</b> A broken check must
/// never take the endpoint with it — an endpoint that returns 500 because its
/// own code failed tells the load balancer nothing about the application.</item>
/// <item><b>Nothing to check is unhealthy.</b> A probe that examined nothing is
/// not evidence of health, and reporting Healthy would be a green result over an
/// empty measurement.</item>
/// </list>
///
/// It is a component rather than an HTTP surface. What serves it over HTTP is
/// named in the README; hosting a web server here would take a dependency this
/// repository does not take.
/// </summary>
public sealed class HealthEndpoint
{
    private readonly IReadOnlyList<IHealthCheck> checks;
    private readonly TimeSpan slowThreshold;
    private readonly IClock clock;

    /// <summary>Creates an endpoint over <paramref name="checks"/>.</summary>
    /// <param name="checks">Everything worth checking.</param>
    /// <param name="slowThreshold">Beyond this, a healthy check is reported degraded.</param>
    /// <param name="clock">Where the current time comes from.</param>
    public HealthEndpoint(
        IReadOnlyList<IHealthCheck> checks, TimeSpan slowThreshold, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(clock);

        this.checks = checks;
        this.slowThreshold = slowThreshold;
        this.clock = clock;
    }

    /// <summary>Runs every check and aggregates the results.</summary>
    public async Task<HealthReport> ProbeAsync()
    {
        if (checks.Count == 0)
        {
            return new HealthReport(
                HealthStatus.Unhealthy,
                [new HealthEntry(
                    "(none)",
                    HealthStatus.Unhealthy,
                    "no checks are registered, so this probe proves nothing",
                    TimeSpan.Zero)]);
        }

        List<HealthEntry> entries = new(checks.Count);

        foreach (IHealthCheck check in checks)
        {
            entries.Add(await RunAsync(check).ConfigureAwait(false));
        }

        HealthStatus worst = entries.Max(entry => entry.Status);
        return new HealthReport(worst, entries);
    }

    private async Task<HealthEntry> RunAsync(IHealthCheck check)
    {
        DateTimeOffset started = clock.UtcNow;

        HealthCheckResult result;
        try
        {
            result = await check.CheckAsync().ConfigureAwait(false);
        }
        // Catching everything is the requirement, not an oversight: a check that
        // throws must be reported, never propagated. An endpoint that fails
        // because one of its own probes threw tells an operator nothing about
        // the application it was asked about.
        catch (Exception failure)
        {
            return new HealthEntry(
                check.Name,
                HealthStatus.Unhealthy,
                $"the check itself failed: {failure.Message}",
                clock.UtcNow - started);
        }

        TimeSpan duration = clock.UtcNow - started;

        // A dependency that answers correctly but slowly is degraded, not
        // healthy. Reporting it healthy is how a system that is minutes from
        // falling over looks fine right up until it does.
        HealthStatus status = result.Status == HealthStatus.Healthy && duration > slowThreshold
            ? HealthStatus.Degraded
            : result.Status;

        string detail = status == HealthStatus.Degraded && result.Status == HealthStatus.Healthy
            ? $"{result.Detail} (took {duration.TotalMilliseconds:0}ms, over the threshold)"
            : result.Detail;

        return new HealthEntry(check.Name, status, detail, duration);
    }
}
