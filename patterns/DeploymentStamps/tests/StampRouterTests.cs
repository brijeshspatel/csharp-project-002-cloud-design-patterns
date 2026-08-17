namespace DeploymentStamps.Tests;

/// <summary>
/// What deployment stamps guarantees: that a tenant lives in **exactly one**
/// complete, independent copy of the application — so one stamp failing costs
/// only its own tenants, and the others do not notice.
///
/// The stable home is the property everything else rests on. A tenant that
/// drifts between stamps has no isolation to speak of, and the arrangement has
/// become a badly replicated one instead.
/// </summary>
public class StampRouterTests
{
    private static readonly string[] Tenants =
        ["acme", "brightly", "cobalt", "delta", "everest", "foxtrot"];

    private static StampRouter ThreeStamps()
    {
        StampRouter router = new();
        router.AddStamp(new Stamp("stamp-1"));
        router.AddStamp(new Stamp("stamp-2"));
        router.AddStamp(new Stamp("stamp-3"));

        foreach (string tenant in Tenants)
        {
            router.Assign(tenant);
        }

        return router;
    }

    [Fact]
    public void Routes_a_tenant_to_its_own_stamp()
    {
        StampRouter router = ThreeStamps();

        string? answer = router.Send(new TenantRequest("acme", "an invoice"));

        Assert.NotNull(answer);
        Assert.StartsWith(router.HomeOf("acme")!.Name, answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Sends_the_same_tenant_to_the_same_stamp_every_time()
    {
        StampRouter router = ThreeStamps();
        string home = router.HomeOf("acme")!.Name;

        for (int attempt = 0; attempt < 50; attempt++)
        {
            Assert.Equal(home, router.HomeOf("acme")!.Name);
        }

        // A tenant's data lives in its stamp's own store. Drifting between
        // stamps would mean the data was somewhere else, which is a geode with
        // extra steps rather than a stamp.
        Assert.Equal(home, router.HomeOf("acme")!.Name);
    }

    [Fact]
    public void Keeps_tenants_on_other_stamps_serving_when_one_fails()
    {
        StampRouter router = ThreeStamps();
        Stamp failing = router.HomeOf("acme")!;

        failing.Fail();

        foreach (string tenant in Tenants)
        {
            if (router.HomeOf(tenant)!.Name == failing.Name)
            {
                continue;
            }

            Assert.NotNull(router.Send(new TenantRequest(tenant, "an invoice")));
        }
    }

    [Fact]
    public void Counts_the_tenants_a_stamp_failure_did_not_affect()
    {
        StampRouter router = ThreeStamps();
        Stamp failing = router.HomeOf("acme")!;

        failing.Fail();

        // Six tenants over three stamps: two lose service and four never learn
        // that anything happened. That ratio is the pattern's whole argument,
        // and it improves as stamps are added.
        Assert.Equal(4, router.TenantsUnaffectedBy(failing));
    }

    [Fact]
    public void Refuses_a_request_for_a_tenant_whose_stamp_is_down()
    {
        StampRouter router = ThreeStamps();
        Stamp failing = router.HomeOf("acme")!;
        failing.Fail();

        // Refused rather than served elsewhere. Another stamp has none of this
        // tenant's data, so "failing over" would answer with somebody else's
        // world — which is worse than an honest refusal.
        Assert.Null(router.Send(new TenantRequest("acme", "an invoice")));
    }

    [Fact]
    public void Adds_a_stamp_without_moving_existing_tenants()
    {
        StampRouter router = ThreeStamps();
        Dictionary<string, string> before = Tenants.ToDictionary(t => t, t => router.HomeOf(t)!.Name);

        router.AddStamp(new Stamp("stamp-4"));
        router.Assign("gamma");

        // Scale is achieved by adding stamps, and adding one must not migrate
        // anybody — migration means moving a data store, which is a project.
        foreach (string tenant in Tenants)
        {
            Assert.Equal(before[tenant], router.HomeOf(tenant)!.Name);
        }

        Assert.Equal("stamp-4", router.HomeOf("gamma")!.Name);
    }
}
