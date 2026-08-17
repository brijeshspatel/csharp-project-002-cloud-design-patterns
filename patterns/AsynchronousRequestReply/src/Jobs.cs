namespace AsynchronousRequestReply;

/// <summary>What the caller asked for.</summary>
/// <param name="Report">Which report.</param>
/// <param name="Period">Over what period.</param>
public readonly record struct ReportRequest(string Report, string Period);

/// <summary>Where a job has got to.</summary>
public enum JobStatus
{
    /// <summary>Accepted, not yet started.</summary>
    Pending,

    /// <summary>Being worked on.</summary>
    Running,

    /// <summary>Finished, with a result.</summary>
    Succeeded,

    /// <summary>Finished, with an error.</summary>
    Failed,

    /// <summary>No such job. Deliberately distinct from Pending.</summary>
    NotFound,
}

/// <summary>What the caller gets immediately, in place of the answer.</summary>
/// <param name="JobId">The handle for this piece of work.</param>
/// <param name="StatusLocation">Where to ask about it.</param>
public readonly record struct Acceptance(string JobId, string StatusLocation);

/// <summary>The current state of a job.</summary>
/// <param name="Status">Where it has got to.</param>
/// <param name="Result">Present once it has succeeded.</param>
/// <param name="Error">Present once it has failed.</param>
public readonly record struct JobState(JobStatus Status, string? Result, string? Error);

/// <summary>Where job state lives between the request and the poll.</summary>
public interface IJobStore
{
    /// <summary>Records a newly accepted job and returns its identifier.</summary>
    string Create(ReportRequest request);

    /// <summary>Reads a job's state. Returns <see cref="JobStatus.NotFound"/> if unknown.</summary>
    JobState Read(string jobId);

    /// <summary>Replaces a job's state.</summary>
    void Write(string jobId, JobState state);

    /// <summary>Whether the store knows about this job.</summary>
    bool Knows(string jobId);
}

/// <summary>An in-memory store. A real one outlives the process, which is the point.</summary>
public sealed class InMemoryJobStore : IJobStore
{
    private readonly Dictionary<string, JobState> jobs = [];
    private int next;

    /// <inheritdoc/>
    public string Create(ReportRequest request)
    {
        string jobId = $"job-{++next:0000}";
        jobs[jobId] = new JobState(JobStatus.Pending, null, null);
        return jobId;
    }

    /// <inheritdoc/>
    public JobState Read(string jobId) =>
        jobs.TryGetValue(jobId, out JobState state)
            ? state
            : new JobState(JobStatus.NotFound, null, null);

    /// <inheritdoc/>
    public void Write(string jobId, JobState state) => jobs[jobId] = state;

    /// <inheritdoc/>
    public bool Knows(string jobId) => jobs.ContainsKey(jobId);
}
