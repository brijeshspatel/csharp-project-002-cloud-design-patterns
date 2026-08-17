namespace StranglerFig.Tests;

/// <summary>
/// What a strangler fig guarantees: that a legacy system is replaced **feature
/// by feature**, with routing that shifts as each one moves, and that nothing
/// which has not moved yet stops working.
///
/// The migration is a process rather than a state, so the tests exercise it
/// mid-flight: one feature migrated, three not, and both systems serving
/// traffic at once.
/// </summary>
public class MigrationRouterTests
{
    private static readonly string[] Features = ["invoice", "refund", "statement", "dunning"];

    private sealed record Cast(MigrationRouter Router, LegacyBilling Legacy, ModernBilling Modern);

    private static Cast Assemble()
    {
        LegacyBilling legacy = new();
        ModernBilling modern = new();
        return new Cast(new MigrationRouter(legacy, modern, Features), legacy, modern);
    }

    [Fact]
    public void Routes_an_unmigrated_feature_to_the_legacy_system()
    {
        Cast cast = Assemble();

        string answer = cast.Router.Send(new BillingRequest("refund", "ACC-1"));

        // Nothing has moved yet, so everything is still the legacy system's
        // work. A migration that broke unmigrated features on day one would not
        // be a migration.
        Assert.StartsWith("legacy", answer, StringComparison.Ordinal);
        Assert.Equal(1, cast.Legacy.Handled);
        Assert.Equal(0, cast.Modern.Handled);
    }

    [Fact]
    public void Routes_a_migrated_feature_to_the_new_implementation()
    {
        Cast cast = Assemble();

        cast.Router.Migrate("invoice");
        string answer = cast.Router.Send(new BillingRequest("invoice", "ACC-1"));

        Assert.StartsWith("modern", answer, StringComparison.Ordinal);
        Assert.Equal(1, cast.Modern.Handled);
        Assert.Equal(0, cast.Legacy.Handled);
    }

    [Fact]
    public void Migrates_one_feature_without_touching_the_others()
    {
        Cast cast = Assemble();

        cast.Router.Migrate("invoice");

        // Both systems serving at once, which is the whole of the migration's
        // middle. The alternative — a big-bang cutover — is the thing this
        // pattern exists to avoid.
        Assert.StartsWith("modern", cast.Router.Send(new BillingRequest("invoice", "ACC-1")), StringComparison.Ordinal);
        Assert.StartsWith("legacy", cast.Router.Send(new BillingRequest("refund", "ACC-1")), StringComparison.Ordinal);
        Assert.StartsWith("legacy", cast.Router.Send(new BillingRequest("statement", "ACC-1")), StringComparison.Ordinal);
        Assert.Equal(1, cast.Modern.Handled);
        Assert.Equal(2, cast.Legacy.Handled);
    }

    [Fact]
    public void Reports_the_proportion_of_features_migrated()
    {
        Cast cast = Assemble();

        Assert.Equal(0.00, cast.Router.MigratedProportion, 3);

        cast.Router.Migrate("invoice");
        Assert.Equal(0.25, cast.Router.MigratedProportion, 3);

        cast.Router.Migrate("refund");
        Assert.Equal(0.50, cast.Router.MigratedProportion, 3);

        // The number an engineering manager actually asks for, and the one that
        // distinguishes a migration in progress from one that has stalled.
        Assert.Equal(["statement", "dunning"], cast.Router.Remaining);
    }

    [Fact]
    public void Serves_every_feature_from_the_new_system_once_migration_completes()
    {
        Cast cast = Assemble();

        foreach (string feature in Features)
        {
            cast.Router.Migrate(feature);
        }

        foreach (string feature in Features)
        {
            Assert.StartsWith("modern", cast.Router.Send(new BillingRequest(feature, "ACC-1")), StringComparison.Ordinal);
        }

        Assert.True(cast.Router.Complete);
        Assert.Equal(0, cast.Legacy.Handled);
    }

    [Fact]
    public void Falls_back_to_the_legacy_system_for_an_unknown_feature()
    {
        Cast cast = Assemble();

        string answer = cast.Router.Send(new BillingRequest("chargeback", "ACC-1"));

        // Deliberate, and the opposite of what a routing gateway should do. A
        // migration cannot enumerate every corner of a system nobody fully
        // understands, so anything unrecognised must keep working — and be
        // counted, so the unknowns are discoverable rather than invisible.
        Assert.StartsWith("legacy", answer, StringComparison.Ordinal);
        Assert.Equal(1, cast.Router.UnknownFeatures);
    }

    [Fact]
    public void Refuses_to_migrate_a_feature_nobody_wrote_down()
    {
        Cast cast = Assemble();

        // A typo silently added to the migrated set would inflate the count
        // that Complete divides by: the migration would end on paper while the
        // real feature still routed to legacy.
        Assert.Throws<ArgumentException>(() => cast.Router.Migrate("invocie"));

        Assert.False(cast.Router.Complete);
        Assert.Empty(cast.Router.Migrated);
    }
}
