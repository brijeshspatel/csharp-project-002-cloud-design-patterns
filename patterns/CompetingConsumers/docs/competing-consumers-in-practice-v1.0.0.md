---
doc_id: competing-consumers-in-practice
title: Competing Consumers in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Competing Consumers in practice

Supporting material for [the Competing Consumers pattern](../README.md).

## The visibility timeout is where the difficulty lives

The version in this folder removes an item when it is claimed and puts it back if the consumer
fails. That is enough to show the guarantee and not nearly enough to run.

The problem it dodges: what if the consumer does not fail, but *dies*? No release is ever called,
and the message is gone.

Real brokers solve it with a **visibility timeout**. Claiming hides the message rather than deleting
it, for a stated period. Complete within that period and it is deleted; die, and it becomes visible
again for somebody else. The message is never lost and never permanently held.

That single mechanism creates most of the operational subtleties:

* **Too short, and messages are redelivered while still being processed.** Two consumers now do the
  same work simultaneously — the exact thing the pattern promised would not happen. The pattern
  still holds; the *timeout* was wrong.
* **Too long, and a dead consumer's work stalls** for the length of the timeout before anyone else
  can take it.
* **Long-running work needs the lock renewed** while it runs, which means the handler has to
  cooperate rather than just doing its job.

The timeout must exceed the slowest realistic processing time, not the average — and processing time
usually has a long tail nobody measured.

## At-least-once is the only delivery guarantee on offer

Exactly-once delivery does not exist. It cannot: the acknowledgement can be lost after the work is
done, and the sender cannot distinguish that from the work never happening.

So a message will sometimes be delivered twice, and this pattern makes it likelier than most —
every timeout, every crash, every release produces a redelivery.

**Idempotent handling is therefore a requirement, not a refinement.** Idempotent Consumer is the
pattern that supplies it, and a competing-consumers deployment without it has a correctness bug
waiting for its first bad day.

## Ordering is genuinely gone

This deserves stating plainly because it surprises people who added consumers to fix throughput and
found a different bug.

With one consumer, messages are processed in the order they arrived. With three, they are not.
Message 2 may complete before message 1, because a different worker took it and finished sooner.

Where that matters, the answer is not to abandon the pattern but to narrow the ordering requirement:
usually only messages *about the same thing* need ordering. Service Bus sessions and Event Hubs
partitions both do this — messages sharing a key go to one consumer in order, while different keys
process in parallel. Sequential Convoy is that idea as a pattern.

## Scaling has a floor and a ceiling

**The floor.** Consumers polling an empty queue cost money and produce nothing. Scale-to-zero, or a
push trigger, is what makes an idle queue free.

**The ceiling, and it is the one people hit.** Adding consumers only helps while the consumers are
the bottleneck. Ten workers writing to a database that was already saturated do not go faster —
they contend, retry, and time out, and the throughput graph gets *worse* as workers are added. The
symptom is unmistakable once you know it: latency rising while throughput falls.

Before scaling consumers, establish what the actual bottleneck is. If it is downstream, this pattern
will amplify the problem rather than solve it.

## The poison message

One message that always fails will be claimed, fail, be released, and claimed again — for ever,
consuming a worker's attention each time.

Every broker offers a delivery count and a dead-letter destination, and both need configuring
deliberately. Releasing to the *back* of the channel, as this implementation does, is a partial
mitigation: it stops the bad message blocking everything behind it, but does nothing about the
loop itself.

## What this repository's model leaves out

Consumers are pumped in a fixed rotation on a single thread, so nothing is concurrent — the
distribution guarantee is demonstrated but never raced. There is no visibility timeout, so the
consumer-dies-mid-message case cannot occur. There is no delivery count, no dead-letter queue and
therefore no answer to a poison message. Nothing is durable. And no two consumers ever contend for
the same item, which is the very race that removal-on-claim exists to win.
