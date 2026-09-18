# Priority Queue in practice

Supporting material for [the Priority Queue pattern](../README.md).

## Starvation is the default, not the edge case

Strict priority has no answer to it, and the demonstration in this folder is deliberately built to
show that rather than describe it: a low-priority ticket arrives first and is still waiting after
five rounds, because urgent work keeps arriving.

This is not a bug in the implementation. It is what strict priority *means*. As long as the
high-priority arrival rate is at or above the service rate, the low lane is never reached — and it
does not matter how long anything has been waiting, because waiting time is not an input to the
decision.

Two remedies, and both cost the strictness that made the pattern attractive:

**Ageing.** An item's effective priority rises with how long it has waited: after five minutes a low
becomes a normal, after fifteen a high. Simple to explain, and it bounds the wait. The cost is that
priority is no longer a promise — a sufficiently old low-priority item will overtake a fresh
high-priority one, which is exactly what somebody paid to avoid.

**Weighted service.** The consumer takes, say, seven from the high lane, two from normal and one
from low, in rotation. Every lane makes progress at a guaranteed rate. The cost is that a high item
can wait behind low ones, which is the same objection in a different shape.

The choice has to be made deliberately. Deploying strict priority and *hoping* the low lane drains
is how a customer discovers a ticket nobody looked at for three weeks.

## Priority inflation

If callers choose the priority and nothing costs them anything, everything becomes high. It happens
quickly, and it is rational for each caller individually.

Once it happens the scheme is worse than useless: you have a field to store, route on and monitor,
and it no longer discriminates. The high lane is now the queue, and the other lanes are empty.

Defences, roughly in order of effectiveness:

* **Derive priority from policy, not from the request.** Customer tier, severity classification, or
  message type decides it — not a caller-supplied field.
* **Give high priority a quota.** A caller may mark N per hour as high; beyond that it is normal.
* **Make it visible.** A dashboard showing "98% of requests are high priority" is usually enough to
  start the conversation.

## Most brokers do not implement this

Worth knowing before designing around it: **Azure Service Bus and Storage queues do not serve
messages by priority within a queue.** Neither do most brokers. Queues are FIFO, and that is the
guarantee they are built to give quickly.

The pattern is therefore realised structurally — one queue per priority, and consumers allocated
across them. Priority becomes a routing decision at send time and a *consumer allocation* decision
at run time, rather than a property of the queue's discipline.

That has a pleasant side effect: weighted service falls out naturally. Assign seven consumers to the
high queue and one to the low, and the low queue makes guaranteed progress without anybody
implementing ageing.

It also means the number of priorities is now a number of queues, which is its own argument for
keeping it to three.

## Three priorities, not ten

More levels look more expressive and behave worse.

With ten, nobody can articulate the difference between four and five, so the assignment becomes
inconsistent between teams and over time. In practice everything collapses onto the extremes anyway,
which is the same distribution three levels would have given with less machinery.

Three works because the questions are answerable: *does this need to jump the queue* (high), *can
this wait until things are quiet* (low), *neither* (normal).

## What to monitor

Aggregate queue depth is actively misleading for a priority queue: it can look healthy while the low
lane is starving.

Watch **per-lane depth** and **per-lane age of the oldest item**. Age is the one that matters. Depth
rises and falls with load; an oldest-item age that only ever climbs is starvation, and it is
invisible in any aggregate.

## What this repository's model leaves out

Nothing is durable. Priority is strict with no ageing and no weighted service — deliberately, so the
starvation trade-off is visible rather than papered over. Consumers are not partitioned across
lanes, which is how the pattern is actually deployed on real brokers. There is no deadline or
service-level tracking, so "processed more quickly" is shown as ordering rather than measured as
latency. And priority is caller-supplied, with no policy layer and no quota, so the inflation
described above cannot be demonstrated.
