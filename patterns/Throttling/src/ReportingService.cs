namespace Throttling;

/// <summary>
/// A multi-tenant reporting API behind a throttle.
///
/// It exists to make the degraded path visible. Reports are expensive, so
/// between the soft and hard limits this serves yesterday's cached figures
/// instead of recomputing — a real answer, plainly labelled as stale, which is
/// what "degrade rather than refuse" means in practice.
/// </summary>
public sealed class ReportingService
{
    private readonly Throttle throttle;

    /// <summary>Creates the service behind <paramref name="throttle"/>.</summary>
    public ReportingService(Throttle throttle)
    {
        ArgumentNullException.ThrowIfNull(throttle);
        this.throttle = throttle;
    }

    /// <summary>Serves a report for <paramref name="tenant"/>, or declines to.</summary>
    public string RunReport(string tenant) => throttle.Admit(tenant) switch
    {
        ThrottleDecision.Accepted =>
            "full report, recomputed from source",
        ThrottleDecision.Degraded =>
            "cached report from 09:00, recompute skipped to protect the service",
        _ =>
            "declined: quota exhausted for this window, retry in the next one",
    };
}
