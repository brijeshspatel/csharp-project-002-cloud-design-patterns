namespace CircuitBreaker;

/// <summary>
/// What time it is.
///
/// Note the shape: this is a *clock*, not a delay. A breaker never sleeps — it
/// asks whether the break duration has elapsed and answers immediately. That is
/// why this tier carries an <c>IClock</c> rather than reusing Retry's
/// <c>ITimeSource</c>, which exists to wait.
///
/// It lives in this pattern's own folder. Every pattern here owns its own copy,
/// deliberately, so that one folder can be lifted out without bringing a shared
/// library with it.
/// </summary>
public interface IClock
{
    /// <summary>The current instant, in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>
/// A clock the caller moves by hand.
///
/// This is what makes the break duration assertable. A breaker tested against
/// the real clock could only be tested by a suite that really waits thirty
/// seconds, so in practice its timing would never be asserted at all.
/// </summary>
public sealed class ManualClock : IClock
{
    /// <summary>Starts the clock at <paramref name="start"/>.</summary>
    public ManualClock(DateTimeOffset start) => UtcNow = start;

    /// <inheritdoc/>
    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>Moves the clock forward by <paramref name="duration"/>.</summary>
    public void Advance(TimeSpan duration) => UtcNow += duration;
}

/// <summary>
/// The real clock. Not used by the tests or the demonstration, and present so a
/// reader can see what the production implementation is.
/// </summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
