namespace Sidecar;

/// <summary>Where the sidecar has got to, as the host can see it.</summary>
public enum SidecarStatus
{
    /// <summary>Not running — before start, or after stop.</summary>
    Stopped,

    /// <summary>Up and collecting.</summary>
    Running,

    /// <summary>It fell over. **The application did not.**</summary>
    Failed,
}

/// <summary>
/// A helper deployed beside the application: same host, same lifecycle,
/// separate process.
///
/// It collects observations the application never wrote. That is the point —
/// the application contains no metrics code, and the telemetry exists because
/// something was deployed next to it rather than because it cooperated. The
/// same shape carries configuration reloading, certificate rotation, log
/// shipping and proxying.
/// </summary>
public sealed class MetricsSidecar
{
    private readonly bool failsOnStart;
    private readonly List<string> collected = [];

    /// <summary>Creates a sidecar, optionally one that fails when started.</summary>
    public MetricsSidecar(bool failsOnStart = false) => this.failsOnStart = failsOnStart;

    /// <summary>Where it has got to.</summary>
    public SidecarStatus Status { get; private set; } = SidecarStatus.Stopped;

    /// <summary>What it has observed. Empty where it never came up.</summary>
    public IReadOnlyList<string> Collected => collected;

    /// <summary>
    /// Brought up by the host, not by anybody remembering to. Failure here sets
    /// the status and is not thrown — the host must not be taken down by its
    /// telemetry.
    /// </summary>
    public void Start() => Status = failsOnStart ? SidecarStatus.Failed : SidecarStatus.Running;

    /// <summary>Taken down by the host, so it cannot outlive the application.</summary>
    public void Stop() => Status = SidecarStatus.Stopped;

    /// <summary>Records one observation, where it is running to record it.</summary>
    public void Observe(string path)
    {
        if (Status != SidecarStatus.Running)
        {
            return;
        }

        collected.Add($"observed {path}");
    }
}

/// <summary>
/// The host: the unit that is deployed, containing the application and its
/// sidecar.
///
/// **Lifecycle sharing is the pattern.** Starting the host starts the sidecar;
/// stopping the host stops it. Nobody deploys the sidecar separately, nobody
/// remembers to start it, and it cannot outlive the application it belongs to.
/// A helper that must be managed by hand is composition; this is a topology.
///
/// **Isolation is the other half.** The sidecar can fail without the
/// application failing — losing telemetry is bad, and losing the application
/// because telemetry failed is very much worse. It is the mistake that makes
/// teams distrust sidecars, and it is what the fourth test exists to prevent.
///
/// **Isolated is not invisible.** The host can see its sidecar's status, so a
/// failed one is reported rather than silently absent.
///
/// **What this is not.** Ambassador is one *job* this shape does — outbound
/// calls with retry policy attached — and is conventionally deployed exactly
/// like this. Sidecar is the shape itself, which also carries metrics,
/// configuration, certificates and log shipping, none of which involve an
/// outbound call on the application's behalf.
/// </summary>
public sealed class ApplicationHost
{
    private readonly MetricsSidecar sidecar;
    private readonly List<string> handled = [];

    /// <summary>Creates a host containing <paramref name="sidecar"/>.</summary>
    public ApplicationHost(MetricsSidecar sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        this.sidecar = sidecar;
    }

    /// <summary>Whether the application is up.</summary>
    public bool Running { get; private set; }

    /// <summary>What the application did — its own work, and nothing else.</summary>
    public IReadOnlyList<string> Handled => handled;

    /// <summary>The sidecar's status, so a failure is visible rather than silent.</summary>
    public SidecarStatus SidecarStatus => sidecar.Status;

    /// <summary>Starts the application, and the sidecar with it.</summary>
    public void Start()
    {
        Running = true;

        // Started here, and its failure deliberately not rethrown: the host
        // comes up whether or not its telemetry did.
        sidecar.Start();
    }

    /// <summary>Stops the application, and the sidecar with it.</summary>
    public void Stop()
    {
        sidecar.Stop();
        Running = false;
    }

    /// <summary>
    /// Serves one request. The application work is <see cref="DoWork"/>, which
    /// knows nothing about metrics; the host hands the observation to the
    /// sidecar afterwards.
    /// </summary>
    public string Handle(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string answer = DoWork(path);
        sidecar.Observe(path);
        return answer;
    }

    /// <summary>
    /// The application's actual work. Deliberately free of any reference to the
    /// sidecar: this is the code that would exist with no sidecar deployed.
    /// </summary>
    private string DoWork(string path)
    {
        handled.Add(path);
        return $"handled {path}";
    }
}
