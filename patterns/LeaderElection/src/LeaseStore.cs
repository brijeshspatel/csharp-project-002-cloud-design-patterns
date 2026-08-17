namespace LeaderElection;

/// <summary>Time, so a lease can expire without anybody waiting for it.</summary>
public interface IClock
{
    /// <summary>The current instant.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>A clock that moves only when told to.</summary>
public sealed class ManualClock : IClock
{
    /// <summary>Creates a clock reading <paramref name="start"/>.</summary>
    public ManualClock(DateTimeOffset start) => UtcNow = start;

    /// <inheritdoc />
    public DateTimeOffset UtcNow { get; private set; }

    /// <summary>Moves the clock forward.</summary>
    public void Advance(TimeSpan elapsed) => UtcNow += elapsed;
}

/// <summary>Who holds leadership, and until when.</summary>
/// <param name="Owner">Which participant.</param>
/// <param name="ExpiresAt">When it lapses unless renewed.</param>
public readonly record struct Lease(string Owner, DateTimeOffset ExpiresAt);

/// <summary>
/// The one thing every participant agrees on: a single lease, held by at most
/// one of them at a time.
///
/// **The expiry is what makes this safe.** A leader that crashes cannot
/// announce it — that is what crashing means — so leadership cannot depend on
/// the leader releasing it. Instead the lease lapses on its own, and the next
/// participant to ask gets it. Everything else in the pattern follows from
/// that: renewal exists because the lease expires, and the lease expires
/// because a leader may vanish silently.
///
/// A store that granted a held lease would elect two leaders, which is the one
/// outcome the whole pattern exists to prevent.
///
/// **This in-memory store is not synchronised**, so it satisfies the pattern's
/// "holds a lease atomically" precondition only when callers take turns, as the
/// stepped demonstration and tests here do. Shared across threads, its
/// check-then-act in <see cref="TryAcquire"/> could grant the same free lease
/// twice — the two-leader outcome itself. A real store (a blob lease, a
/// database row) provides the atomicity; this model assumes it.
/// </summary>
public sealed class LeaseStore
{
    private readonly IClock clock;
    private readonly TimeSpan duration;
    private Lease? lease;

    /// <summary>Creates a store whose leases last <paramref name="duration"/>.</summary>
    public LeaseStore(IClock clock, TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration.Ticks);

        this.clock = clock;
        this.duration = duration;
    }

    /// <summary>Who holds it now, or nothing where it has lapsed or was released.</summary>
    public string? Holder =>
        lease is { } held && clock.UtcNow < held.ExpiresAt ? held.Owner : null;

    /// <summary>
    /// Takes the lease for <paramref name="owner"/>, if it is free or already
    /// theirs. Refuses while another participant's lease is live.
    /// </summary>
    public bool TryAcquire(string owner)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);

        string? holder = Holder;
        if (holder is not null && !string.Equals(holder, owner, StringComparison.Ordinal))
        {
            return false;
        }

        lease = new Lease(owner, clock.UtcNow + duration);
        return true;
    }

    /// <summary>Extends the lease. Only the current holder may.</summary>
    public bool Renew(string owner) =>
        string.Equals(Holder, owner, StringComparison.Ordinal) && TryAcquire(owner);

    /// <summary>
    /// Gives the lease up. A clean shutdown hands over immediately rather than
    /// leaving the cluster leaderless until the lease happens to lapse.
    /// </summary>
    public void Release(string owner)
    {
        if (string.Equals(Holder, owner, StringComparison.Ordinal))
        {
            lease = null;
        }
    }
}

/// <summary>
/// One instance of an application, any of which could be the leader and only
/// one of which is.
///
/// **Leadership is derived from the store, never stored here.** A participant
/// that remembered "I am the leader" would go on believing it after its lease
/// lapsed — which is precisely how two leaders happen. Asking the store every
/// time is what makes expiry take effect without anybody being told.
///
/// **What this is not.** The other coordination patterns in this tier are about
/// a multi-step operation going wrong. This one is about exclusivity: making
/// sure that of several identical instances, exactly one does the thing that
/// must happen once.
/// </summary>
public sealed class ElectionParticipant
{
    private readonly LeaseStore store;

    /// <summary>Creates a participant contending for <paramref name="store"/>'s lease.</summary>
    public ElectionParticipant(string id, LeaseStore store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(store);

        Id = id;
        this.store = store;
    }

    /// <summary>Which instance this is.</summary>
    public string Id { get; }

    /// <summary>Whether it holds the lease **right now**, asked of the store.</summary>
    public bool IsLeader => string.Equals(store.Holder, Id, StringComparison.Ordinal);

    /// <summary>Contends for the lease.</summary>
    public bool TryBecomeLeader() => store.TryAcquire(Id);

    /// <summary>Extends its own lease, where it still holds one.</summary>
    public bool RenewLease() => store.Renew(Id);

    /// <summary>Hands leadership back on a clean shutdown.</summary>
    public void StepDown() => store.Release(Id);

    /// <summary>
    /// Does the work that must happen once — but only while actually leader.
    /// The check is here rather than at the call site because a participant that
    /// was leader a moment ago is the ordinary case, not an exotic one.
    /// </summary>
    public bool RunExclusiveWork(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (!IsLeader)
        {
            return false;
        }

        work();
        return true;
    }
}
