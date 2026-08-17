---
doc_id: geode-in-practice
title: Geode in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-18
updated: 2026-08-18
---

# Geode in practice

Supporting material for [the Geode pattern](../README.md).

## Stamps and geodes, side by side

These two patterns are the reason this tier is ordered as it is, and holding them apart is worth
doing explicitly.

| Question | Deployment Stamps | Geode |
|---|---|---|
| Why more than one? | Isolation | Ubiquity |
| Where does a request belong? | Exactly one stamp | Anywhere |
| What is shared between copies? | Nothing | Everything |
| What does losing one cost? | Its tenants are refused | Latency, and nothing else |
| What is the price? | Cannot fail over | Replication lag, and write conflicts |
| What does adding one do? | Adds capacity for **new** tenants | Adds capacity and reach for **all** requests |

The two are also combinable, and often are: stamps by tenant, each stamp internally replicated
across regions. That gets isolation *and* regional resilience, at the cost of running both
mechanisms. The mistake is to build one while believing you have the other's properties.

## Consistency is a decision, not a default

"Eventually consistent" describes a family of behaviours, and choosing none of them means choosing
whatever the store does by default — which is rarely what anybody would have picked.

The questions worth answering explicitly, before the first write:

* **Where can writes happen?** Single-region writes remove conflicts entirely and add latency for
  distant writers. Multi-region writes remove that latency and introduce conflicts.
* **What happens when two regions write the same thing?** Last-writer-wins is simple and silently
  discards data. Merge functions preserve more and require knowing what the data means. Rejecting
  the second write pushes the decision to the caller.
* **What does a client see after its own write?** Read-your-writes is the one guarantee users notice
  the absence of — a user who edits a profile and then sees the old version assumes the save failed
  and does it again.
* **How stale is acceptable?** A catalogue can be minutes behind. A balance cannot.

A design that has answered those four is usually fine. One that has not will discover them
individually, in production, as separate incidents.

## The confidently wrong answer

The failure mode this folder demonstrates is the one that matters most, and it is easy to overlook
because nothing appears broken.

A node stops receiving replication. It is up, it is healthy by every liveness check, it answers
quickly — and it answers with data from before the last write. Its users see an old price, a
cancelled order still active, a permission that was revoked an hour ago. There is no error anywhere.

Two defences, and both are cheap relative to the problem:

* **Replication lag as a first-class metric, alerted on.** Not "is the node up" but "how far behind
  is it", measured per node. A node whose lag is growing monotonically has stopped replicating and
  will not recover on its own.
* **Staleness surfaced in the response.** A header or field saying how old the data is turns "the
  system is wrong" into "this reply is four minutes old", which is a different conversation and a
  much shorter one.

Testing with a node deliberately **behind** rather than **down** is what exercises this. Most
failure testing kills nodes, which the pattern handles beautifully; lag is the case it handles
least well, so it is the case worth rehearsing.

## Data residency is where geodes stop

The pattern's central mechanism — every node holds everything — is exactly what jurisdictional rules
frequently forbid. Personal data that may not leave a region cannot be replicated to nodes outside
it, and no amount of configuration makes that acceptable.

The usual resolutions:

* **Split the data.** Replicate the catalogue everywhere and keep personal data in its jurisdiction.
  Most systems have far more of the former than the latter.
* **Use stamps for the constrained parts.** A regional stamp holds what must stay regional; a geode
  network holds what may travel. This is the combination mentioned above, chosen for a legal reason
  rather than an operational one.
* **Regional geodes.** A geode network within a jurisdiction, several such networks worldwide, and
  no replication between them.

The one that fails is replicating everything and adding a filter later, because the data has already
left by then.

## Storage multiplies, and so does write cost

Every node holds everything, so five nodes means five copies. That is obvious stated plainly and
easy to forget when the node count is a configuration value.

It affects two budgets. **Storage** scales with the node count, which for a large dataset is the
dominant cost of the pattern. And **writes** are amplified: one logical write becomes N physical
ones, so a write-heavy workload pays the multiplier on every operation.

That is why the pattern suits read-heavy data. A geode network over data that is written as often as
it is read is paying N times for the writes to make the reads local, and the arithmetic usually does
not work.

## What this repository's model leaves out

**There is no geography.** Nodes are objects in one process, so there is no latency, no partition
and no distance — the entire reason the pattern exists is asserted rather than measured.
Replication is a synchronous loop, so lag is a flag rather than a consequence of distance. Writes
are ordered by construction, so conflicts cannot occur and conflict resolution is described but
never exercised. There is no data residency constraint. And nothing shows the storage multiplication
or write amplification that holding everything everywhere implies.
