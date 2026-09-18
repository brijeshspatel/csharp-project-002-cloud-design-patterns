# Leader Election in practice

Supporting material for [the Leader Election pattern](../README.md).

## A lease does not actually guarantee one leader

This is the thing to understand before relying on the pattern, and the model in this folder cannot
show it because everything shares one clock.

The guarantee a lease gives is: **at most one participant holds a lease according to the store**.
That is not the same as at most one participant *believing* it is the leader and acting on it. Two
situations break the gap open:

* **A paused process.** The leader is garbage-collecting, or its VM is suspended for live
  migration, for eight seconds. Its lease lapses; a successor is elected; the old process resumes,
  finishes the line of code it was on, and writes — believing itself leader, because from inside
  the pause no time passed.
* **Clock skew.** Store and participant disagree about when the lease ends. The participant renews
  a lease the store already considers lapsed, or considers itself leader for a window the store
  has given away.

Neither is exotic. Both are routine at scale, and neither is fixed by shortening the lease — a
shorter lease makes the window smaller and more frequent.

The fix is **fencing**, and it moves the guarantee to where it matters: the store issues a
monotonically increasing token with each lease, the leader stamps every downstream write with it,
and downstream systems reject anything stamped older than the highest they have seen. A stale
leader's writes are then refused by the system that would have been corrupted. The lease makes two
leaders unlikely; fencing makes the second one harmless.

Where the downstream system cannot enforce a token, be honest that the pattern is providing
*mostly once* rather than *exactly once*, and make the work idempotent so the overlap is
survivable.

## Sizing the lease

The lease length is a single knob with two costs pulling against each other.

**Long lease.** Cheap — renewals are rare. When a leader dies, the work stops for up to the whole
lease duration, because nobody may take over until it lapses.

**Short lease.** Fast failover, and renewal traffic every few seconds; a leader that misses one
renewal because of a slow moment loses leadership unnecessarily, causing an election that was not
needed and possibly a flapping cluster.

Two rules make this manageable:

* **Set the lease from the failover gap you can tolerate**, since that is what it is: the maximum
  time the work is not being done.
* **Renew at about a third of it.** That leaves room for two failed renewals before leadership is
  lost, which converts a transient blip from an election into a non-event.

Azure blob leases cap at 60 seconds for a reason: it is a length at which failover is quick and
renewal is not constant.

## Losing leadership in the middle of the work

Election is usually written as a gate — check, then do the work. That is right for a job lasting
milliseconds and wrong for one lasting minutes, because the lease can lapse while the work is in
flight and the check has already passed.

Long-running leader work should:

* **Re-check leadership at intervals**, and stop cleanly on losing it — the check belongs beside
  the work, not only at its start.
* **Keep renewing during the work.** A leader that stops renewing because it is busy is a leader
  that loses its lease for no reason.
* **Be interruptible.** Stopping half way must leave a state a successor can pick up from — which
  usually means the work also wants a log, at which point Saga is the neighbouring pattern.

## The store is now the thing that must not fail

Election concentrates a distributed availability question into a single arbiter. If the lease store
is unreachable, nobody can acquire or renew, and either everybody stands down — the work stops — or
everybody proceeds, which is worse.

The usual answers are to make the store one you already depend on (if the application cannot work
without its storage account, electing on a blob lease adds no new failure mode), and to decide
explicitly what participants do when the store is unreachable. **Standing down is almost always
correct**: it fails to the state where nothing happens, rather than to the state where everything
happens at once.

## Do not elect when you could partition

Leader election is often reached for when the real requirement is "don't do this twice", and that
requirement frequently has a cheaper answer:

* **Partition the work.** Three instances, three shards, each owning its own — no leader, no gap,
  and three times the throughput.
* **Let the queue arbitrate.** A message consumed by exactly one consumer, or a session lock that
  gives ordered single consumption per session, is election that somebody else operates.
* **Make the job idempotent and let all of them run it.** Sometimes cheapest of all: three instances
  attempt the billing run, the first write wins, the others no-op.

The pattern earns its place when the work genuinely cannot be split and no infrastructure is
already arbitrating.

## What this repository's model leaves out

One process, one clock: no clock skew, no pause, no partition — so the overlapping-leaders case that
the first section is entirely about cannot occur here. There is no fencing token, so nothing
protects against a leader that has silently lost its lease. Acquisition is stepped rather than
concurrent, so nothing exercises real contention on the store. There is no renewal timer — renewal
happens because the demonstration calls it. And the store cannot be unreachable, so the question of
what participants do when the arbiter is down never arises.
