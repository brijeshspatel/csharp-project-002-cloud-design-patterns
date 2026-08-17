---
doc_id: sequential-convoy-in-practice
title: Sequential Convoy in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Sequential Convoy in practice

Supporting material for [the Sequential Convoy pattern](../README.md).

## Choosing the grouping key is the whole design

Everything else follows from it, and both mistakes are common.

**Too coarse** and you serialise work that never needed it. Grouping by tenant rather than by account
means one busy tenant's entries process one at a time, and the parallelism you were protecting was
already spent. In the limit — one group for everything — you have reinvented the single ordered
queue, with extra bookkeeping.

**Too fine** and you break the ordering you needed. Grouping by transaction rather than by account
means each transaction is trivially ordered with itself and entries for the same account interleave
freely, which is the bug you set out to prevent.

The test to apply is: **what two things, if swapped, would give a wrong answer?** Whatever contains
both is the group. For a ledger it is the account, because balance depends on order within an
account and on nothing else.

A second consideration: **how many groups will there be, and how evenly distributed?** A thousand
even groups parallelise beautifully. Three groups, one carrying 95% of traffic, serialises almost
everything — and the metric that reveals it is throughput per group, not aggregate throughput.

## The gap that never closes

This is the operational failure of the pattern, and it is silent.

A group holds messages 2 and 3 waiting for 1. Message 1 was lost, or dead-lettered after repeated
failures, or never produced because of a bug upstream. The group waits, holding memory, delivering
nothing — for ever. No error is raised: from the dispatcher's point of view it is behaving perfectly.

Three policies, and one must be chosen deliberately:

* **Wait indefinitely** — what this implementation does. Correct if a gap always eventually closes,
  and a permanent stall otherwise.
* **Skip after a timeout.** The group makes progress; ordering is violated exactly once, and the
  system must be able to survive that. Suitable where a later message supersedes an earlier one.
* **Dead-letter the group.** Stop, alert, and let a human decide. Safest for financial work, and it
  needs somebody actually watching the alert.

Whichever is chosen, **monitor the age of the oldest held message per group**. Held *count* is
noisy — it rises and falls normally. An oldest-held age that only climbs is a stuck convoy, and it
is invisible in any aggregate.

## Sequence numbers are the producer's problem

The dispatcher can only order what it can identify, and that means gapless per-group sequence
numbers assigned before the message is sent.

With one producer per group this is easy — a counter. With several producers writing to the same
group it is genuinely hard: two producers assigning "next" simultaneously either collide or leave a
gap, and a gap stalls the group for ever under the wait-indefinitely policy.

Options, roughly in order of preference: **make one producer own each group**; **allocate sequence
numbers from a shared store** with an atomic increment; or **order by something already
monotonic** — a database log sequence number, a change-feed offset — rather than minting one.

The last is usually the best answer when it is available, because it removes the coordination
problem entirely rather than solving it.

## Why Service Bus sessions are usually the right answer

Hand-rolling this is more work than it looks once durability is involved. The held buffer must
survive a restart, the expectation must be shared across consumer instances, and two consumers
processing the same group concurrently defeats the entire pattern.

**Service Bus sessions** solve exactly that. A session is locked to one consumer at a time, so
within a group there is no concurrency to coordinate; different sessions are consumed in parallel by
different workers; and the broker holds the state. The application supplies a `SessionId` and gets
the pattern.

The residual work is still real — the stuck-group policy, sequence assignment, and monitoring are
yours regardless — but the hard concurrency and durability parts move to the broker.

## What this repository's model leaves out

Nothing is durable: a restart forgets every expectation and every held message. There is no consumer
and no session lock — messages release into a list, where a real convoy needs the group locked to
one worker, and that lock is what makes it safe under competing consumers. There is no timeout,
dead-letter or stuck-group policy, so a gap waits for ever. The held buffer is unbounded. And
sequence numbers are supplied ready-made by the demonstration, so the producer-side difficulty above
is not exercised at all.
