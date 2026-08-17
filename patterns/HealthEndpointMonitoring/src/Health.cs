namespace HealthEndpointMonitoring;

/// <summary>How healthy something is. Ordered, so the worst can be selected.</summary>
public enum HealthStatus
{
    /// <summary>Working as intended.</summary>
    Healthy,

    /// <summary>Working, but not well. Serving, and worth looking at.</summary>
    Degraded,

    /// <summary>Not working. Should not receive traffic.</summary>
    Unhealthy,
}

/// <summary>What a single check found.</summary>
/// <param name="Status">How healthy the dependency is.</param>
/// <param name="Detail">Something an operator can act on.</param>
public readonly record struct HealthCheckResult(HealthStatus Status, string Detail);

/// <summary>One check's contribution to a report.</summary>
/// <param name="Name">Which check.</param>
/// <param name="Status">What it concluded, after the slow-check rule was applied.</param>
/// <param name="Detail">Something an operator can act on.</param>
/// <param name="Duration">How long it took.</param>
public readonly record struct HealthEntry(
    string Name, HealthStatus Status, string Detail, TimeSpan Duration);

/// <summary>The whole probe.</summary>
/// <param name="Status">The worst status any check reported.</param>
/// <param name="Entries">Every check, so a reader can see *which* one is unhappy.</param>
public sealed record HealthReport(HealthStatus Status, IReadOnlyList<HealthEntry> Entries);

/// <summary>One thing worth checking.</summary>
public interface IHealthCheck
{
    /// <summary>What this check is called, as it appears in the report.</summary>
    string Name { get; }

    /// <summary>Runs the check.</summary>
    Task<HealthCheckResult> CheckAsync();
}

/// <summary>
/// What time it is. Used to measure how long each check took, which is what
/// turns "the database answered" into "the database answered, slowly".
/// </summary>
public interface IClock
{
    /// <summary>The current instant, in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>A clock the caller moves by hand, so slowness is modelled without sleeping.</summary>
public sealed class ManualClock : IClock
{
    /// <summary>Starts the clock at <paramref name="start"/>.</summary>
    public ManualClock(DateTimeOffset start) => UtcNow = start;

    /// <inheritdoc/>
    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>Moves the clock forward by <paramref name="duration"/>.</summary>
    public void Advance(TimeSpan duration) => UtcNow += duration;
}

/// <summary>The real clock. What production would use.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
