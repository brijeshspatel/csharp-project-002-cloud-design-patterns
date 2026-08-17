---
doc_id: queue-based-load-leveling-in-practice
title: Queue-Based Load Leveling in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Queue-Based Load Leveling in practice

Supporting material for [the Queue-Based Load Leveling pattern](../README.md).

## A queue absorbs a spike; it cannot absorb a deficit

The arithmetic is unforgiving and gets ignored constantly.

If the average arrival rate is below the average processing rate, a queue smooths the difference and
the backlog drains between bursts. This is the pattern working.

If the average arrival rate is *above* the processing rate, the queue grows without bound. It does
not matter how large the buffer is or how bursty the traffic looks — the backlog gets longer every
minute, for ever. A queue has bought time and nothing else, and the time gets spent believing the
system is coping because nothing is erroring.

**The tell is the age of the oldest message, not the depth.** Depth fluctuates with bursts and looks
alarming when it is fine. Age climbing steadily over hours is the signature of a deficit, and the
only fix is more consumers or less work.

## Why the buffer must be bounded

An unbounded queue is not a safety feature, it is a deferred failure with worse timing.

Bounded, the queue refuses at a moment you chose, in a way you can handle: shed the request, return
an error, apply back-pressure to the producer. Unbounded, it accepts everything until memory or
storage runs out, and *then* fails — at the worst possible moment, in the least controlled way, and
usually taking the consumer down with it so the backlog cannot drain either.

Deciding what a full queue means is part of adopting the pattern:

* **Refuse the newest** — simple, and the fairest when all work is equal.
* **Shed the oldest** — right when data is perishable, as with live telemetry.
* **Shed by priority** — see Priority Queue.
* **Back-pressure the producer** — best where you control it, and impossible where you do not.

## The poison message

One message that always fails can stall a queue indefinitely: it is delivered, it fails, it returns
to the queue, and it is delivered again. Nothing behind it moves.

Every real broker has an answer — a delivery-count limit and a dead-letter queue — and it needs to be
configured deliberately rather than left at a default nobody has read. The dead-letter queue then
needs somebody to look at it, which is the part most often forgotten: messages arrive there silently
and sit for months.

## Durability is most of why you use a broker

The version in this folder is a `Queue<T>` in one process. Restart the process and the buffer is
gone.

That single difference is most of what a managed broker sells. Once the queue is durable, the
producer and consumer become genuinely independent: the consumer can be redeployed, scaled to zero,
or crash, and the work waits. Producer and consumer stop sharing a fate, which is the operational
benefit that makes the latency cost worth paying.

It also introduces everything durability implies — acknowledgements, visibility timeouts,
at-least-once delivery, and therefore duplicates. Idempotent Consumer is the pattern that deals with
the last of those, and it is not optional in a real deployment.

## Scaling consumers is a different pattern

Queue-Based Load Leveling smooths the rate. It does not make the consumer faster, and a backlog
drains at exactly the consumer's rate.

Draining faster means more consumers, which is Competing Consumers — and the two compose so
naturally that they are often described as one. Keeping them separate is worthwhile: this pattern's
question is "how do I stop refusing work?", and that one's is "how do I get through it faster?".

## What this repository's model leaves out

Nothing is durable. There is no acknowledgement, no visibility timeout, no redelivery, and therefore
no at-least-once semantics or duplicates. There is no dead-letter queue, so the poison message
described above cannot be demonstrated. Producer and consumer share a process, so the independence
that motivates the pattern operationally is absent. And the drain loop is driven explicitly by the
demonstration, so nothing here can silently fall behind — which is the failure mode the section
above says to watch for.
