namespace Geode.Tests;

/// <summary>
/// What a geode network guarantees: that **any node can serve any request**,
/// because every node holds the same data — so a client is served by the
/// nearest one and loses nothing when that one is gone.
///
/// This is the opposite of Deployment Stamps, and the tests are shaped to make
/// the difference impossible to miss: the same request is asked of every node
/// and every node answers it. A stamped arrangement could not pass that test,
/// and this one could not pass a stamp's isolation test.
/// </summary>
public class GeodeNetworkTests
{
    private sealed record Cast(GeodeNetwork Network, GeodeNode Europe, GeodeNode Asia, GeodeNode America);

    private static Cast Assemble()
    {
        GeodeNode europe = new("geode-eu", "europe");
        GeodeNode asia = new("geode-ap", "asia");
        GeodeNode america = new("geode-us", "america");

        GeodeNetwork network = new();
        network.AddNode(europe);
        network.AddNode(asia);
        network.AddNode(america);
        network.Write("catalogue/sku-77", "Standing Desk");

        return new Cast(network, europe, asia, america);
    }

    [Fact]
    public void Serves_a_request_from_the_nearest_node()
    {
        Cast cast = Assemble();

        GeodeResponse response = cast.Network.Read(new ClientLocation("asia"), "catalogue/sku-77");

        Assert.Equal("geode-ap", response.ServedBy);
        Assert.Equal("Standing Desk", response.Value);
    }

    [Fact]
    public void Serves_the_same_request_from_any_node()
    {
        Cast cast = Assemble();

        foreach (string region in new[] { "europe", "asia", "america" })
        {
            GeodeResponse response = cast.Network.Read(new ClientLocation(region), "catalogue/sku-77");
            Assert.Equal("Standing Desk", response.Value);
        }

        // Every node could have answered any of those. That is the whole
        // difference from a stamped arrangement, where a request has exactly
        // one home and no other copy holds its data at all.
        Assert.Equal(3, cast.Network.NodesThatCouldServe("catalogue/sku-77"));
    }

    [Fact]
    public void Replicates_a_write_to_every_node()
    {
        Cast cast = Assemble();

        cast.Network.Write("catalogue/sku-77", "Standing Desk, Walnut");

        Assert.Equal("Standing Desk, Walnut", cast.Europe.Read("catalogue/sku-77"));
        Assert.Equal("Standing Desk, Walnut", cast.Asia.Read("catalogue/sku-77"));
        Assert.Equal("Standing Desk, Walnut", cast.America.Read("catalogue/sku-77"));
    }

    [Fact]
    public void Reads_stale_data_from_a_node_that_has_not_caught_up()
    {
        Cast cast = Assemble();

        cast.Asia.Lag();
        cast.Network.Write("catalogue/sku-77", "Standing Desk, Walnut");

        // Asia is still serving the old value, and serving it confidently. This
        // is the price of every node holding everything, and it is asserted
        // rather than hidden.
        Assert.Equal("Standing Desk", cast.Network.Read(new ClientLocation("asia"), "catalogue/sku-77").Value);
        Assert.Equal("Standing Desk, Walnut", cast.Network.Read(new ClientLocation("europe"), "catalogue/sku-77").Value);
    }

    [Fact]
    public void Serves_from_another_node_when_the_nearest_is_unavailable()
    {
        Cast cast = Assemble();

        cast.Asia.Fail();
        GeodeResponse response = cast.Network.Read(new ClientLocation("asia"), "catalogue/sku-77");

        // A different node, the same answer. A stamped arrangement refuses here
        // because no other copy holds the data; this one does not have to.
        Assert.True(response.Found);
        Assert.NotEqual("geode-ap", response.ServedBy);
        Assert.Equal("Standing Desk", response.Value);
    }

    [Fact]
    public void Reports_which_node_served_each_request()
    {
        Cast cast = Assemble();

        Assert.Equal("geode-eu", cast.Network.Read(new ClientLocation("europe"), "catalogue/sku-77").ServedBy);
        Assert.Equal("geode-us", cast.Network.Read(new ClientLocation("america"), "catalogue/sku-77").ServedBy);
    }
}
