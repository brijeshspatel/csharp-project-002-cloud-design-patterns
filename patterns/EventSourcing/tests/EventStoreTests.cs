namespace EventSourcing.Tests;

/// <summary>
/// What event sourcing guarantees: that the **events are the record** and state
/// is derived from them, that nothing is ever overwritten, and that any past
/// state can be reconstructed by replaying a prefix of the stream.
///
/// The last two are what a store that overwrites destroys, silently and
/// irrecoverably, so both are asserted directly.
/// </summary>
public class EventStoreTests
{
    private const string Account = "ACC-1";

    private static EventStore StoreWithHistory()
    {
        EventStore store = new();
        store.Append(Account, new AccountEvent("opened", 0m, 1), expectedVersion: 0);
        store.Append(Account, new AccountEvent("deposited", 500m, 2), expectedVersion: 1);
        store.Append(Account, new AccountEvent("withdrawn", -120m, 3), expectedVersion: 2);
        return store;
    }

    [Fact]
    public void Appends_an_event_to_the_stream()
    {
        EventStore store = new();

        store.Append(Account, new AccountEvent("opened", 0m, 1), expectedVersion: 0);

        Assert.Single(store.Stream(Account));
        Assert.Equal(1, store.VersionOf(Account));
    }

    [Fact]
    public void Rebuilds_current_state_by_replaying_the_stream()
    {
        EventStore store = StoreWithHistory();

        AccountAggregate account = AccountAggregate.Replay(store.Stream(Account));

        Assert.Equal(380m, account.Balance);
        Assert.Equal(3, account.Version);
    }

    [Fact]
    public void Keeps_every_event_after_the_state_changes()
    {
        EventStore store = StoreWithHistory();

        store.Append(Account, new AccountEvent("deposited", 40m, 4), expectedVersion: 3);

        // Four events, and the first is still the one that opened the account.
        // A store that overwrote would answer the balance correctly and have
        // destroyed the history that is the entire point of the pattern.
        Assert.Equal(4, store.Stream(Account).Count);
        Assert.Equal("opened", store.Stream(Account)[0].Kind);
    }

    [Fact]
    public void Rebuilds_state_as_it_was_at_an_earlier_version()
    {
        EventStore store = StoreWithHistory();

        AccountAggregate asAtTwo = AccountAggregate.Replay(store.StreamAsAt(Account, version: 2));

        // The balance before the withdrawal, reconstructed rather than recorded.
        Assert.Equal(500m, asAtTwo.Balance);
        Assert.Equal(2, asAtTwo.Version);
    }

    [Fact]
    public void Rejects_an_append_against_a_stale_expected_version()
    {
        EventStore store = StoreWithHistory();

        // A writer that read at version 2 and appends now has missed the
        // withdrawal; accepting this write would lose a decision made against
        // state that no longer holds.
        Assert.Throws<InvalidOperationException>(() =>
            store.Append(Account, new AccountEvent("withdrawn", -50m, 3), expectedVersion: 2));
    }

    [Fact]
    public void Reports_an_empty_stream_as_initial_state()
    {
        EventStore store = new();

        AccountAggregate account = AccountAggregate.Replay(store.Stream("ACC-UNKNOWN"));

        Assert.Equal(0m, account.Balance);
        Assert.Equal(0, account.Version);
    }
}
