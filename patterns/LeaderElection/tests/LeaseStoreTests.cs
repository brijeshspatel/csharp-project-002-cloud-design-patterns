namespace LeaderElection.Tests;

/// <summary>
/// What leader election guarantees: that **at most one participant holds the
/// lease at any moment**, that the leadership ends by itself when a leader stops
/// renewing, and that the exclusive work therefore happens once.
///
/// The election is stepped and the clock injected. Nothing here races: a test
/// that elects a leader by starting threads and hoping passes locally and fails
/// in CI, and proves nothing either way.
/// </summary>
public class LeaseStoreTests
{
    private static readonly DateTimeOffset Midnight =
        new(2026, 8, 17, 0, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan LeaseFor = TimeSpan.FromMinutes(5);

    private static ElectionParticipant[] ThreeParticipants(LeaseStore store) =>
        [new("instance-1", store), new("instance-2", store), new("instance-3", store)];

    [Fact]
    public void Elects_one_leader_from_several_participants()
    {
        ManualClock clock = new(Midnight);
        LeaseStore store = new(clock, LeaseFor);
        ElectionParticipant[] participants = ThreeParticipants(store);

        foreach (ElectionParticipant participant in participants)
        {
            participant.TryBecomeLeader();
        }

        Assert.Single(participants, participant => participant.IsLeader);
        Assert.Equal("instance-1", store.Holder);
    }

    [Fact]
    public void Refuses_a_second_leader_while_the_lease_is_held()
    {
        ManualClock clock = new(Midnight);
        LeaseStore store = new(clock, LeaseFor);
        ElectionParticipant first = new("instance-1", store);
        ElectionParticipant second = new("instance-2", store);

        Assert.True(first.TryBecomeLeader());

        // Four minutes in, the lease is live. A second holder here is the whole
        // failure this pattern exists to prevent.
        clock.Advance(TimeSpan.FromMinutes(4));

        Assert.False(second.TryBecomeLeader());
        Assert.False(second.IsLeader);
        Assert.Equal("instance-1", store.Holder);
    }

    [Fact]
    public void Lets_a_successor_take_over_once_the_lease_expires()
    {
        ManualClock clock = new(Midnight);
        LeaseStore store = new(clock, LeaseFor);
        ElectionParticipant first = new("instance-1", store);
        ElectionParticipant second = new("instance-2", store);
        first.TryBecomeLeader();

        // The leader stops renewing — it crashed, or its host was reclaimed. It
        // cannot announce that; the expiry is what ends its leadership.
        clock.Advance(TimeSpan.FromMinutes(6));

        Assert.True(second.TryBecomeLeader());
        Assert.Equal("instance-2", store.Holder);
        Assert.False(first.IsLeader);
    }

    [Fact]
    public void Keeps_the_leader_while_it_renews_the_lease()
    {
        ManualClock clock = new(Midnight);
        LeaseStore store = new(clock, LeaseFor);
        ElectionParticipant first = new("instance-1", store);
        ElectionParticipant second = new("instance-2", store);
        first.TryBecomeLeader();

        clock.Advance(TimeSpan.FromMinutes(3));
        Assert.True(first.RenewLease());
        clock.Advance(TimeSpan.FromMinutes(3));

        // Six minutes of a five-minute lease, and still leader — because it
        // renewed at three. Renewal is what makes a short lease workable.
        Assert.True(first.IsLeader);
        Assert.False(second.TryBecomeLeader());
    }

    [Fact]
    public void Runs_the_exclusive_work_once_across_all_participants()
    {
        ManualClock clock = new(Midnight);
        LeaseStore store = new(clock, LeaseFor);
        ElectionParticipant[] participants = ThreeParticipants(store);
        int billingRuns = 0;

        foreach (ElectionParticipant participant in participants)
        {
            participant.TryBecomeLeader();
            participant.RunExclusiveWork(() => billingRuns++);
        }

        // Three instances woke up; the customer was billed once.
        Assert.Equal(1, billingRuns);
    }

    [Fact]
    public void Releases_the_lease_when_the_leader_steps_down()
    {
        ManualClock clock = new(Midnight);
        LeaseStore store = new(clock, LeaseFor);
        ElectionParticipant first = new("instance-1", store);
        ElectionParticipant second = new("instance-2", store);
        first.TryBecomeLeader();

        first.StepDown();

        // A clean shutdown hands over immediately rather than leaving the
        // cluster leaderless until the lease happens to expire.
        Assert.Null(store.Holder);
        Assert.True(second.TryBecomeLeader());
        Assert.Equal("instance-2", store.Holder);
    }
}
