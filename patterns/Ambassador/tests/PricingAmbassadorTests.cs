namespace Ambassador.Tests;

/// <summary>
/// What an ambassador guarantees: that the application makes **one call** while
/// the helper makes as many attempts as the network requires — and that the
/// application cannot tell which happened.
///
/// The point is where the retry logic lives, not that retries exist. So the
/// tests assert the asymmetry between calls and attempts, and that a quote
/// obtained after a retry is indistinguishable from one obtained first time.
/// </summary>
public class PricingAmbassadorTests
{
    private static readonly DateTimeOffset Noon =
        new(2026, 8, 18, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan Backoff = TimeSpan.FromMilliseconds(200);

    private static PricingAmbassador Over(PricingBackend backend, int attemptLimit = 3) =>
        new(backend, new ManualClock(Noon), attemptLimit, Backoff);

    private static PricingBackend Answers(Func<int, bool> answersOnAttempt) =>
        new("pricing", answersOnAttempt);

    [Fact]
    public void Returns_a_quote_the_backend_answered_first_time()
    {
        PricingBackend backend = Answers(_ => true);
        PricingAmbassador ambassador = Over(backend);

        PriceQuote? quote = ambassador.Ask("SKU-1042");

        Assert.Equal(549.00m, quote?.Price);
        Assert.Equal(1, backend.Attempts);
        Assert.Equal(TimeSpan.Zero, ambassador.TotalBackoff);
    }

    [Fact]
    public void Retries_a_backend_that_fails_once()
    {
        PricingBackend backend = Answers(attempt => attempt > 1);
        PricingAmbassador ambassador = Over(backend);

        PriceQuote? quote = ambassador.Ask("SKU-1042");

        Assert.NotNull(quote);
        Assert.Equal(2, backend.Attempts);
        Assert.Equal(Backoff, ambassador.TotalBackoff);
    }

    [Fact]
    public void Gives_up_after_the_attempt_limit()
    {
        PricingBackend backend = Answers(_ => false);
        PricingAmbassador ambassador = Over(backend, attemptLimit: 3);

        PriceQuote? quote = ambassador.Ask("SKU-1042");

        // Exactly three, not four and not for ever. A helper that retried
        // without a bound would hold the application's thread indefinitely,
        // which is worse than the failure it was hiding.
        Assert.Null(quote);
        Assert.Equal(3, backend.Attempts);
    }

    [Fact]
    public void Counts_the_attempts_the_application_did_not_make()
    {
        PricingBackend backend = Answers(attempt => attempt > 2);
        PricingAmbassador ambassador = Over(backend);

        ambassador.Ask("SKU-1042");

        // One call in, three attempts out: two the application neither made nor
        // knew about. That difference is the whole of what was moved.
        Assert.Equal(1, ambassador.CallsFromApplication);
        Assert.Equal(3, ambassador.AttemptsMade);
        Assert.Equal(2, ambassador.AttemptsAbsorbed);
    }

    [Fact]
    public void Leaves_the_application_unaware_of_the_retry()
    {
        PriceQuote? firstTime = Over(Answers(_ => true)).Ask("SKU-1042");
        PriceQuote? afterRetries = Over(Answers(attempt => attempt > 2)).Ask("SKU-1042");

        // Byte-identical results. The application has no way to discover that
        // one of these took three attempts, which is what lets retry policy
        // change without the application being redeployed.
        Assert.Equal(firstTime, afterRetries);
    }

    [Fact]
    public void Reports_a_backend_that_never_answers()
    {
        PricingBackend backend = Answers(_ => false);
        PricingAmbassador ambassador = Over(backend, attemptLimit: 2);

        ambassador.Ask("SKU-1042");

        // The application gets nothing; the ambassador knows why and can say
        // so to an operator. Silence in both directions would be the worst of
        // both worlds.
        Assert.Contains("2 attempt", ambassador.LastFailure, StringComparison.Ordinal);
    }
}
