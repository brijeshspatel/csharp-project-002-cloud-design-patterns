# Sharding in practice

Supporting material for [the Sharding pattern](../README.md).

## The shard key is a one-way door

Nearly every other decision in a system can be revisited. The shard key mostly cannot: once data
has landed under it, changing it means re-routing — physically moving — every record, while the
system serves traffic. That is why the key deserves more scrutiny than any schema decision.

The test is simple to state: **which questions must be cheap?** List the queries the system
actually runs, weighted by frequency. The key that lets the most of that traffic route to one
shard is the right key — even when a rarer query suffers for it. A key chosen for elegance
(`id` because it is the primary key) rather than for the traffic can shard a store perfectly and
still leave the dominant query fanning out.

Three shapes recur:

* **Tenant-shaped traffic** (SaaS): shard by tenant. Almost every query carries it.
* **User-shaped traffic** (consumer apps): shard by user id, and pair it with an index table for
  the login-by-email lookup that does not carry it.
* **Time-shaped traffic** (telemetry, logs): a pure time key concentrates every write on the
  newest shard. Compose it — device id first, time second — so writes spread while a device's
  history stays together.

## Hashed, ranged, and directory routing

**Hashed** (this folder's model): shard = stable hash of key, mod count. Spreads keys evenly with
no bookkeeping. Its costs: range queries by key lose locality, and changing the shard count
remaps nearly everything — which is why real systems use consistent hashing or fixed virtual
buckets mapped to physical shards, so a count change moves buckets, not keys.

**Ranged**: contiguous key ranges per shard. Range queries stay local; sequential keys make the
newest shard hot, and skewed ranges need splitting by hand.

**Directory**: a lookup table from key (or bucket) to shard. Most flexible — records can move
individually, placement can be deliberate — and the directory is now a component with the
availability the whole store needs. The Azure Elastic Database shard map manager is this,
productised.

Hash for spread, range for scans, directory for control — and most systems hash into virtual
buckets with a small directory over them, taking most of each.

## Hot shards: spread keys, concentrated load

Hashing distributes **keys** evenly. Load follows usage, and usage is never even: one tenant is
half the traffic; one device firehoses; one player is streamed by a million viewers. The shard
holding them becomes the ceiling the sharding was meant to remove.

The responses, in escalating order: cache the hot entity in front of the store; isolate it —
directory routing can give a whale tenant a shard of its own; or split beneath it, composing the
key with a second component so one entity's load spreads. What does not work is resharding
around a hot key with the same routing family — the hash already spread the keys; it is the load
that is lumpy.

## Rebalancing is an operation, not a detail

Shards fill unevenly, counts change, machines retire. Moving a bucket while serving traffic has
a standard shape: copy the bucket to its new shard while writes continue; apply the writes that
landed during the copy; cut the routing over; retire the old copy. The window between copy and
cutover is the hard part — it is the two-write problem again, at bucket scale, and it is why
"how do we rebalance?" should be answered before the first shard fills rather than after.

Platforms that shard natively — Cosmos DB above all — do this invisibly, which is most of what
is being paid for.

## Queries that refuse to shard

Some questions never carry the key: global search, cross-tenant analytics, leaderboards.
Fanning out on demand — this folder's `ScanAll` — is honest for rare questions. For frequent
ones, precompute instead: an index table maps a queried field to the shard key so the lookup
routes; a materialized view aggregates across shards ahead of the question; and analytics
belongs in a separate store fed asynchronously, not in fan-out queries over the operational
shards.

## What this repository's model leaves out

The shards are dictionaries in one process, so a fan-out costs four increments of a counter
rather than four network calls with the slowest setting the latency. The shard count never
changes, so the remapping cost of naive mod-count hashing never bites and no rebalancing story
is exercised. Load is uniform, so no shard runs hot. And the routing function has exactly one
consumer, so nothing shows it as the shared, frozen infrastructure it becomes.
