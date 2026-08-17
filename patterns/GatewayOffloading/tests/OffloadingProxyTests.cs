namespace GatewayOffloading.Tests;

/// <summary>
/// What gateway offloading guarantees: that cross-cutting work is implemented
/// **once at the edge instead of once per service**, and that the services
/// behind it genuinely do not contain it.
///
/// The second half is the one worth testing. A proxy that validates tokens
/// while every service also validates tokens has offloaded nothing; it has
/// added a hop. So the test that matters asks the service directly, with no
/// token at all, and expects it to answer.
/// </summary>
public class OffloadingProxyTests
{
    private const string GoodToken = "token-valid";

    private static (OffloadingProxy Proxy, ApplicationService Orders) Assemble()
    {
        ApplicationService orders = new("orders");
        OffloadingProxy proxy = new();
        proxy.Register("/orders", orders);
        proxy.Register("/catalogue", new ApplicationService("catalogue"));
        proxy.Register("/accounts", new ApplicationService("accounts"));
        return (proxy, orders);
    }

    [Fact]
    public void Validates_a_token_before_the_service_is_reached()
    {
        (OffloadingProxy proxy, ApplicationService orders) = Assemble();

        EdgeResult result = proxy.Send(new EdgeRequest("/orders", GoodToken, "list"));

        Assert.True(result.Allowed);
        Assert.Equal(1, orders.Handled);
    }

    [Fact]
    public void Refuses_a_request_with_no_valid_token()
    {
        (OffloadingProxy proxy, ApplicationService orders) = Assemble();

        EdgeResult result = proxy.Send(new EdgeRequest("/orders", "token-expired", "list"));

        // Refused, and the service never saw it — so every service behind the
        // proxy is spared both the code and the traffic.
        Assert.False(result.Allowed);
        Assert.Equal(0, orders.Handled);
    }

    [Fact]
    public void Compresses_a_response_the_service_returned_plain()
    {
        (OffloadingProxy proxy, ApplicationService orders) = Assemble();

        string plain = orders.Handle(new EdgeRequest("/orders", string.Empty, "list"));
        EdgeResult result = proxy.Send(new EdgeRequest("/orders", GoodToken, "list"));

        // The service returned plain text; the compressed form appeared at the
        // edge, which is the only place that knows what the client accepts.
        Assert.DoesNotContain("gzip", plain, StringComparison.Ordinal);
        Assert.True(result.Compressed);
        Assert.Contains("gzip", result.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Leaves_the_service_holding_no_cross_cutting_code()
    {
        ApplicationService service = new("orders");

        // Called directly with no token at all, it answers — because
        // validating tokens is not its job and it does not know how. A service
        // that refused here would be one that had kept the code the proxy is
        // supposed to have taken.
        string body = service.Handle(new EdgeRequest("/orders", string.Empty, "list"));

        Assert.NotEmpty(body);
        Assert.Equal(1, service.Handled);
    }

    [Fact]
    public void Counts_the_implementations_the_services_no_longer_need()
    {
        (OffloadingProxy proxy, _) = Assemble();

        // Two concerns across three services would be six implementations; done
        // once at the edge it is two, so four were avoided. In process the code
        // costs nothing to run either way, so the saving is only visible as a
        // count of implementations that do not exist.
        Assert.Equal(2, proxy.ConcernsAtEdge);
        Assert.Equal(3, proxy.ServicesBehind);
        Assert.Equal(4, proxy.ImplementationsAvoided);
    }
}
