namespace Quarantine;

/// <summary>
/// Something from outside the organisation that the workload wants to consume:
/// a package, a container image, a model, a dataset.
///
/// **`Scanned` is a fact about the asset, not an assumption.** An artefact
/// arriving with no scan report is not clean; it is unexamined, and the two are
/// routinely confused.
/// </summary>
/// <param name="Name">What it is called.</param>
/// <param name="Version">Which version.</param>
/// <param name="Licence">What it is licensed under.</param>
/// <param name="HasKnownVulnerability">Whether scanning found something.</param>
/// <param name="Scanned">Whether it has been scanned **at all**.</param>
public readonly record struct ExternalAsset(
    string Name,
    string Version,
    string Licence,
    bool HasKnownVulnerability,
    bool Scanned);

/// <summary>What the gate decided, and why.</summary>
/// <param name="Admitted">Whether the workload may use it.</param>
/// <param name="Reason">Which check it failed — specific, not "rejected".</param>
public readonly record struct AssetVerdict(bool Admitted, string Reason);

/// <summary>
/// What the workload is allowed to consume — **and nothing else**.
///
/// This is the only thing downstream reads, which is what makes the gate a gate
/// rather than a report. A refused asset does not appear here, so nothing has to
/// remember to check a verdict before using something.
/// </summary>
public sealed class AssetRegistry
{
    private readonly Dictionary<string, ExternalAsset> admitted = [];

    /// <summary>Everything the workload may use.</summary>
    public IReadOnlyList<string> Admitted => [.. admitted.Keys];

    /// <summary>Whether a named asset is available to the workload.</summary>
    public bool Contains(string name) => admitted.ContainsKey(name);

    /// <summary>
    /// Makes an asset available. The gate is the only caller in this model,
    /// but nothing in the type system enforces that — a real registry closes
    /// this door with access control, so that admission genuinely cannot
    /// happen except through the checks.
    /// </summary>
    public void Admit(ExternalAsset asset) => admitted[asset.Name] = asset;
}

/// <summary>
/// The gate every external asset passes through **before** the workload can
/// consume it.
///
/// The ordering is the entire pattern. Scanning after adoption tells you that
/// something bad is already in production — useful, and monitoring rather than
/// quarantine. Checking first means the bad thing never arrives.
///
/// **Unchecked is refused, not admitted.** An asset with no scan report is
/// unexamined, and a gate that let it through on the grounds that nothing was
/// found has inverted its own purpose.
///
/// **Refusals name the check they failed**, because "rejected" tells a
/// developer nothing about whether to find another library, request an
/// exception, or wait for a patched version.
///
/// **What this is not.** Gatekeeper, in tier 5, validates **requests** arriving
/// from outside and its subject is network reachability. This validates
/// **assets** the organisation is choosing to depend on, before they enter the
/// supply chain.
/// </summary>
public sealed class QuarantineGate
{
    private readonly AssetRegistry registry;
    private readonly List<string> allowedLicences;

    /// <summary>Creates a gate admitting into <paramref name="registry"/>.</summary>
    public QuarantineGate(AssetRegistry registry, IReadOnlyList<string> allowedLicences)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(allowedLicences);

        this.registry = registry;
        this.allowedLicences = [.. allowedLicences];
    }

    /// <summary>How many assets were refused **before** the workload saw them.</summary>
    public int Refused { get; private set; }

    /// <summary>How many were admitted.</summary>
    public int AdmittedCount { get; private set; }

    /// <summary>
    /// The checks this gate applies, named — including which licences it
    /// actually accepts, because "licence" alone tells a developer nothing
    /// about why theirs was refused.
    /// </summary>
    public IReadOnlyList<string> Checks =>
        ["scanned at all", "known vulnerabilities", $"licence in [{string.Join(", ", allowedLicences)}]"];

    /// <summary>
    /// Submits an asset for admission. It reaches the registry only if every
    /// check passes.
    /// </summary>
    public AssetVerdict Submit(ExternalAsset asset)
    {
        if (!asset.Scanned)
        {
            return Refuse($"'{asset.Name}' has not been scanned; unchecked is not clean");
        }

        if (asset.HasKnownVulnerability)
        {
            return Refuse($"'{asset.Name} {asset.Version}' has a known vulnerability");
        }

        if (!allowedLicences.Contains(asset.Licence))
        {
            return Refuse($"'{asset.Licence}' is not an approved licence");
        }

        registry.Admit(asset);
        AdmittedCount++;
        return new AssetVerdict(Admitted: true, Reason: string.Empty);
    }

    private AssetVerdict Refuse(string reason)
    {
        Refused++;
        return new AssetVerdict(Admitted: false, reason);
    }
}
