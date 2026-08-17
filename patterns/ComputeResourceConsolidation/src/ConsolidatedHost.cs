namespace ComputeResourceConsolidation;

/// <summary>
/// What consolidation cost and what it saved.
/// </summary>
/// <param name="UnitsBefore">Computational units when each task had its own.</param>
/// <param name="UnitsAfter">Units now.</param>
/// <param name="Utilisation">Percentage of the host's capacity claimed — **may exceed 100**.</param>
public readonly record struct ResourceUsage(int UnitsBefore, int UnitsAfter, int Utilisation)
{
    /// <summary>Units that no longer have to exist — the benefit, as a number.</summary>
    public int UnitsRemoved => UnitsBefore - UnitsAfter;
}

/// <summary>
/// One small, periodic job: a nightly report, a cache warm, an index rebuild.
///
/// Each of these is the kind of task that historically got a virtual machine of
/// its own — idle for twenty-three and a half hours a day, and paid for around
/// the clock.
/// </summary>
public sealed class ScheduledTask
{
    /// <summary>Creates a task claiming <paramref name="cost"/> of a host's capacity.</summary>
    public ScheduledTask(string name, int cost)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cost);

        Name = name;
        Cost = cost;
    }

    /// <summary>What this task is called.</summary>
    public string Name { get; }

    /// <summary>What share of a host's capacity it claims.</summary>
    public int Cost { get; }

    /// <summary>How many times it has run.</summary>
    public int Runs { get; private set; }

    /// <summary>Does the work. Unchanged by which host it happens to run on.</summary>
    public string Run(bool degraded)
    {
        Runs++;
        return degraded ? $"{Name} completed (degraded)" : $"{Name} completed";
    }
}

/// <summary>
/// One computational unit running many tasks.
///
/// **This pattern argues the opposite of the rest of its tier.** Deployment
/// Stamps and Geode both say *deploy more copies* — for blast radius and for
/// reach. This one says *deploy fewer units* — for cost density. They are not
/// in conflict because they answer different questions: how much idle capacity
/// am I paying for, versus how much of the system does one failure take with
/// it. A system can and often should do both, at different granularities.
///
/// **Sharing capacity means sharing fate.** One task that claims more than its
/// share does not fail; it makes its neighbours slower. That is the price of
/// the units removed, and it is asserted rather than described.
///
/// **What this is not.** Deployment Stamps separates by tenant for isolation;
/// Geode replicates for reach. Bulkhead, in tier 1, isolates *within* a process
/// so one workload cannot exhaust another — which is the mitigation for exactly
/// the noisy-neighbour cost this pattern introduces.
/// </summary>
public sealed class ConsolidatedHost
{
    private readonly List<ScheduledTask> tasks = [];

    /// <summary>Creates a host with <paramref name="capacity"/> to share out.</summary>
    public ConsolidatedHost(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    /// <summary>How much this host has to give.</summary>
    public int Capacity { get; }

    /// <summary>The tasks running here.</summary>
    public IReadOnlyList<string> Tasks => [.. tasks.Select(task => task.Name)];

    /// <summary>What was claimed, what was saved, and how full the host is.</summary>
    public ResourceUsage Usage =>
        new(
            UnitsBefore: tasks.Count,
            UnitsAfter: tasks.Count == 0 ? 0 : 1,
            Utilisation: tasks.Sum(task => task.Cost) * 100 / Capacity);

    /// <summary>
    /// Whether the tasks together claim more than the host has. Over-committed
    /// is not a failure — it is everybody being slower.
    /// </summary>
    public bool IsOverCommitted => Usage.Utilisation > 100;

    /// <summary>How many times a named task has run.</summary>
    public int RunsOf(string name) =>
        tasks.FirstOrDefault(task => task.Name == name)?.Runs ?? 0;

    /// <summary>Adds a task to this host.</summary>
    public void Add(ScheduledTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        tasks.Add(task);
    }

    /// <summary>
    /// Runs **every** task. Nothing is dropped for not fitting: a host that shed
    /// work it could not accommodate would look efficient while losing the
    /// nightly report.
    /// </summary>
    public IReadOnlyList<string> RunAll()
    {
        bool degraded = IsOverCommitted;
        return [.. tasks.Select(task => task.Run(degraded))];
    }
}
