namespace GatewayAggregation;

/// <summary>
/// One screen's worth of data, assembled from four services.
/// </summary>
/// <param name="CustomerId">Whose screen.</param>
/// <param name="Profile">Their name, from the profile service.</param>
/// <param name="Orders">Recent orders.</param>
/// <param name="Offers">Offers, which may legitimately be empty.</param>
/// <param name="BasketItems">How many things are in the basket.</param>
/// <param name="Unavailable">**Which backends did not answer** — never silently omitted.</param>
public readonly record struct HomeScreen(
    string CustomerId,
    string? Profile,
    IReadOnlyList<string> Orders,
    IReadOnlyList<string> Offers,
    int BasketItems,
    IReadOnlyList<string> Unavailable);

/// <summary>Who the customer is. The screen cannot render without it.</summary>
public sealed class ProfileService
{
    private readonly Dictionary<string, string> names = new()
    {
        ["CUST-1"] = "Ada Lovelace",
        ["CUST-2"] = "Ben Nichols",
    };

    /// <summary>How many times it has been called.</summary>
    public int Calls { get; private set; }

    /// <summary>The customer's name, or nothing where there is no such customer.</summary>
    public string? NameOf(string customerId)
    {
        Calls++;
        return names.TryGetValue(customerId, out string? name) ? name : null;
    }
}

/// <summary>Recent orders. Optional to the screen: an empty list renders fine.</summary>
public sealed class OrderService
{
    /// <summary>How many times it has been called.</summary>
    public int Calls { get; private set; }

    /// <summary>Recent orders for a customer.</summary>
    public IReadOnlyList<string> RecentFor(string customerId)
    {
        Calls++;
        return [$"{customerId}-ORD-1042", $"{customerId}-ORD-1043"];
    }
}

/// <summary>
/// Offers, and the one that can be unhealthy — because a screen with a failing
/// optional panel is the interesting case, not the happy one.
/// </summary>
public sealed class OfferService
{
    private readonly bool healthy;

    /// <summary>Creates a service that answers, or does not.</summary>
    public OfferService(bool healthy = true) => this.healthy = healthy;

    /// <summary>How many times it has been called, answering or not.</summary>
    public int Calls { get; private set; }

    /// <summary>Offers for a customer.</summary>
    /// <exception cref="InvalidOperationException">The service is unavailable.</exception>
    public IReadOnlyList<string> For(string customerId)
    {
        Calls++;

        if (!healthy)
        {
            throw new InvalidOperationException("the offers service is unavailable");
        }

        return [$"10% off for {customerId}"];
    }
}

/// <summary>What is in the basket.</summary>
public sealed class BasketService
{
    /// <summary>How many times it has been called.</summary>
    public int Calls { get; private set; }

    /// <summary>How many items the customer has in their basket.</summary>
    public int CountFor(string customerId)
    {
        Calls++;
        return customerId.Length % 5;
    }
}

/// <summary>
/// Assembles one screen from four services, so the client makes **one** request
/// instead of four.
///
/// The saving is not computational — the same four calls happen, and the
/// gateway makes them. It is that they happen **on the fast side of the slow
/// link**: a mobile client on a poor connection pays four round trips of
/// latency and four TLS handshakes, and the gateway pays four calls inside a
/// data centre.
///
/// **A failing optional backend degrades the screen; it does not lose it.** And
/// the failure is reported rather than omitted — silently returning an empty
/// offers list tells the client the customer has no offers, which is a
/// different and wrong statement.
///
/// **What this is not.** Gateway Routing forwards one request to one service;
/// this knows exactly which four to call and calls all of them. Gateway
/// Offloading does work the services would otherwise each implement; this does
/// no part of their work. Gatekeeper exists to make back ends unreachable.
/// Backends for Frontends would give the mobile and desktop clients different
/// backends rather than one aggregating endpoint.
/// </summary>
public sealed class AggregatingGateway
{
    private readonly ProfileService profiles;
    private readonly OrderService orders;
    private readonly OfferService offers;
    private readonly BasketService baskets;

    /// <summary>Creates a gateway over the four services a home screen needs.</summary>
    public AggregatingGateway(
        ProfileService profiles,
        OrderService orders,
        OfferService offers,
        BasketService baskets)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(offers);
        ArgumentNullException.ThrowIfNull(baskets);

        this.profiles = profiles;
        this.orders = orders;
        this.offers = offers;
        this.baskets = baskets;
    }

    /// <summary>How many requests the client made.</summary>
    public int ClientRoundTrips { get; private set; }

    /// <summary>How many calls the gateway made on the client's behalf.</summary>
    public int BackendCalls { get; private set; }

    /// <summary>What the client was spared — the benefit, as a number.</summary>
    public int RoundTripsSaved => BackendCalls - ClientRoundTrips;

    /// <summary>
    /// Builds one screen. Returns nothing where the customer does not exist,
    /// which is the one failure that is not partial.
    /// </summary>
    public HomeScreen? Fetch(string customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        ClientRoundTrips++;

        BackendCalls++;
        string? name = profiles.NameOf(customerId);

        // No customer, no screen. The remaining backends are not asked, because
        // there is nothing for them to answer about.
        if (name is null)
        {
            return null;
        }

        List<string> unavailable = [];

        BackendCalls++;
        IReadOnlyList<string> recent = orders.RecentFor(customerId);

        BackendCalls++;
        IReadOnlyList<string> available;
        try
        {
            available = offers.For(customerId);
        }
        catch (InvalidOperationException)
        {
            // Caught narrowly and named: an optional panel failing must not
            // cost the screen, and must not be reported as "no offers".
            available = [];
            unavailable.Add("offers");
        }

        BackendCalls++;
        int basketItems = baskets.CountFor(customerId);

        return new HomeScreen(customerId, name, recent, available, basketItems, unavailable);
    }
}
