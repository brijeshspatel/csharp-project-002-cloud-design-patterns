# Messaging Bridge in practice

Supporting material for [the Messaging Bridge pattern](../README.md).

## The temporary component that never leaves

A bridge is built for a migration and justified by its end date. Then the migration slips, three
teams have not moved yet, and a component introduced as scaffolding is carrying production traffic
between two systems.

The way it goes wrong is unremarkable and predictable. Nobody owns it, because it belonged to a
project that has closed. It has no capacity plan, because it was never meant to carry this much. It
is on the critical path of two systems and appears on the architecture diagram of neither. And it is
the first thing to fall over in an incident that involves either side.

Two defences, and both are organisational rather than technical:

* **Write the decommissioning criteria down when the bridge is built**, in terms of observable
  facts: "removed when no producer has written to the legacy broker for thirty days". Then measure
  it, and put the number somewhere visible.
* **Give it an owner who is not the migration project**, since the project will end and the bridge
  will not.

## Translation loses things, and the loss is silent

The interesting failure is not a message that fails to cross. It is one that crosses successfully
with something missing.

The demonstration drops a `trace-id` going from the modern bus to the legacy broker, because there
is nowhere to put it. Nothing errors. The message arrives, is processed, and works — and three
months later somebody trying to trace a request through the estate finds the trail stops at the
bridge, with no indication that a bridge was involved.

Common casualties, in rough order of how often they cause trouble:

* **Correlation and trace identifiers**, as above.
* **Message properties used for routing or filtering**, where the target has no equivalent.
* **Scheduled delivery and time-to-live**, which many brokers express differently or not at all.
* **Ordering and session guarantees**, which almost never survive a hop.
* **Delivery-count and dead-letter history**, which resets, so a poison message looks fresh.

The mitigation is not to prevent all loss — often impossible — but to **enumerate it deliberately**
and tell the people who depend on the lost thing. A table in the bridge's own documentation, listing
what does not survive in each direction, is a small artefact that prevents a specific kind of long
and confusing investigation.

## Acknowledge in the right order, or lose messages

The version in this folder takes from the source and publishes to the target in two steps, with
nothing in between that can fail.

In reality both are network calls, and the order matters enormously:

* **Take, then publish.** If the publish fails, the message is gone from the source and never
  reached the target. **Lost.**
* **Publish, then acknowledge the source.** If the acknowledgement fails, the message is published
  and will be redelivered on the source. **Duplicated.**

The second is the right choice, because duplication is recoverable and loss is not. It makes the
bridge an at-least-once component, which means every consumer downstream needs to tolerate
duplicates — see Idempotent Consumer, and note that with two at-least-once hops the duplicate rate
is the sum, not the maximum.

Peeking rather than taking, as this implementation does for untranslatable messages, is the same
instinct: do not remove anything until the outcome is known.

## Loops

Bridging is often bidirectional, and a bidirectional bridge can feed itself.

A message crosses from legacy to modern. Something on the modern side republishes it — a
retry, a fan-out, an enrichment step. The bridge sees it on the modern bus and carries it back to
legacy. And round it goes, at whatever rate the bridge can manage, until somebody notices the
brokers are saturated.

The defence is the `source` header this implementation sets: a bridge refuses to carry a message it
can see it has already carried. It has to be applied deliberately, because the failure mode is not
obvious until it happens and is spectacular when it does.

## Do not let it grow

Bridges attract logic. It is the one place that sees both systems, so it is the tempting place to
put the enrichment, the routing rule, the small filter that saves changing a consumer.

Each addition is individually reasonable, and the result is a service carrying business rules that
nobody planned, that has no tests of its own, and that must now be understood by anyone changing
either side.

**Keep it a translator.** Where logic is genuinely needed, put it in a service on one side of the
bridge, where it can be owned and tested like anything else.

## What this repository's model leaves out

Nothing is durable, so the acknowledge-ordering problem above cannot occur here. There is no retry,
no dead-letter and no poison handling. Delivery is exactly-once in process, where two at-least-once
hops in reality make duplicates near-certain. Loop protection is only as good as the `source` header
being recorded — nothing refuses a message on the strength of it. And the two systems are
deliberately simple: real translation involves schemas, encodings, character sets and correlation
identifiers, which is where most of the work actually lives.
