namespace RateLimiting;

/// <summary>
/// A client of a third-party geocoding API that publishes a limit of ten
/// requests per second.
///
/// The client's job is to never find out what happens when you exceed it.
/// </summary>
public sealed class GeocodingClient
{
    private readonly TokenBucketRateLimiter limiter;
    private int calls;

    /// <summary>Creates a client paced by <paramref name="limiter"/>.</summary>
    public GeocodingClient(TokenBucketRateLimiter limiter)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        this.limiter = limiter;
    }

    /// <summary>Calls actually sent to the remote API.</summary>
    public int Calls => calls;

    /// <summary>
    /// Geocodes an address if the limiter permits, otherwise reports how long
    /// the caller should wait.
    /// </summary>
    public GeocodeOutcome Geocode(string address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(address);

        if (!limiter.TryAcquire())
        {
            return new GeocodeOutcome(false, null, limiter.RetryAfter());
        }

        calls++;
        return new GeocodeOutcome(true, $"51.5074, -0.1278 ({address})", TimeSpan.Zero);
    }
}

/// <summary>What came of a geocoding attempt.</summary>
/// <param name="Sent">Whether the call was actually made.</param>
/// <param name="Coordinates">The result, where one was obtained.</param>
/// <param name="RetryAfter">How long to wait, where it was not.</param>
public readonly record struct GeocodeOutcome(
    bool Sent, string? Coordinates, TimeSpan RetryAfter);
