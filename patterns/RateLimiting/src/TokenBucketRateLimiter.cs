namespace RateLimiting;

/// <summary>
/// Paces a client so it stays inside a limit somebody else published.
///
/// **This is the client side of the conversation.** The server deciding what it
/// will accept from you is Throttling; this is what you run so that its answer
/// is always yes. A limiter that merely refused its own caller would be a
/// throttle applied to the wrong party — the distinctive move here is
/// <see cref="RetryAfter"/>, which turns a refusal into an instruction.
///
/// A token bucket rather than a fixed window, deliberately. A window resets on
/// a boundary, which lets a client spend a full allowance either side of it and
/// achieve twice the intended rate; tokens accrue continuously, so the long-run
/// rate holds no matter where you start looking. The bucket's capacity is what
/// still permits a burst, because most published limits do.
/// </summary>
public sealed class TokenBucketRateLimiter
{
    private readonly double capacity;
    private readonly double refillPerSecond;
    private readonly IClock clock;

    private double tokens;
    private DateTimeOffset lastRefill;

    /// <summary>Creates a limiter, with a full bucket.</summary>
    /// <param name="capacity">The largest burst permitted. At least one.</param>
    /// <param name="refillPerSecond">The sustained rate, in calls per second.</param>
    /// <param name="clock">Where the current time comes from.</param>
    public TokenBucketRateLimiter(int capacity, double refillPerSecond, IClock clock)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(refillPerSecond);
        ArgumentNullException.ThrowIfNull(clock);

        this.capacity = capacity;
        this.refillPerSecond = refillPerSecond;
        this.clock = clock;

        tokens = capacity;
        lastRefill = clock.UtcNow;
    }

    /// <summary>Takes a token if one is available.</summary>
    /// <returns><c>true</c> when the call may proceed.</returns>
    public bool TryAcquire()
    {
        Refill();

        if (tokens < 1.0)
        {
            return false;
        }

        tokens -= 1.0;
        return true;
    }

    /// <summary>
    /// How long until a token is available. <see cref="TimeSpan.Zero"/> when one
    /// already is.
    ///
    /// This is the value that makes the pattern useful rather than merely
    /// restrictive: a caller that knows the wait can schedule around it instead
    /// of spinning.
    /// </summary>
    public TimeSpan RetryAfter()
    {
        Refill();

        if (tokens >= 1.0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds((1.0 - tokens) / refillPerSecond);
    }

    /// <summary>Accrues the tokens earned since the last look.</summary>
    private void Refill()
    {
        DateTimeOffset now = clock.UtcNow;
        double elapsedSeconds = (now - lastRefill).TotalSeconds;

        if (elapsedSeconds <= 0)
        {
            return;
        }

        tokens = Math.Min(capacity, tokens + (elapsedSeconds * refillPerSecond));
        lastRefill = now;
    }
}
