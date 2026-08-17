namespace ExternalConfigurationStore.Tests;

/// <summary>
/// What an external configuration store guarantees: that a setting changes in
/// **one place** and takes effect everywhere, with **no deployment and no
/// restart**.
///
/// And the cost, asserted just as plainly: a bad value reaches every instance
/// exactly as fast as a good one. Speed of propagation is not a benefit that
/// only applies to correct changes.
/// </summary>
public class ConfigurationStoreTests
{
    private static readonly Dictionary<string, string> Packaged = new()
    {
        ["page-size"] = "20",
    };

    private sealed record Fleet(ConfigurationStore Store, IReadOnlyList<ApplicationInstance> Instances);

    private static Fleet FourInstances()
    {
        ConfigurationStore store = new();
        store.Set("feature.new-checkout", "off");

        List<ApplicationInstance> instances = [];
        foreach (string name in new[] { "web-1", "web-2", "web-3", "web-4" })
        {
            instances.Add(new ApplicationInstance(name, store, Packaged));
            store.Register(name);
        }

        return new Fleet(store, instances);
    }

    [Fact]
    public void Serves_a_setting_from_the_store_rather_than_the_package()
    {
        Fleet fleet = FourInstances();

        Assert.Equal("off", fleet.Instances[0].Read("feature.new-checkout"));
    }

    [Fact]
    public void Applies_a_change_to_every_instance()
    {
        Fleet fleet = FourInstances();

        // Every instance has been running and serving the old value — which is
        // the only state in which "does a change reach it?" means anything. An
        // instance that has never read cannot have cached the wrong answer.
        Assert.All(fleet.Instances, instance => Assert.Equal("off", instance.Read("feature.new-checkout")));

        fleet.Store.Set("feature.new-checkout", "on");

        // One change, four instances, no deployment. Every instance is reading
        // the store rather than remembering what it was told at startup.
        Assert.All(fleet.Instances, instance => Assert.Equal("on", instance.Read("feature.new-checkout")));
    }

    [Fact]
    public void Counts_the_restarts_the_change_avoided()
    {
        Fleet fleet = FourInstances();

        fleet.Store.Set("feature.new-checkout", "on");

        // Four instances that would each have needed a restart, and none did.
        Assert.Equal(4, fleet.Store.RestartsAvoided);
        Assert.All(fleet.Instances, instance => Assert.Equal(0, instance.Restarts));
    }

    [Fact]
    public void Applies_a_bad_setting_to_every_instance_too()
    {
        Fleet fleet = FourInstances();

        fleet.Store.Set("feature.new-checkout", "obviously-wrong");

        // The same mechanism, the same speed, the whole fleet. A store that
        // propagates good changes in seconds propagates bad ones in seconds.
        Assert.All(
            fleet.Instances,
            instance => Assert.Equal("obviously-wrong", instance.Read("feature.new-checkout")));
    }

    [Fact]
    public void Reports_an_instance_that_has_stopped_reading_as_behind()
    {
        Fleet fleet = FourInstances();
        fleet.Store.Set("feature.new-checkout", "on");

        ApplicationInstance current = fleet.Instances[0];
        ApplicationInstance stalled = fleet.Instances[1];
        current.Read("feature.new-checkout");
        stalled.Read("feature.new-checkout");

        // The store moves on; only one instance keeps reading.
        fleet.Store.Set("feature.new-checkout", "off");
        current.Read("feature.new-checkout");

        // The stalled instance reports an older version - the drift the
        // version exists to reveal, visible instead of assumed. Without the
        // stamp-at-read this could never fail, because the report would just
        // proxy the store.
        Assert.Equal(fleet.Store.Version, current.ConfigurationVersion);
        Assert.Equal(fleet.Store.Version - 1, stalled.ConfigurationVersion);
    }

    [Fact]
    public void Falls_back_to_a_packaged_default_when_the_store_has_no_value()
    {
        Fleet fleet = FourInstances();

        // The store holds no page size. Without a packaged default the
        // application would be unable to start when the store is empty or
        // unreachable — so the package keeps one, and the store overrides it.
        Assert.Equal("20", fleet.Instances[0].Read("page-size"));
        Assert.Null(fleet.Instances[0].Read("nothing-anywhere"));
    }
}
