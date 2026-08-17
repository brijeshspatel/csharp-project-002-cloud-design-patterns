---
doc_id: choreography-in-practice
title: Choreography in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Choreography in practice

Supporting material for [the Choreography pattern](../README.md).

## Events state facts; commands name recipients

This is the line that decides whether a system is choreographed or is an orchestrator that has been
rearranged.

An **event** is past tense and addressed to nobody: `PaymentAccepted`. The publisher does not know
who subscribes, does not know whether anybody does, and does not change when a subscriber is added.

A **command** is imperative and addressed to somebody: `ReserveStock`. Even sent over a topic, it
carries the knowledge that stock must be reserved next — which means the publisher knows the
sequence, which means the sequence lives in the publisher.

The tell is what happens when a step is added. In a genuinely choreographed system, deploying a new
subscriber changes nothing else. If adding a fraud check means editing the payment service so it
also publishes `CheckFraud`, the coordination never left; it was distributed into the participants,
which is the worst of both — an orchestrator with no single place to read it.

## Reconstructing state is the price, and it has a standard answer

Nothing in a choreographed system knows whether order 1042 is complete. Support asks; the honest
answer requires querying three services and stitching their partial views together.

The standard answer is a **read model**: a component subscribing to the same events and maintaining
the whole picture, purely for querying. That is CQRS, and the important property is that it is a
*consumer* — it decides nothing, nobody waits on it, and if it dies the operation is unaffected.
The moment anything starts asking it what should happen next, an orchestrator has been reinvented
with worse availability characteristics.

Two things make this work in practice: the read model must be rebuildable from the events, so it can
be dropped and recreated; and it must surface its own lag, because a dashboard that is quietly
thirty seconds behind produces confident wrong answers.

## The silent stop

Inventory has no stock, so it publishes nothing. Payment has already been taken. Shipping is not
waiting — it has no idea an order exists. **Nothing anywhere raises an error, because nothing was
expecting anything.**

An orchestrator would have noticed: it called a step and the step said no. In choreography there is
no caller, and "no event arrived" is indistinguishable from "no event was ever going to arrive".

Three responses, usually combined:

* **Publish failure events too.** `StockUnavailable` is a fact, and something can subscribe to it —
  a refund service, a customer notification. This is the cheapest fix and it is easy to forget,
  because the happy path works without it.
* **Give the operation a deadline.** Something outside the flow notices that an order reached
  `PaymentAccepted` and never reached `Shipped` within an hour. That watcher is exactly Scheduler
  Agent Supervisor, and it is the natural companion to choreography rather than a contradiction of
  it — it observes rather than drives.
* **Make incompleteness visible.** The read model above can answer "which orders have been stuck for
  more than an hour?", which converts a silent failure into a queue somebody works.

## Compensation is where choreography hurts most

Undoing an orchestrated operation is a coordinator running compensations in reverse — it knows what
completed, because it did it.

In choreography no component knows what completed. Compensation therefore has to be choreographed
too: publish `OrderFailed`, and every service that did something must recognise that it needs to
undo its own part. Each service now needs to know which of its actions relate to which operation,
and the reverse ordering that Compensating Transaction relies on is not available, because nothing
holds the order.

It can be done, and it is genuinely harder. **If an operation needs reliable compensation, that is
the strongest single argument for orchestrating it** — which is to say, for Saga.

## Cycles, and other things no compiler catches

A service reacting to an event that its own announcement eventually causes will loop for ever, and
nothing in the type system prevents it. The subscription graph is not written down anywhere, so the
cycle is invisible until it is running.

The practical defences are unglamorous: keep a rendered diagram of who publishes and subscribes to
what, generated from configuration rather than drawn by hand; put a hop count or correlation depth
on events and drop those exceeding it; and alert on event volume per correlation id, since a cycle
shows up as one order producing thousands of events long before anybody reasons it out.

## Duplicates are normal, so handlers must be idempotent

Every real broker delivers at least once. A redelivered `PaymentAccepted` must not reserve stock a
second time.

The usual mechanism is a processed-message log keyed by event id, checked before acting and written
after the work succeeds — which is Idempotent Consumer, and it is not optional here. Choreography
multiplies the number of independent handlers, and each one is a place where a duplicate can do
damage.

## What this repository's model leaves out

Publication is synchronous and in-process: no delay, no loss, no duplication, no reordering, and a
handler that threw would propagate to the publisher, which no broker does. There is no retry, no
dead-letter queue and no deadline, so the silent stop is permanent with nothing watching. There is
no read model, so reconstructing state is described here rather than shown. There is no failure
event and no compensation. And the journal's complete ordered history is scaffolding for
inspection — a real broker gives each subscriber its own stream and no single view of everything.
