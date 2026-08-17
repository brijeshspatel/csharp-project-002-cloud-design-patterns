namespace EventSourcing;

/// <summary>
/// Something that happened, stated in the past tense because it already has.
/// </summary>
/// <param name="Kind">What happened: opened, deposited, withdrawn.</param>
/// <param name="Delta">Its effect on the balance; withdrawals are negative.</param>
/// <param name="Version">Its position in the stream, from one.</param>
public readonly record struct AccountEvent(string Kind, decimal Delta, int Version);

/// <summary>
/// An append-only store of event streams, one per account.
///
/// **Nothing here updates or deletes.** That is not an omission for brevity: an
/// event store that overwrites is a database with extra steps, and the history
/// it destroys is the only thing the pattern buys.
///
/// Appends carry the version the writer believed it was working from, and a
/// mismatch is refused. Without that check two writers who both read version
/// two would both append version three, and the decision made against the state
/// one of them saw would be lost with no trace — the one loss an event store is
/// supposed to make impossible.
/// </summary>
public sealed class EventStore
{
    private readonly Dictionary<string, List<AccountEvent>> streams = [];

    /// <summary>How many streams the store holds.</summary>
    public int Streams => streams.Count;

    /// <summary>The whole stream for <paramref name="accountId"/>, oldest first.</summary>
    public IReadOnlyList<AccountEvent> Stream(string accountId) =>
        streams.TryGetValue(accountId, out List<AccountEvent>? stream) ? stream : [];

    /// <summary>The version <paramref name="accountId"/> has reached; zero where it has no events.</summary>
    public int VersionOf(string accountId) => Stream(accountId).Count;

    /// <summary>
    /// The stream as it stood at <paramref name="version"/> — a prefix, which is
    /// how any past state is reconstructed.
    /// </summary>
    public IEnumerable<AccountEvent> StreamAsAt(string accountId, int version) =>
        Stream(accountId).Take(version);

    /// <summary>
    /// Appends <paramref name="change"/>, provided the stream is still at
    /// <paramref name="expectedVersion"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The stream has moved on.</exception>
    public void Append(string accountId, AccountEvent change, int expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(accountId);

        int actual = VersionOf(accountId);
        if (actual != expectedVersion)
        {
            throw new InvalidOperationException(
                $"Stream '{accountId}' is at version {actual}, not {expectedVersion}. " +
                "The decision behind this event was made against state that has changed.");
        }

        if (!streams.TryGetValue(accountId, out List<AccountEvent>? stream))
        {
            stream = [];
            streams[accountId] = stream;
        }

        stream.Add(change);
    }
}

/// <summary>
/// Current state, derived by replaying a stream. It is a **projection of the
/// events and never a second source of truth** — nothing is stored here that
/// the stream does not say.
///
/// There is deliberately no read model and no query interface: separating
/// reading from writing is CQRS, a neighbouring pattern that event sourcing is
/// often paired with and does not require. Keeping them apart is the only way
/// a reader can tell which pattern did what.
/// </summary>
public sealed class AccountAggregate
{
    private AccountAggregate(decimal balance, int version)
    {
        Balance = balance;
        Version = version;
    }

    /// <summary>The balance the events add up to.</summary>
    public decimal Balance { get; }

    /// <summary>The version replayed to.</summary>
    public int Version { get; }

    /// <summary>
    /// Rebuilds state from <paramref name="events"/>. An empty stream replays to
    /// initial state, which is why an account needs no special "does it exist?"
    /// case.
    /// </summary>
    public static AccountAggregate Replay(IEnumerable<AccountEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        decimal balance = 0m;
        int version = 0;

        foreach (AccountEvent change in events)
        {
            balance += change.Delta;
            version = change.Version;
        }

        return new AccountAggregate(balance, version);
    }
}
