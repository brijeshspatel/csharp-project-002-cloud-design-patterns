namespace CompetingConsumers;

/// <summary>
/// One worker drawing from the shared channel.
///
/// Consumers are identical and interchangeable, which is what makes the pattern
/// scale: adding another one needs no coordination, no partitioning and no
/// change anywhere else. The channel is the only thing they share.
/// </summary>
public sealed class Consumer
{
    private readonly List<WorkItem> handled = [];
    private readonly bool failOnce;
    private bool hasFailed;

    /// <summary>Creates a consumer called <paramref name="id"/>.</summary>
    /// <param name="id">How it identifies itself in output.</param>
    /// <param name="failOnce">When set, its first claim fails and is released.</param>
    public Consumer(string id, bool failOnce = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Id = id;
        this.failOnce = failOnce;
    }

    /// <summary>Which worker this is.</summary>
    public string Id { get; }

    /// <summary>Everything it has completed.</summary>
    public IReadOnlyList<WorkItem> Handled => handled;

    /// <summary>
    /// Claims one item and processes it.
    /// </summary>
    /// <returns><c>true</c> when an item was claimed **and completed**.</returns>
    public bool ProcessNext(WorkChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        if (!channel.TryClaim(out WorkItem item))
        {
            return false;
        }

        if (failOnce && !hasFailed)
        {
            hasFailed = true;

            // Releasing is not optional. A claimed item that is neither
            // completed nor released has been silently lost, and nothing in the
            // system will ever report it missing.
            channel.Release(item);
            return false;
        }

        handled.Add(item);
        return true;
    }
}
