---
doc_id: publisher-subscriber-in-practice
title: Publisher-Subscriber in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Publisher-Subscriber in practice

Supporting material for [the Publisher-Subscriber pattern](../README.md).

## Topic or queue: the question people get wrong

Both are "messaging". They answer opposite questions, and the cost of confusing them is high in
both directions.

| | Topic (this pattern) | Queue (Competing Consumers) |
|---|---|---|
| Who receives it | **Every** subscriber | **Exactly one** consumer |
| Adding a receiver | Another party learns about it | More throughput |
| The message is | A fact — "an order was placed" | A job — "render this thumbnail" |
| Wrong choice costs | The job is done N times | Only one party learns something they all needed |

The test is not technical, it is semantic: **is this a fact or a job?** A fact is announced to
whoever cares and the announcer does not know who that is. A job is given to somebody to do, once.

A useful smell: if adding a second receiver would be a *bug*, you want a queue.

## Events are facts, commands are instructions

Naming discipline keeps the two apart, and it decays quickly without it.

`OrderPlaced` is a fact — past tense, already true, and the publisher does not care what anyone does
about it. `SendInvoice` is a command — an instruction to a specific party, with an expectation.

Commands published on a topic go wrong in a particular way: the publisher now depends on somebody
being subscribed. It has a hidden expectation that nothing enforces, and nothing reports when the
expectation stops being met. The coupling has not been removed, only made invisible — which is worse
than the direct call it replaced.

**Past tense in the type name is a cheap and effective guard.**

## The failure this implementation does not have

Publishing here is synchronous and in-process: the publisher calls, subscribers run, the publisher
gets a result. That is fine for showing the fan-out and dishonest about the pattern's name, which
contains the word *asynchronously*.

The consequences of real asynchrony are all absent:

* **A subscriber that is down misses nothing** in a real broker, because its subscription queue
  holds the message until it returns. Here, it simply misses the event.
* **Retry and dead-lettering are per subscriber.** Billing failing does not cause fulfilment to see
  the message again. Getting that right in a hand-rolled implementation is most of the work.
* **The publisher never sees a failure.** Returning `PublishResult` to the publisher, as this does,
  is a convenience for the demonstration and precisely the coupling the pattern removes — a real
  publisher gets an acknowledgement from the *broker* and learns nothing about subscribers.

## Schema is a contract you cannot renegotiate

You do not know who your subscribers are. That is the benefit, and it is also why the event schema
is harder to change than an API.

With an API you can find the callers. With a topic you cannot — there may be a subscription created
by a team that has since reorganised, feeding a report somebody relies on monthly.

So: **additive changes only.** Add fields, never remove or repurpose them. Where a breaking change is
unavoidable, publish a new event type alongside the old, and retire the old one on a timescale set
by the slowest consumer you know about, plus a margin for the ones you do not.

## How much to put in an event

**Thin events** carry an identifier and little else; consumers call back for the detail. They stay
small and always current, at the cost of a callback per consumer per event — which is load on the
publisher, and a coupling of a different kind.

**Fat events** carry everything a consumer might need. No callbacks, and consumers work even when the
publisher is down — but the event is now a data feed, the schema is larger and harder to evolve, and
you may be publishing data some subscribers should not see.

Most systems land in between: enough to act on for the common case, an identifier for the rest.
Where events genuinely need to carry something large, Claim Check is the pattern that keeps the bus
out of it.

## What this repository's model leaves out

Delivery is synchronous and in-process, so the *asynchronously* in the pattern's own description is
not demonstrated. Nothing is durable, so a subscriber that is not present misses the event. There is
no retry, no dead-letter, and no per-subscriber delivery state. There is no filtering, so a
subscriber cannot express interest in a subset. The publisher receives a failure report, which a
real broker would never give it. And the whole thing runs on one thread, so ordering between
subscribers is fixed rather than concurrent.
