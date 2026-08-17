namespace RateLimiting;

/// <summary>
/// What time it is. A token bucket accrues tokens continuously from elapsed
/// time, so it needs a clock; it never sleeps on its own behalf, so it does not
/// need a delay.
///
/// Owned by this pattern, so the folder lifts out on its own.
/// </summary>
public interface IClock
{
    /// <summary>The current instant, in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>A clock the caller moves by hand, so refill is assertable to the millisecond.</summary>
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
