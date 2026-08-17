namespace StranglerFig;

/// <summary>One piece of billing work, asked of whichever system owns it.</summary>
/// <param name="Feature">Which capability — invoice, refund, statement, dunning.</param>
/// <param name="AccountId">Whose account.</param>
public readonly record struct BillingRequest(string Feature, string AccountId);

/// <summary>
/// The system being replaced. It still works, it still serves real traffic, and
/// it keeps doing so until the last feature has moved.
/// </summary>
public sealed class LegacyBilling
{
    /// <summary>How much work it is still doing.</summary>
    public int Handled { get; private set; }

    /// <summary>Does the work, as it always has.</summary>
    public string Handle(BillingRequest request)
    {
        Handled++;
        return $"legacy billing: {request.Feature} for {request.AccountId}";
    }
}

/// <summary>The replacement, which grows one feature at a time.</summary>
public sealed class ModernBilling
{
    /// <summary>How much work has moved to it.</summary>
    public int Handled { get; private set; }

    /// <summary>Does the work, in the new implementation.</summary>
    public string Handle(BillingRequest request)
    {
        Handled++;
        return $"modern billing: {request.Feature} for {request.AccountId}";
    }
}

/// <summary>
/// The router that makes the migration incremental.
///
/// Every caller talks to this, and it decides — per feature — whether the work
/// goes to the old system or the new one. Moving a feature is a routing change,
/// not a release of everything at once, and that is the whole of the pattern:
/// **the alternative is a big-bang cutover, where all the risk arrives on one
/// evening.**
///
/// **An unrecognised feature goes to the legacy system.** That is the opposite
/// of what a routing gateway should do, and here it is correct: nobody fully
/// understands a system old enough to need replacing, so the corners nobody
/// enumerated must keep working. It is counted rather than silent, so the
/// unknowns are discoverable — a migration whose fallback traffic never falls
/// is not finished, whatever the feature list says.
///
/// **What this is not.** Anti-Corruption Layer is a translation *boundary* that
/// may stand for a decade with no migration planned; this is a migration
/// *strategy* whose whole purpose is to end. The two are commonly used
/// together — the layer serves whatever has not moved — and neither requires
/// the other.
/// </summary>
public sealed class MigrationRouter
{
    private readonly LegacyBilling legacy;
    private readonly ModernBilling modern;
    private readonly List<string> features;
    private readonly HashSet<string> migrated = [];

    /// <summary>Creates a router over the two systems and the known features.</summary>
    public MigrationRouter(LegacyBilling legacy, ModernBilling modern, IReadOnlyList<string> features)
    {
        ArgumentNullException.ThrowIfNull(legacy);
        ArgumentNullException.ThrowIfNull(modern);
        ArgumentNullException.ThrowIfNull(features);

        this.legacy = legacy;
        this.modern = modern;
        this.features = [.. features];
    }

    /// <summary>Features that have moved, in the order they moved.</summary>
    public IReadOnlyList<string> Migrated => [.. features.Where(migrated.Contains)];

    /// <summary>Features still served by the legacy system.</summary>
    public IReadOnlyList<string> Remaining => [.. features.Where(feature => !migrated.Contains(feature))];

    /// <summary>How far the migration has got — the number that gets asked for.</summary>
    public double MigratedProportion => features.Count == 0 ? 1.0 : (double)migrated.Count / features.Count;

    /// <summary>Whether every known feature has moved.</summary>
    public bool Complete => migrated.Count == features.Count;

    /// <summary>
    /// Requests for features nobody wrote down. A non-zero count here means the
    /// feature list is incomplete, which is information worth having.
    /// </summary>
    public int UnknownFeatures { get; private set; }

    /// <summary>Moves one feature to the new implementation.</summary>
    public void Migrate(string feature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(feature);
        migrated.Add(feature);
    }

    /// <summary>Sends a request to whichever system currently owns its feature.</summary>
    public string Send(BillingRequest request)
    {
        if (!features.Contains(request.Feature))
        {
            UnknownFeatures++;
            return legacy.Handle(request);
        }

        return migrated.Contains(request.Feature)
            ? modern.Handle(request)
            : legacy.Handle(request);
    }
}
