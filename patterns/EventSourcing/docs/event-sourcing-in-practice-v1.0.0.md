# Event Sourcing in practice

Supporting material for [the Event Sourcing pattern](../README.md).

## Events are a contract that never expires

Every other schema in a system can be migrated: change the table, run the script, the old shape is
gone. An event store cannot work that way, because the old events are the record. An event written
in 2019 must still replay in 2029, in whatever shape it was written.

This has a practical consequence that surprises teams late: **the event schema is the most durable
interface in the system**, more durable than any public API. It deserves the scrutiny a published
contract gets.

Two mechanisms keep it manageable:

* **Upcasting.** On read, map old event shapes forward to the current one, in code, at load time.
  The store keeps what was written; the domain sees one shape. Upcasters accumulate and must
  themselves be kept forever, which is still far cheaper than rewriting history.
* **Additive change only.** New optional fields with sensible defaults cost nothing. Renaming or
  repurposing a field is where pain comes from, because the old meaning is still out there in
  events already written.

What does not work is editing stored events to the new shape. It destroys the audit property that
justified the pattern, and it does so quietly.

## Snapshots, and why they are not state

Replay is linear in stream length. An account with two hundred events replays instantly; one with
two million does not.

A snapshot is state at version N, stored so replay can start from there and apply only what
followed. The discipline is the same as every other derived structure in this tier: **a snapshot
is disposable**. Deleting all snapshots must cost time and nothing else. The moment a snapshot
holds something the events do not, it has become a second source of truth and the pattern's
guarantee is gone.

Practical notes: snapshot on a cadence tied to stream length rather than time; keep more than the
latest, so a bad snapshot can be skipped; and version snapshots separately from events, because a
change to derived state does not invalidate the events it came from.

## Deletion, and the law

Append-only and "delete my data" are in direct tension, and the tension is legal rather than
technical. The workable answers, in rough order of preference:

* **Keep personal data out of events.** Reference a subject by id and hold the details elsewhere,
  where ordinary deletion works. Most events do not need a name in them.
* **Crypto-shredding.** Encrypt personal fields with a per-subject key; erasure destroys the key.
  The stream stays complete and its personal contents become permanently unreadable — the shape of
  history survives, the substance is gone.
* **Rewriting the stream.** Technically possible, and it breaks every guarantee the pattern
  offers, including any auditor's ability to trust it. Last resort.

The decision belongs at the start. Retrofitting crypto-shredding onto years of plaintext events is
a migration of exactly the kind event sourcing is bad at.

## Optimistic concurrency is the whole concurrency story

Expected-version checks are not an optimisation; they are how correctness is maintained without
locks. Two writers read version 2, both decide, both append. Accepting both silently loses the
decision one of them made against state that no longer held — the exact loss the pattern exists to
prevent.

Refusing the second append pushes the choice up to the caller, which is where it belongs, and the
caller has three options: re-read and retry (usually right — see Retry), surface the conflict to a
human, or merge, where the domain genuinely allows it. What matters is that the conflict is
*named*: the store can say which version was expected and what has happened since, which a
last-write-wins store cannot.

## The events are the record — including the mistakes

A wrong event is not deleted or edited; a compensating event is appended. The withdrawal that
should not have happened is followed by a correction, and both are visible. This feels wrong to
anyone used to fixing a row, and it is the point: the account was wrong for six minutes, and that
fact is often precisely what an auditor is asking about.

It also changes how bugs are investigated. Where state is derived, a wrong balance has exactly two
possible causes — the events are wrong, or the replay is. Both are inspectable, which is a better
position than a mutable store, where a wrong value carries no evidence of how it got that way.

## What this repository's model leaves out

The store is a dictionary of lists in one process: nothing is durable, nothing is partitioned, and
the version check is uncontended. There are no snapshots, so replay cost never bites. There is no
event versioning and no upcasting, which is the pattern's largest long-term cost. Nothing consumes
the stream — no projections, no subscribers — so the usual pairing with CQRS is visible only as
its deliberate absence. And there is no erasure story at all.
