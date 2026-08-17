---
doc_id: circuit-breaker-in-practice
title: Circuit Breaker in practice
type: explanation
version: 1.0.1
status: active
created: 2026-08-17
updated: 2026-08-18
---

# Circuit Breaker in practice

Supporting material for [the Circuit Breaker pattern](../README.md). The README says what the
pattern is and when to use it; this says what tends to go wrong once it is real.

## Concurrency is where this pattern is actually hard

The state machine is easy. Three configuration fields, three of mutable state and four
transitions, and the version in this folder is about eighty lines.

What makes production breakers difficult is that they are shared by definition — the whole value is
that one caller's discovery informs the others — and shared mutable state under concurrency is the
part the diagram does not show.

Two failure modes recur:

**The half-open stampede.** The circuit opens, a hundred callers block on their own work, the break
duration elapses, and every one of them reads the state as half-open in the same instant. All
hundred probe. The dependency, which had been getting no traffic and might have been recovering,
receives a hundred simultaneous calls and falls over again. The state exists to let exactly *one*
call through, and getting that right needs an atomic transition rather than a read followed by a
write.

**The lost failure.** Two callers fail at the same moment and both increment a non-atomic counter.
The count goes up by one. With a threshold of three and a service that is entirely down, the
circuit can take considerably longer to open than the configuration says it will.

The implementation in this folder is **deliberately not thread-safe**, and says so in its README.
It is written to make the state machine legible. A production breaker uses interlocked operations
or a lock around the transition, and the READMEs of real libraries are worth reading for how they
handle it.

## Choosing the two numbers

**The threshold** trades false opens against slow detection. Too low and one unlucky burst cuts off
a healthy service; too high and every caller pays a full timeout many times before the breaker
helps. Consecutive failures are a poor signal on a low-traffic dependency, where three failures may
span an hour — which is why real implementations usually sample a failure *rate* over a window with
a minimum-throughput floor, so a breaker never opens on the strength of two calls.

**The break duration** trades recovery speed against probe cost. Too short and the probes themselves
become the load. Too long and a service that recovered in seconds stays cut off for minutes. Where
recovery time is genuinely unknown, growing the break on each failed probe — the same reasoning as
Retry's backoff — is a better answer than one fixed number.

## What a breaker does to the caller

A breaker changes the *shape* of the caller's failures, not just their frequency, and callers are
often not written for the new shape.

Before, failures were rare and slow. After, they are common and instant. Code that logs every
exception now logs a hundred a second. A dashboard counting errors shows a spike at the exact
moment the system started protecting itself. And a fallback path that had never really been
exercised is suddenly the main path.

**The fallback is the part to test.** A breaker without one converts a slow error into a fast error,
which is an improvement of degree. A breaker with a cached answer, a degraded response or a queued
write is an improvement of kind.

## Why it pairs with Retry rather than replacing it

They answer different questions, and each is bad at the other's job.

Retry alone keeps calling a dependency that is down, once per caller per attempt. A breaker alone
fails a request that a single retry would have satisfied, because one connection reset is not
evidence of an outage.

Composed, the retry handles the blip and the breaker handles the outage — with the breaker
**outside** the retry, so that once it opens the retry stops attempting at all. Inverted, the retry
would sit outside and dutifully re-attempt a call the breaker is refusing, burning the retry budget
on `CircuitOpenException` and learning nothing.

## What this repository's model leaves out

The clock is advanced by hand, so nothing here exercises real elapsed time; the breaker is not
thread-safe, so nothing here exercises the concurrency described above; there is one dependency
rather than a fleet, so nothing shows per-endpoint breakers or partial failure; and no metrics are
emitted, so the operator experience — the thing that makes a breaker diagnosable in production — is
absent entirely.

Those are real and they are not demonstrated here. The README's **In Azure** section says the same
thing, and it is repeated because the concurrency caveat is the one a reader of this folder is most
likely to carry away wrongly.
