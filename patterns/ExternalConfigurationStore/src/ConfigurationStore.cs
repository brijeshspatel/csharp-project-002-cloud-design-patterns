namespace ExternalConfigurationStore;

/// <summary>One change to one setting, and the version it produced.</summary>
/// <param name="Key">Which setting.</param>
/// <param name="Value">Its new value.</param>
/// <param name="Version">The store's version after the change.</param>
public readonly record struct SettingChange(string Key, string Value, int Version);

/// <summary>
/// Configuration held **outside every deployment package**, in one place that
/// all instances read.
///
/// The version number is what makes drift detectable: an instance reporting an
/// older version has stopped refreshing, and without a version nobody would
/// know until its behaviour diverged.
/// </summary>
public sealed class ConfigurationStore
{
    private readonly Dictionary<string, string> settings = [];
    private readonly List<SettingChange> history = [];
    private readonly HashSet<string> readers = [];

    /// <summary>The store's current version, incremented by every change.</summary>
    public int Version { get; private set; }

    /// <summary>Every change, in order — the audit trail configuration usually lacks.</summary>
    public IReadOnlyList<SettingChange> History => history;

    /// <summary>How many instances read from this store.</summary>
    public int Readers => readers.Count;

    /// <summary>
    /// Restarts that changing settings here did not require — the benefit, as a
    /// number. Every change would otherwise have meant redeploying or restarting
    /// every instance.
    /// </summary>
    public int RestartsAvoided => history.Count > 1 ? (history.Count - 1) * readers.Count : 0;

    /// <summary>Notes that an instance reads from this store.</summary>
    public void Register(string instanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        readers.Add(instanceName);
    }

    /// <summary>Changes a setting, once, for everybody.</summary>
    public void Set(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        settings[key] = value;
        Version++;
        history.Add(new SettingChange(key, value, Version));
    }

    /// <summary>The current value, or nothing where the store holds none.</summary>
    public string? Get(string key) => settings.TryGetValue(key, out string? value) ? value : null;
}

/// <summary>
/// One running instance of the application.
///
/// **It reads the store rather than remembering what it was told at startup.**
/// That single property is the pattern: a value read once and cached is a value
/// that requires a restart to change, which is the arrangement being replaced.
///
/// **It keeps packaged defaults**, deliberately. An application that cannot
/// start without reaching the store has made the store a hard startup
/// dependency, and a configuration store outage becomes a total outage.
///
/// **What this is not.** Deployment Stamps and Geode are about where components
/// run; this is about where their settings live, and it composes with both — one
/// store per stamp, or one per region, being the usual arrangements.
/// </summary>
public sealed class ApplicationInstance
{
    private readonly ConfigurationStore store;
    private readonly IReadOnlyDictionary<string, string> packagedDefaults;
    private int lastObservedVersion;

    /// <summary>Creates an instance reading from <paramref name="store"/>.</summary>
    public ApplicationInstance(
        string name,
        ConfigurationStore store,
        IReadOnlyDictionary<string, string> packagedDefaults)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(packagedDefaults);

        Name = name;
        this.store = store;
        this.packagedDefaults = packagedDefaults;
    }

    /// <summary>What this instance is called.</summary>
    public string Name { get; }

    /// <summary>How many times it has been restarted.</summary>
    public int Restarts { get; private set; }

    /// <summary>
    /// The configuration version this instance last observed — stamped by each
    /// read rather than proxied live from the store. The distinction is the
    /// point: an instance that has stopped reading genuinely reports an older
    /// number, and that gap is the drift the version exists to reveal.
    /// </summary>
    public int ConfigurationVersion => lastObservedVersion;

    /// <summary>
    /// Reads a setting: the store first, then the packaged default, then
    /// nothing.
    /// </summary>
    public string? Read(string key)
    {
        lastObservedVersion = store.Version;

        if (store.Get(key) is { } value)
        {
            return value;
        }

        return packagedDefaults.TryGetValue(key, out string? fallback) ? fallback : null;
    }

    /// <summary>Restarts the instance — which this pattern exists to avoid needing.</summary>
    public void Restart() => Restarts++;
}
