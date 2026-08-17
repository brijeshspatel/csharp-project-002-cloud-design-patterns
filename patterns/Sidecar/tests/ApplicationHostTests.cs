namespace Sidecar.Tests;

/// <summary>
/// What a sidecar guarantees: a helper that **shares the application's
/// lifecycle** — started with it, stopped with it — while remaining isolated
/// enough that its failure is not the application's.
///
/// Lifecycle sharing is what is tested, because it is what separates a sidecar
/// from a helper class. A component that must be started and stopped by hand,
/// or that takes the application down when it fails, is composition rather than
/// a deployment topology.
/// </summary>
public class ApplicationHostTests
{
    private static (ApplicationHost Host, MetricsSidecar Sidecar) Assemble(bool sidecarFails = false)
    {
        MetricsSidecar sidecar = new(failsOnStart: sidecarFails);
        return (new ApplicationHost(sidecar), sidecar);
    }

    [Fact]
    public void Starts_the_sidecar_with_the_application()
    {
        (ApplicationHost host, MetricsSidecar sidecar) = Assemble();

        Assert.Equal(SidecarStatus.Stopped, sidecar.Status);

        host.Start();

        // Nobody started the sidecar. It came up because the application did,
        // which is the property that makes it a sidecar rather than a service
        // somebody has to remember to deploy.
        Assert.True(host.Running);
        Assert.Equal(SidecarStatus.Running, sidecar.Status);
    }

    [Fact]
    public void Stops_the_sidecar_with_the_application()
    {
        (ApplicationHost host, MetricsSidecar sidecar) = Assemble();
        host.Start();

        host.Stop();

        // And it goes down with it. A helper that outlives its application is
        // a leak; one that survives a rescheduled pod is somebody's incident.
        Assert.False(host.Running);
        Assert.Equal(SidecarStatus.Stopped, sidecar.Status);
    }

    [Fact]
    public void Collects_metrics_the_application_did_not_write()
    {
        (ApplicationHost host, MetricsSidecar sidecar) = Assemble();
        host.Start();

        host.Handle("/orders/1042");
        host.Handle("/catalogue/sku-77");

        // The application's own record contains its work and nothing else — no
        // timing, no counters, no metrics code at all. The observations exist
        // because the sidecar was there, not because the application cooperated.
        Assert.Equal(["/orders/1042", "/catalogue/sku-77"], host.Handled);
        Assert.DoesNotContain(host.Handled, entry => entry.Contains("observed", StringComparison.Ordinal));
        Assert.Equal(2, sidecar.Collected.Count);
    }

    [Fact]
    public void Survives_a_sidecar_that_fails()
    {
        (ApplicationHost host, MetricsSidecar sidecar) = Assemble(sidecarFails: true);

        host.Start();
        string answer = host.Handle("/orders/1042");

        // The application is serving. Losing telemetry is bad; losing the
        // application because telemetry failed is very much worse, and it is
        // the mistake that makes teams distrust sidecars.
        Assert.True(host.Running);
        Assert.Equal("handled /orders/1042", answer);
        Assert.Equal(SidecarStatus.Failed, sidecar.Status);
        Assert.Empty(sidecar.Collected);
    }

    [Fact]
    public void Reports_the_sidecar_status_to_the_host()
    {
        (ApplicationHost healthy, _) = Assemble();
        (ApplicationHost broken, _) = Assemble(sidecarFails: true);

        healthy.Start();
        broken.Start();

        // Isolated is not the same as invisible. The host can see that its
        // sidecar is down, which is what lets it be reported rather than
        // silently absent.
        Assert.Equal(SidecarStatus.Running, healthy.SidecarStatus);
        Assert.Equal(SidecarStatus.Failed, broken.SidecarStatus);
    }
}
