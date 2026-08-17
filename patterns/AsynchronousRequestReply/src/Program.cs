using AsynchronousRequestReply;

// A quarterly sales report takes minutes to build. The caller cannot hold a
// connection open that long, so it is given a handle and comes back. Both
// outcomes are shown, because a caller that can only discover success waits
// for ever on failure.

Console.WriteLine("Asynchronous Request-Reply - an immediate answer to a slow request");
Console.WriteLine(new string('=', 66));
Console.WriteLine();

InMemoryJobStore store = new();
JobGateway gateway = new(store);
StatusEndpoint status = new(store);
ReportWorker worker = new(store);

Console.WriteLine("A report that succeeds");
Console.WriteLine(new string('-', 66));

Acceptance accepted = gateway.Submit(new ReportRequest("quarterly-sales", "2026-Q2"));
Console.WriteLine($"  submitted -> job {accepted.JobId}, poll {accepted.StatusLocation}");
Console.WriteLine("  the caller now has its connection back");

Poll(accepted.JobId);
worker.Start(accepted.JobId);
Poll(accepted.JobId);
worker.Complete(accepted.JobId, "https://reports.example/quarterly-sales-2026-Q2.csv");
Poll(accepted.JobId);

Console.WriteLine();
Console.WriteLine("A report that fails");
Console.WriteLine(new string('-', 66));

Acceptance failing = gateway.Submit(new ReportRequest("annual-audit", "2025"));
Console.WriteLine($"  submitted -> job {failing.JobId}");
worker.Start(failing.JobId);
worker.Fail(failing.JobId, "the warehouse was unavailable");
Poll(failing.JobId);

Console.WriteLine();
Console.WriteLine("A job nobody has heard of");
Console.WriteLine(new string('-', 66));
Poll("job-9999");

Console.WriteLine();
Console.WriteLine("NotFound is deliberately not Pending. Reporting pending for work");
Console.WriteLine("that does not exist would have the caller polling for ever.");

void Poll(string jobId)
{
    JobState state = status.Poll(jobId);
    string detail = state.Status switch
    {
        JobStatus.Succeeded => $" -> {state.Result}",
        JobStatus.Failed => $" -> {state.Error}",
        _ => string.Empty,
    };

    Console.WriteLine($"  poll {jobId}: {state.Status}{detail}");
}
