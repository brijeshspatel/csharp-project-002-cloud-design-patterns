namespace GatewayRouting.Tests;

/// <summary>
/// What gateway routing guarantees: that clients see **one endpoint** while
/// several services sit behind it, and that which service handles a request is
/// the gateway's decision rather than the client's knowledge.
///
/// The refusal matters as much as the routing. A gateway that quietly sends an
/// unmatched path somewhere plausible turns a typo into a request against the
/// wrong service, which is worse than a clean rejection.
/// </summary>
public class RequestGatewayTests
{
    private static (RequestGateway Gateway, BackendService Orders, BackendService Catalogue) Assemble()
    {
        BackendService orders = new("orders-v1");
        BackendService ordersV2 = new("orders-v2");
        BackendService catalogue = new("catalogue");

        RouteTable routes = new();
        routes.Add("/v1/orders", orders);
        routes.Add("/v2/orders", ordersV2);
        routes.Add("/catalogue", catalogue);

        return (new RequestGateway(routes), orders, catalogue);
    }

    [Fact]
    public void Routes_a_request_to_the_service_that_owns_the_path()
    {
        (RequestGateway gateway, BackendService orders, _) = Assemble();

        ServiceResponse response = gateway.Send(new ClientRequest("/v1/orders/1042", string.Empty));

        Assert.True(response.Found);
        Assert.Equal("orders-v1", response.ServedBy);
        Assert.Equal(1, orders.Handled);
    }

    [Fact]
    public void Routes_a_versioned_path_to_a_different_service()
    {
        (RequestGateway gateway, BackendService orders, _) = Assemble();

        ServiceResponse response = gateway.Send(new ClientRequest("/v2/orders/1042", string.Empty));

        // Same resource, same client, different implementation behind it. The
        // client's URL changed; nothing else had to.
        Assert.Equal("orders-v2", response.ServedBy);
        Assert.Equal(0, orders.Handled);
    }

    [Fact]
    public void Refuses_a_path_no_route_matches()
    {
        (RequestGateway gateway, BackendService orders, BackendService catalogue) = Assemble();

        ServiceResponse response = gateway.Send(new ClientRequest("/invoices/9", string.Empty));

        // Refused, and — the part that matters — no service saw it. A gateway
        // that falls through to something plausible turns a typo into a request
        // against the wrong service.
        Assert.False(response.Found);
        Assert.Equal(0, orders.Handled);
        Assert.Equal(0, catalogue.Handled);
    }

    [Fact]
    public void Matches_whole_path_segments_rather_than_raw_prefixes()
    {
        (RequestGateway gateway, _, BackendService catalogue) = Assemble();

        // /cataloguesale begins with the characters of /catalogue and belongs
        // to no real resource. Raw prefix matching would route it; segment
        // matching refuses it, and still owns the genuine sub-paths.
        ServiceResponse lookalike = gateway.Send(new ClientRequest("/cataloguesale", string.Empty));
        ServiceResponse genuine = gateway.Send(new ClientRequest("/catalogue/items/42", string.Empty));

        Assert.False(lookalike.Found);
        Assert.True(genuine.Found);
        Assert.Equal(1, catalogue.Handled);
    }

    [Fact]
    public void Reports_the_routes_it_holds()
    {
        (RequestGateway gateway, _, _) = Assemble();

        Assert.Equal(["/v1/orders", "/v2/orders", "/catalogue"], gateway.Routes);
    }

    [Fact]
    public void Adds_a_service_without_changing_the_client()
    {
        BackendService offers = new("offers");
        RouteTable routes = new();
        routes.Add("/catalogue", new BackendService("catalogue"));
        RequestGateway gateway = new(routes);

        // Before: nothing serves offers.
        Assert.False(gateway.Send(new ClientRequest("/offers", string.Empty)).Found);

        routes.Add("/offers", offers);

        // After: the same call reaches a service that did not exist a moment
        // ago, and no client was redeployed to make that true.
        Assert.Equal("offers", gateway.Send(new ClientRequest("/offers", string.Empty)).ServedBy);
    }
}
