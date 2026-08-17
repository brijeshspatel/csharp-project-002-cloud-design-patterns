namespace Throttling;

/// <summary>What the throttle decided to do with a request.</summary>
public enum ThrottleDecision
{
    /// <summary>Serve it in full.</summary>
    Accepted,

    /// <summary>Serve it, but cheaply — cached, sampled, or lower fidelity.</summary>
    Degraded,

    /// <summary>Do not serve it.</summary>
    Rejected,
}

/// <summary>
/// Protects a resource its owner controls, by limiting what any one consumer
/// may take of it in a window.
///
/// **The degradation step is the pattern.** A throttle that only rejects is a
/// rate limiter facing the wrong way. Between the soft and hard limits this one
/// still answers — from a cache, at lower fidelity, with fewer rows — and only
/// past the hard limit does it refuse. A tenant that gets a slightly stale
/// report has been served; a tenant that gets an error has not.
///
/// This is the **server** side of the conversation. The client side, pacing
/// itself so it never provokes any of this, is Rate Limiting.
/// </summary>
public sealed class Throttle
{
    private readonly int softLimit;
    private readonly int hardLimit;
    private readonly TimeSpan window;
    private readonly IClock clock;
    private readonly Dictionary<string, Usage> usage = [];

    /// <summary>Creates a throttle.</summary>
    /// <param name="softLimit">Requests served in full before degradation begins.</param>
    /// <param name="hardLimit">Requests served at all, degraded or not, before refusal.</param>
    /// <param name="window">How long an allowance lasts.</param>
    /// <param name="clock">Where the current time comes from.</param>
    public Throttle(int softLimit, int hardLimit, TimeSpan window, IClock clock)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(softLimit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(hardLimit, softLimit);
        ArgumentNullException.ThrowIfNull(clock);

        this.softLimit = softLimit;
        this.hardLimit = hardLimit;
        this.window = window;
        this.clock = clock;
    }

    /// <summary>Decides what to do with a request from <paramref name="tenant"/>.</summary>
    public ThrottleDecision Admit(string tenant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);

        DateTimeOffset now = clock.UtcNow;

        if (!usage.TryGetValue(tenant, out Usage current) ||
            now - current.WindowStarted >= window)
        {
            // First request, or the previous window has expired. Per-tenant
            // state is what keeps one noisy consumer from spending another's
            // allowance -- a single global counter would let the loudest tenant
            // throttle everybody.
            current = new Usage(now, 0);
        }

        int count = current.Count + 1;
        usage[tenant] = current with { Count = count };

        if (count <= softLimit)
        {
            return ThrottleDecision.Accepted;
        }

        return count <= hardLimit
            ? ThrottleDecision.Degraded
            : ThrottleDecision.Rejected;
    }

    private readonly record struct Usage(DateTimeOffset WindowStarted, int Count);
}
