namespace AsynchronousRequestReply;

/// <summary>
/// Accepts work that will take minutes and answers the caller immediately.
///
/// The caller is a front end that cannot wait: a browser will time out, a
/// gateway will cut the connection, and holding a request thread open for ten
/// minutes is capacity nobody can afford. So the gateway does not return the
/// answer — it returns a **handle to the answer**, and the caller comes back.
///
/// Over HTTP this is `202 Accepted` with a `Location` header, and the status
/// location is a URL the caller polls. That transport is not modelled here (see
/// **In Azure**); the shape of the exchange is.
/// </summary>
public sealed class JobGateway
{
    private readonly IJobStore store;

    /// <summary>Creates a gateway over <paramref name="store"/>.</summary>
    public JobGateway(IJobStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Accepts <paramref name="request"/> and returns where to ask about it.
    /// </summary>
    public Acceptance Submit(ReportRequest request)
    {
        string jobId = store.Create(request);

        // The status location must contain the identifier. A caller given only
        // "accepted" has no way to ever find the answer, which is the one thing
        // this exchange exists to provide.
        return new Acceptance(jobId, $"/jobs/{jobId}/status");
    }
}

/// <summary>Answers "where has my job got to?".</summary>
public sealed class StatusEndpoint
{
    private readonly IJobStore store;

    /// <summary>Creates an endpoint over <paramref name="store"/>.</summary>
    public StatusEndpoint(IJobStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>
    /// Reports a job's current state.
    ///
    /// An unknown job is <see cref="JobStatus.NotFound"/>, never
    /// <see cref="JobStatus.Pending"/>. Reporting pending for work that does not
    /// exist would have a caller polling for ever.
    /// </summary>
    public JobState Poll(string jobId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        return store.Read(jobId);
    }
}

/// <summary>
/// The back end doing the work.
///
/// It is driven explicitly here rather than running on its own thread, so the
/// demonstration and the tests can observe each transition rather than race it.
/// </summary>
public sealed class ReportWorker
{
    private readonly IJobStore store;

    /// <summary>Creates a worker over <paramref name="store"/>.</summary>
    public ReportWorker(IJobStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        this.store = store;
    }

    /// <summary>Marks a job as being worked on.</summary>
    public void Start(string jobId)
    {
        MustKnow(jobId);
        store.Write(jobId, new JobState(JobStatus.Running, null, null));
    }

    /// <summary>Finishes a job with a result.</summary>
    public void Complete(string jobId, string result)
    {
        MustKnow(jobId);
        store.Write(jobId, new JobState(JobStatus.Succeeded, result, null));
    }

    /// <summary>Finishes a job with an error.</summary>
    public void Fail(string jobId, string error)
    {
        MustKnow(jobId);
        store.Write(jobId, new JobState(JobStatus.Failed, null, error));
    }

    // A worker given an identifier nothing submitted must refuse it. Writing it
    // would mint a job that was never accepted - and turn NotFound, the status
    // this pattern makes load-bearing, into Running on the strength of a typo.
    private void MustKnow(string jobId)
    {
        if (!store.Knows(jobId))
        {
            throw new InvalidOperationException($"No job '{jobId}' was ever accepted.");
        }
    }
}
