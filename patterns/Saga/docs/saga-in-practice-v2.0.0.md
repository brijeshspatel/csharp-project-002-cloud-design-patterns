# Saga in practice

Supporting material for [the Saga pattern](../README.md).

## The log narrows the window; idempotency closes it

A saga writes `Started`, performs the step, then writes `Completed`. A crash can land between any
two of those, and one gap is genuinely ambiguous: the process died after calling the payment
service and before recording the outcome. The log says `Started` and nothing more. Was the card
charged?

Nothing in the log can answer it, because the information never got there. There are only two
honest responses:

* **Ask the downstream service.** Query by an idempotency key the saga generated before acting —
  "has charge `order-1042-payment` been applied?" This is the strongest answer and requires the
  service to support it.
* **Retry, and require the step to be idempotent.** Send the same request with the same
  idempotency key; a service that has already applied it returns the original result rather than
  charging again.

Both rest on the same thing: an identifier chosen by the saga *before* the call, stable across
retries. A saga whose steps are not idempotent has a log that narrows the window and a failure mode
that still charges the customer twice.

`Started` earns its place here. Without it, a replacement cannot distinguish "never attempted" from
"attempted, outcome unknown" — and those want different handling.

## Orchestration versus choreography, decided once

A saga can be driven by a coordinator that calls each service in turn — orchestration, which is
what this folder implements — or by services reacting to one another's events with nobody in
charge, which is Choreography, implemented separately in this tier.

Orchestration gives you one place that knows the sequence, one place to look when an order is
stuck, and one component to change when the sequence changes. It costs a component that every
service depends on, and a coordinator that becomes a growing repository of business logic.

Choreography removes that component and distributes the sequence into the services themselves.
Nothing is a bottleneck and nothing is a single point of failure — and **nothing knows the overall
state**, which is why "why is order 1042 stuck?" becomes an archaeology exercise across several
services' logs.

The two implementations in this tier run the same business operation for exactly this comparison.
The decision usually turns on how much you will need to answer that question.

## A stuck saga is an operational object, not an exception

Compensation fails. The refund service is down, the stock system rejects a release for an item that
has since been discontinued, and the saga cannot go forward or back.

Designs that treat this as an exception lose the saga. What works is treating it as a **work item**:

* Persist the saga in a terminal-but-unresolved state, distinct from both success and clean failure.
* Retain everything a human needs — the log, the identifiers, what remains in effect.
* Surface it somewhere with a queue and an owner, not only a log line.
* Make compensation re-runnable from that state, because the usual resolution is "the downstream
  service came back; try again".

A team that cannot list its stuck sagas does not know whether it has any.

## The log is not the business data

A saga log records what the saga did: attempted payment, completed it, countered it. It is not the
order, the payment or the shipment, and it should not become them.

The temptation appears when the log is the only thing that spans all three services and therefore
the only convenient place to answer cross-service questions. Answer them there once and the log has
quietly become a distributed database with no schema and no owner.

The distinction matters against a neighbouring pattern: in Event Sourcing the log **is** the data,
deliberately, and state is derived from it. A saga log is metadata about a process. Keeping that
line clear is what lets the saga log be truncated, archived or rebuilt without anybody losing an
order.

## Timeouts belong to a different pattern

A saga assumes each step eventually returns something — success or failure. Real services sometimes
return neither: the request is accepted, the agent goes away, and no answer ever arrives.

A saga with no timeout waits forever on that step. Adding one is reasonable, and it raises the
question a timeout always raises: after the deadline, did the work happen or not? That question —
detecting silence and deciding what to do about it — is Scheduler Agent Supervisor's entire subject,
and it is implemented separately in this tier for that reason.

The practical advice is to give every step a deadline, treat expiry as "outcome unknown" rather
than as failure, and resolve it by querying the downstream service with the idempotency key before
deciding whether to compensate.

## What this repository's model leaves out

The log is in memory, so it survives nothing; the demonstration simulates a lost coordinator by
discarding the object, which is the shape of the problem rather than the problem. There is no crash
between an action and its log entry, so the ambiguity idempotency exists to cover never arises.
A compensation that reports failure is recorded as `CompensationFailed` rather than assumed to
have worked - but nothing retries or escalates it, so the log tells the truth and acting on it is
left outside the model. There are no timeouts. And nothing is concurrent — two coordinators cannot pick up the same saga, which
in a real system is what a lease or Leader Election prevents.
