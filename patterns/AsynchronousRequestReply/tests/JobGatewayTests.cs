namespace AsynchronousRequestReply.Tests;

/// <summary>
/// What the exchange guarantees: that a caller gets an immediate, useful answer
/// to a request that will take minutes, that polling reports honest progress,
/// and that a job nobody has heard of is reported as such rather than as
/// pending.
/// </summary>
public class JobGatewayTests
{
    private static (JobGateway Gateway, StatusEndpoint Status, ReportWorker Worker) Build()
    {
        InMemoryJobStore store = new();
        return (new JobGateway(store), new StatusEndpoint(store), new ReportWorker(store));
    }

    private static ReportRequest Request() => new("quarterly-sales", "2026-Q2");

    [Fact]
    public void Accepts_a_request_and_returns_a_status_location()
    {
        (JobGateway gateway, _, _) = Build();

        Acceptance acceptance = gateway.Submit(Request());

        // The caller leaves with something it can act on. Returning only "ok"
        // would leave it with no way to ever find the answer.
        Assert.NotEmpty(acceptance.JobId);
        Assert.Contains(acceptance.JobId, acceptance.StatusLocation, StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_pending_before_the_work_starts()
    {
        (JobGateway gateway, StatusEndpoint status, _) = Build();

        Acceptance acceptance = gateway.Submit(Request());

        Assert.Equal(JobStatus.Pending, status.Poll(acceptance.JobId).Status);
    }

    [Fact]
    public void Reports_running_while_the_work_is_in_progress()
    {
        (JobGateway gateway, StatusEndpoint status, ReportWorker worker) = Build();
        Acceptance acceptance = gateway.Submit(Request());

        worker.Start(acceptance.JobId);

        // Pending and running are different facts, and a caller showing a
        // progress indicator needs to tell them apart.
        Assert.Equal(JobStatus.Running, status.Poll(acceptance.JobId).Status);
    }

    [Fact]
    public void Reports_the_result_once_the_work_succeeds()
    {
        (JobGateway gateway, StatusEndpoint status, ReportWorker worker) = Build();
        Acceptance acceptance = gateway.Submit(Request());

        worker.Start(acceptance.JobId);
        worker.Complete(acceptance.JobId, "https://reports.example/quarterly-sales-2026-Q2.csv");

        JobState state = status.Poll(acceptance.JobId);

        Assert.Equal(JobStatus.Succeeded, state.Status);
        Assert.Equal("https://reports.example/quarterly-sales-2026-Q2.csv", state.Result);
    }

    [Fact]
    public void Reports_the_failure_once_the_work_fails()
    {
        (JobGateway gateway, StatusEndpoint status, ReportWorker worker) = Build();
        Acceptance acceptance = gateway.Submit(Request());

        worker.Start(acceptance.JobId);
        worker.Fail(acceptance.JobId, "the warehouse was unavailable");

        JobState state = status.Poll(acceptance.JobId);

        // A failure must be reachable through the same channel as a success.
        // A caller that can only discover success waits for ever on failure.
        Assert.Equal(JobStatus.Failed, state.Status);
        Assert.Equal("the warehouse was unavailable", state.Error);
    }

    [Fact]
    public void Reports_not_found_for_a_job_it_has_never_seen()
    {
        (_, StatusEndpoint status, _) = Build();

        // Reporting Pending here would be the worst possible answer: the caller
        // would poll for ever for work that does not exist and never will.
        Assert.Equal(JobStatus.NotFound, status.Poll("no-such-job").Status);
    }

    [Fact]
    public void Refuses_work_on_a_job_nothing_ever_submitted()
    {
        (_, StatusEndpoint status, ReportWorker worker) = Build();

        // A worker with a mistyped identifier must not mint a job that was
        // never accepted - that would turn NotFound into Running on the
        // strength of a typo.
        Assert.Throws<InvalidOperationException>(() => worker.Start("job-9999"));
        Assert.Equal(JobStatus.NotFound, status.Poll("job-9999").Status);
    }
}
