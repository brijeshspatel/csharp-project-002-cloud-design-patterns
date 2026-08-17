namespace GatewayAggregation.Tests;

/// <summary>
/// What gateway aggregation guarantees: that a screen needing four backends
/// costs the client **one round trip instead of four**, and that one backend
/// failing degrades the screen rather than losing it.
///
/// The saving is asserted as a count. In process four method calls cost the
/// same as one; the whole value of the pattern is on a link where they do not,
/// so the only honest way to show it is to count what the client did not have
/// to do.
/// </summary>
public class AggregatingGatewayTests
{
    private sealed record Cast(
        AggregatingGateway Gateway,
        ProfileService Profiles,
        OrderService Orders,
        OfferService Offers,
        BasketService Baskets);

    private static Cast Assemble(bool offersHealthy = true)
    {
        ProfileService profiles = new();
        OrderService orders = new();
        OfferService offers = new(healthy: offersHealthy);
        BasketService baskets = new();
        return new Cast(new AggregatingGateway(profiles, orders, offers, baskets), profiles, orders, offers, baskets);
    }

    [Fact]
    public void Answers_a_home_screen_in_one_client_round_trip()
    {
        Cast cast = Assemble();

        HomeScreen? screen = cast.Gateway.Fetch("CUST-1");

        Assert.NotNull(screen);
        Assert.Equal(1, cast.Gateway.ClientRoundTrips);
    }

    [Fact]
    public void Calls_every_backend_the_screen_needs()
    {
        Cast cast = Assemble();

        cast.Gateway.Fetch("CUST-1");

        Assert.Equal(1, cast.Profiles.Calls);
        Assert.Equal(1, cast.Orders.Calls);
        Assert.Equal(1, cast.Offers.Calls);
        Assert.Equal(1, cast.Baskets.Calls);
    }

    [Fact]
    public void Returns_what_succeeded_when_one_backend_fails()
    {
        Cast cast = Assemble(offersHealthy: false);

        HomeScreen? screen = cast.Gateway.Fetch("CUST-1");

        // The offers panel is empty and everything else rendered. Aborting the
        // whole screen because one optional panel failed is the defect this
        // asserts against.
        Assert.NotNull(screen);
        Assert.Equal("Ada Lovelace", screen?.Profile);
        Assert.NotEmpty(screen?.Orders ?? []);
        Assert.Empty(screen?.Offers ?? ["placeholder"]);
    }

    [Fact]
    public void Reports_which_backend_failed()
    {
        Cast cast = Assemble(offersHealthy: false);

        HomeScreen? screen = cast.Gateway.Fetch("CUST-1");

        // Silently returning a partial screen teaches the client that the
        // customer has no offers, which is a different and wrong statement.
        Assert.Equal(["offers"], screen?.Unavailable);
    }

    [Fact]
    public void Counts_the_round_trips_the_client_did_not_make()
    {
        Cast cast = Assemble();

        cast.Gateway.Fetch("CUST-1");

        // Four backend calls, one client round trip: three the client did not
        // make over whatever link it is on.
        Assert.Equal(4, cast.Gateway.BackendCalls);
        Assert.Equal(1, cast.Gateway.ClientRoundTrips);
        Assert.Equal(3, cast.Gateway.RoundTripsSaved);
    }

    [Fact]
    public void Reports_nothing_for_an_unknown_customer()
    {
        Cast cast = Assemble();

        HomeScreen? screen = cast.Gateway.Fetch("CUST-UNKNOWN");

        // No profile means no screen, and the remaining backends are not asked.
        Assert.Null(screen);
        Assert.Equal(0, cast.Orders.Calls);
    }
}
