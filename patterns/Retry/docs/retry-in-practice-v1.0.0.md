---
doc_id: retry-in-practice
title: Retry in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Retry in practice

Supporting material for [the Retry pattern](../README.md). The README says what the pattern is and
when to use it; this says what tends to go wrong with it once it is real.

## The three numbers, and how to choose them

A retry policy is mostly three decisions, and the defaults people reach for are usually wrong in
the same direction — too many attempts, too little backoff, no jitter.

**Attempts.** Past three or four, additional attempts rarely convert a failure into a success; what
they reliably do is extend the failure. If a dependency has not recovered after four growing waits,
it is having an outage, not a blip, and the answer is to stop calling it.

**Base delay and cap.** The base sets how quickly the first retry happens; the cap sets the worst
case. Exponential growth from 100ms reaches 12.8 seconds by the eighth attempt and 1.7 minutes by
the eleventh, so a policy without a cap is a policy whose worst case nobody has calculated.

**Jitter.** Not optional, and the reason is not politeness. Clients that fail together retry
together: an outage synchronises every caller onto the same clock, and they then return in a
thundering herd precisely when the dependency is most fragile. Jitter is what desynchronises them.

## The idempotency trap

Retry is safe when repeating the operation is safe, and that is a property of the operation, not of
the policy. The trap is that the *failure* often gives no information about whether the operation
happened.

A timeout is the clearest case. The request may never have arrived; it may have arrived, been
processed and had its response lost. Retrying is correct in the first case and duplicates work in
the second, and the caller cannot tell which it is.

Where the operation is not naturally idempotent, the fix is on the dependency's side rather than
the caller's: an idempotency key the server de-duplicates on, a conditional write, or a
compensating action. Tier 2's Idempotent Consumer is the same problem seen from the receiving end.

## Why retry pairs with Circuit Breaker

They answer different questions:

* Retry asks *"should I try this call again?"* — a decision about one operation.
* Circuit Breaker asks *"should I be calling this dependency at all right now?"* — a decision about
  the dependency, shared across every caller in the process.

Retry alone will keep sending traffic at a dependency that is down, once per caller per attempt,
which is load a struggling service does not need. A breaker in front of the retry stops that,
failing fast while the dependency is known bad and letting a probe through occasionally to find out
when it recovers.

The source catalogue makes this pairing explicit, and it is why Circuit Breaker follows Retry in
tier 1 rather than sitting elsewhere.

## Retrying is not free at the caller

The cost that surprises people is not latency, it is occupancy.

A thread, a connection from a pool, an open transaction or a held lock that persists across the
waiting is a resource nobody else can use for the whole retry budget. Under load this turns one
slow dependency into resource exhaustion across the caller — the failure mode Bulkhead exists to
contain, and a good reason to keep the total retry budget small and to release scarce resources
before waiting.

## What this repository's model leaves out

The implementation injects `ITimeSource` and the tests drive `RecordingTimeSource`, so no delay is
ever really waited for. That buys a deterministic, instant test suite and it costs everything that
only appears when time genuinely passes: interaction with request timeouts, cancellation during a
wait, thread-pool behaviour under concurrent retries, and the herd effects jitter exists to
prevent.

Those are real and they are not demonstrated here. The README's **In Azure** section says the same
thing, and it is repeated because it is the one claim a reader of this pattern is most likely to
over-read.
