namespace Ambassador;

/// <summary>What the pricing service says a product costs.</summary>
/// <param name="Sku">Which product.</param>
/// <param name="Price">What it costs.</param>
/// <param name="Found">Whether there is a price at all.</param>
public readonly record struct PriceQuote(string Sku, decimal Price, bool Found);

/// <summary>Time, so backoff can be reasoned about without waiting for it.</summary>
public interface IClock
{
    /// <summary>The current instant.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>A clock that moves only when told to.</summary>
public sealed class ManualClock : IClock
{
    /// <summary>Creates a clock reading <paramref name="start"/>.</summary>
    public ManualClock(DateTimeOffset start) => UtcNow = start;

    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
}

/// <summary>
/// A remote service across a network that sometimes does not answer.
///
/// Its unreliability is the reason the ambassador exists, and it is
/// deterministic here so the tests are: an attempt either answers or does not,
/// according to which attempt it is.
/// </summary>
public sealed class PricingBackend
{
    private readonly Func<int, bool> answersOnAttempt;

    /// <summary>Creates a backend that answers, or does not, per attempt.</summary>
    public PricingBackend(string name, Func<int, bool> answersOnAttempt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(answersOnAttempt);

        Name = name;
        this.answersOnAttempt = answersOnAttempt;
    }

    /// <summary>What this service is called.</summary>
    public string Name { get; }

    /// <summary>How many times it has been asked, answering or not.</summary>
    public int Attempts { get; private set; }

    /// <summary>A quote, or nothing where the call did not come back.</summary>
    public PriceQuote? Quote(string sku)
    {
        Attempts++;
        return answersOnAttempt(Attempts) ? new PriceQuote(sku, 549.00m, Found: true) : null;
    }
}

/// <summary>
/// The ambassador: a helper that makes outbound calls **on the application's
/// behalf**, holding the retry policy, the attempt limit and the backoff.
///
/// The application asks once and gets an answer or nothing. It does not know
/// whether that took one attempt or three, and that ignorance is the product:
/// retry policy becomes an operational concern that can be tuned, standardised
/// across services written in different languages, and changed without any
/// application being redeployed.
///
/// **Retries are bounded.** A helper that retried indefinitely would hold the
/// caller's thread for ever, which is worse than the failure it was hiding.
///
/// **On its relationship to Sidecar.** An ambassador is conventionally *deployed
/// as* a sidecar — same host, same lifecycle, separate process. That is not
/// concealed here: Sidecar is the deployment shape, and this is one job that
/// shape does. The two are separate patterns because a sidecar may collect
/// metrics or supply configuration and never make an outbound call, and an
/// ambassador's logic is the same whether it is deployed alongside or as a
/// shared proxy.
///
/// **What this is not.** Retry, in tier 1, is the policy itself — what to retry,
/// how often, with what backoff. This pattern is about *where that policy
/// lives*: outside the application, in a component the application does not
/// configure.
/// </summary>
public sealed class PricingAmbassador
{
    private readonly PricingBackend backend;
    private readonly IClock clock;
    private readonly int attemptLimit;
    private readonly TimeSpan backoff;

    /// <summary>Creates an ambassador in front of <paramref name="backend"/>.</summary>
    public PricingAmbassador(
        PricingBackend backend,
        IClock clock,
        int attemptLimit,
        TimeSpan backoff)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptLimit);

        this.backend = backend;
        this.clock = clock;
        this.attemptLimit = attemptLimit;
        this.backoff = backoff;
    }

    /// <summary>How many times the application asked.</summary>
    public int CallsFromApplication { get; private set; }

    /// <summary>How many times the network was actually attempted.</summary>
    public int AttemptsMade { get; private set; }

    /// <summary>What the application never had to do — the benefit, as a number.</summary>
    public int AttemptsAbsorbed => AttemptsMade - CallsFromApplication;

    /// <summary>How long the ambassador would have waited between attempts.</summary>
    public TimeSpan TotalBackoff { get; private set; }

    /// <summary>When the last attempt was made, for an operator's benefit.</summary>
    public DateTimeOffset LastAttemptAt { get; private set; }

    /// <summary>Why the last call failed, where it did.</summary>
    public string LastFailure { get; private set; } = string.Empty;

    /// <summary>
    /// One call from the application's point of view. Retries up to the attempt
    /// limit; returns the first answer, or nothing.
    /// </summary>
    public PriceQuote? Ask(string sku)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        CallsFromApplication++;

        // Bounded by construction: the loop cannot run more than attemptLimit
        // times, so a permanently dead backend fails fast rather than hanging.
        for (int attempt = 1; attempt <= attemptLimit; attempt++)
        {
            AttemptsMade++;
            LastAttemptAt = clock.UtcNow;

            if (backend.Quote(sku) is { } quote)
            {
                LastFailure = string.Empty;
                return quote;
            }

            // The wait is recorded rather than slept: a test that sleeps is a
            // slow test, and a demonstration that sleeps teaches nothing extra.
            if (attempt < attemptLimit)
            {
                TotalBackoff += backoff;
            }
        }

        LastFailure = $"{backend.Name} did not answer in {attemptLimit} attempt(s)";
        return null;
    }
}
