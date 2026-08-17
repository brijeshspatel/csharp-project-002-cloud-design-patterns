namespace Quarantine.Tests;

/// <summary>
/// What quarantine guarantees: that an external asset is checked **before the
/// workload can consume it**, and that anything failing a check never reaches
/// the registry the workload reads.
///
/// The order is the pattern. Scanning after adoption is monitoring — useful,
/// and a different thing entirely. The test that matters most asserts what the
/// workload can see, not what the gate decided.
/// </summary>
public class QuarantineGateTests
{
    private static readonly string[] AllowedLicences = ["MIT", "Apache-2.0", "BSD-3-Clause"];

    private sealed record Cast(QuarantineGate Gate, AssetRegistry Registry);

    private static Cast Assemble()
    {
        AssetRegistry registry = new();
        return new Cast(new QuarantineGate(registry, AllowedLicences), registry);
    }

    private static ExternalAsset Package(
        string name,
        string licence = "MIT",
        bool vulnerable = false,
        bool scanned = true) =>
        new(name, "1.4.2", licence, vulnerable, scanned);

    [Fact]
    public void Admits_an_asset_that_passes_every_check()
    {
        Cast cast = Assemble();

        AssetVerdict verdict = cast.Gate.Submit(Package("serilog"));

        Assert.True(verdict.Admitted);
        Assert.True(cast.Registry.Contains("serilog"));
    }

    [Fact]
    public void Refuses_an_asset_that_fails_a_check()
    {
        Cast cast = Assemble();

        Assert.False(cast.Gate.Submit(Package("left-pad", vulnerable: true)).Admitted);
        Assert.False(cast.Gate.Submit(Package("some-gpl-thing", licence: "GPL-3.0")).Admitted);
    }

    [Fact]
    public void Never_admits_a_refused_asset_to_the_workload()
    {
        Cast cast = Assemble();

        cast.Gate.Submit(Package("left-pad", vulnerable: true));
        cast.Gate.Submit(Package("some-gpl-thing", licence: "GPL-3.0"));
        cast.Gate.Submit(Package("unscanned-thing", scanned: false));

        // The registry is what the workload reads. Reporting a refusal while
        // the asset is already consumable would be monitoring wearing
        // quarantine's name.
        Assert.Empty(cast.Registry.Admitted);
    }

    [Fact]
    public void Counts_the_assets_refused_before_use()
    {
        Cast cast = Assemble();

        cast.Gate.Submit(Package("serilog"));
        cast.Gate.Submit(Package("left-pad", vulnerable: true));
        cast.Gate.Submit(Package("some-gpl-thing", licence: "GPL-3.0"));

        Assert.Equal(2, cast.Gate.Refused);
        Assert.Single(cast.Registry.Admitted);
    }

    [Fact]
    public void Reports_which_check_an_asset_failed()
    {
        Cast cast = Assemble();

        Assert.Contains(
            "vulnerability",
            cast.Gate.Submit(Package("left-pad", vulnerable: true)).Reason,
            StringComparison.Ordinal);

        Assert.Contains(
            "licence",
            cast.Gate.Submit(Package("some-gpl-thing", licence: "GPL-3.0")).Reason,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_an_asset_that_has_not_been_checked_at_all()
    {
        Cast cast = Assemble();

        AssetVerdict verdict = cast.Gate.Submit(Package("mystery-lib", scanned: false));

        // Unchecked is not the same as clean. An asset arriving with no scan
        // report is refused rather than assumed innocent, because the whole
        // point of a gate is that nothing passes unexamined.
        Assert.False(verdict.Admitted);
        Assert.Contains("not been scanned", verdict.Reason, StringComparison.Ordinal);
        Assert.False(cast.Registry.Contains("mystery-lib"));
    }
}
