namespace Throttling;

/// <summary>
/// What time it is. A throttle needs to know which window a request falls in;
/// it never waits, so this is a clock rather than a delay.
///
/// Owned by this pattern, like every other fake here, so the folder lifts out
/// on its own.
/// </summary>
public interface IClock
{
    /// <summary>The current instant, in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>A clock the caller moves by hand, so window boundaries are assertable.</summary>
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
