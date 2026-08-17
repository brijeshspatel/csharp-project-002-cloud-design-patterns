namespace DeploymentStamps;

/// <summary>Whether a stamp is serving.</summary>
public enum StampHealth
{
    /// <summary>Serving its tenants.</summary>
    Healthy,

    /// <summary>Not serving. **Only its own tenants are affected.**</summary>
    Down,
}

/// <summary>One piece of work, for one tenant.</summary>
/// <param name="TenantId">Whose work.</param>
/// <param name="Work">What is being asked.</param>
public readonly record struct TenantRequest(string TenantId, string Work);

/// <summary>
/// One complete, independent copy of the application — **including its own data
/// store**.
///
/// The independence is the pattern. A stamp knows nothing about any other
/// stamp: no shared database, no shared cache, no shared queue. That is what
/// makes a stamp's failure local, and it is also why a tenant cannot simply be
/// served by a different one.
/// </summary>
public sealed class Stamp
{
    private readonly List<string> tenants = [];

    /// <summary>Creates a stamp named <paramref name="name"/>.</summary>
    public Stamp(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>What this stamp is called.</summary>
    public string Name { get; }

    /// <summary>Whether it is serving.</summary>
    public StampHealth Health { get; private set; } = StampHealth.Healthy;

    /// <summary>The tenants whose data lives here, and nowhere else.</summary>
    public IReadOnlyList<string> Tenants => tenants;

    /// <summary>How much work it has done.</summary>
    public int Handled { get; private set; }

    /// <summary>Gives a tenant its home here.</summary>
    public void Home(string tenantId) => tenants.Add(tenantId);

    /// <summary>Takes the stamp out of service.</summary>
    public void Fail() => Health = StampHealth.Down;

    /// <summary>Returns it to service.</summary>
    public void Recover() => Health = StampHealth.Healthy;

    /// <summary>Does the work, using this stamp's own data.</summary>
    public string Handle(TenantRequest request)
    {
        Handled++;
        return $"{Name} handled {request.Work} for {request.TenantId}";
    }
}

/// <summary>
/// Assigns each tenant a stamp and keeps it there.
///
/// **The home is stable, deliberately.** A tenant's data lives in its stamp's
/// own store, so a tenant that drifted between stamps would find a different
/// world each time — which is a badly replicated system rather than a stamped
/// one. Stability is what makes the isolation mean anything.
///
/// **A request for a tenant whose stamp is down is refused, not rerouted.**
/// Another stamp holds none of this tenant's data, so failing over would answer
/// with somebody else's world. An honest refusal is better.
///
/// **What this is not.** Geode is the opposite arrangement: there, every node
/// holds the same data and **any** node can serve **any** request, which buys
/// ubiquity and costs replication lag. Stamps buy isolation and cost the
/// ability to serve a tenant from anywhere. Sharding partitions a data store
/// within one application; a stamp is a whole application, including its store.
/// </summary>
public sealed class StampRouter
{
    private readonly List<Stamp> stamps = [];
    private readonly Dictionary<string, Stamp> homes = [];

    /// <summary>The stamps, in the order they were added.</summary>
    public IReadOnlyList<string> Stamps => [.. stamps.Select(stamp => stamp.Name)];

    /// <summary>How many tenants have a home.</summary>
    public int AssignedTenants => homes.Count;

    /// <summary>Adds a stamp. **Existing tenants do not move.**</summary>
    public void AddStamp(Stamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        stamps.Add(stamp);
    }

    /// <summary>
    /// Gives a tenant a home, once. Assignment fills the emptiest stamp, which
    /// is why adding a stamp attracts new tenants without moving anybody.
    /// </summary>
    public void Assign(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (homes.ContainsKey(tenantId) || stamps.Count == 0)
        {
            return;
        }

        Stamp emptiest = stamps[0];
        foreach (Stamp stamp in stamps)
        {
            if (stamp.Tenants.Count < emptiest.Tenants.Count)
            {
                emptiest = stamp;
            }
        }

        emptiest.Home(tenantId);
        homes[tenantId] = emptiest;
    }

    /// <summary>Which stamp owns a tenant — the same answer every time.</summary>
    public Stamp? HomeOf(string tenantId) =>
        homes.TryGetValue(tenantId, out Stamp? stamp) ? stamp : null;

    /// <summary>
    /// How many tenants would not notice <paramref name="stamp"/> failing —
    /// the benefit, as a number.
    /// </summary>
    public int TenantsUnaffectedBy(Stamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);
        return homes.Count - stamp.Tenants.Count;
    }

    /// <summary>Sends work to the tenant's own stamp, or refuses.</summary>
    public string? Send(TenantRequest request)
    {
        Stamp? home = HomeOf(request.TenantId);

        if (home is null || home.Health == StampHealth.Down)
        {
            return null;
        }

        return home.Handle(request);
    }
}
