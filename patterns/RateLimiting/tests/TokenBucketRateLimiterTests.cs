namespace RateLimiting.Tests;

/// <summary>
/// What a client-side rate limiter guarantees: that a burst is allowed up to
/// the bucket's capacity, that the long-run rate never exceeds the published
/// one, and that a refusal comes with a usable answer to "how long?".
///
/// The last is what separates this from a throttle. A throttle refuses and the
/// caller is stuck; a limiter refuses its own caller and tells it exactly when
/// to come back.
/// </summary>
public class TokenBucketRateLimiterTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 17, 9, 0, 0, TimeSpan.Zero);

    private static (TokenBucketRateLimiter Limiter, ManualClock Clock) Build(
        int capacity = 5, double refillPerSecond = 5)
    {
        ManualClock clock = new(Start);
        TokenBucketRateLimiter limiter = new(capacity, refillPerSecond, clock);
        return (limiter, clock);
    }

    [Fact]
    public void Allows_a_burst_up_to_the_bucket_capacity()
    {
        (TokenBucketRateLimiter limiter, _) = Build(capacity: 5);

        for (int i = 0; i < 5; i++)
        {
            Assert.True(limiter.TryAcquire(), $"call {i + 1} should have been permitted");
        }
    }

    [Fact]
    public void Refuses_once_the_bucket_is_empty()
    {
        (TokenBucketRateLimiter limiter, _) = Build(capacity: 5);

        for (int i = 0; i < 5; i++)
        {
            limiter.TryAcquire();
        }

        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public void Refills_over_time_at_the_configured_rate()
    {
        (TokenBucketRateLimiter limiter, ManualClock clock) = Build(
            capacity: 5, refillPerSecond: 5);

        for (int i = 0; i < 5; i++)
        {
            limiter.TryAcquire();
        }

        Assert.False(limiter.TryAcquire());

        // At five per second, one token accrues every two hundred milliseconds.
        clock.Advance(TimeSpan.FromMilliseconds(200));

        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public void Reports_how_long_to_wait_when_it_refuses()
    {
        (TokenBucketRateLimiter limiter, _) = Build(capacity: 2, refillPerSecond: 4);

        limiter.TryAcquire();
        limiter.TryAcquire();

        Assert.False(limiter.TryAcquire());

        // Four per second means a token every two hundred and fifty
        // milliseconds, and the bucket is empty.
        Assert.Equal(TimeSpan.FromMilliseconds(250), limiter.RetryAfter());
    }

    [Fact]
    public void Never_exceeds_the_published_rate_over_a_window()
    {
        (TokenBucketRateLimiter limiter, ManualClock clock) = Build(
            capacity: 5, refillPerSecond: 5);

        int granted = 0;

        // One second, in ten-millisecond steps, taking everything on offer.
        for (int step = 0; step < 100; step++)
        {
            while (limiter.TryAcquire())
            {
                granted++;
            }

            clock.Advance(TimeSpan.FromMilliseconds(10));
        }

        // The bucket started full and refilled for one second, so the most that
        // can lawfully have been granted is capacity plus one second's refill.
        Assert.True(granted <= 10, $"granted {granted}, which exceeds the published rate");
        Assert.True(granted >= 5, $"granted {granted}, so the burst was not even allowed");
    }
}
