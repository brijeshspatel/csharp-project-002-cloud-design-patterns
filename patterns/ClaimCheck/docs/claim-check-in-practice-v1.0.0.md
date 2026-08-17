---
doc_id: claim-check-in-practice
title: Claim Check in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Claim Check in practice

Supporting material for [the Claim Check pattern](../README.md).

## Lifetime coupling is the failure that bites

Splitting one thing into two creates a question nobody had before: **which one expires first?**

A message can sit on a queue far longer than anyone plans for. It is retried. It is dead-lettered
and replayed a fortnight later during an incident review. A consumer group is paused for a
migration. Meanwhile the blob container has a thirty-day lifecycle policy that somebody set
sensibly, in isolation, a year ago.

When the message is finally consumed, the payload has gone. The failure appears in a consumer that
has not changed, referencing a message that looks perfectly valid, pointing at a blob that no longer
exists.

**Payload retention must exceed the message's maximum possible lifetime**, and that maximum is
larger than the queue's default TTL: it includes the dead-letter window, manual replay, and however
long an incident might pause a consumer. Pick the number from that, write down which number you
used, and put the two settings somewhere they will be read together.

## Who deletes the payload

Tempting: the consumer deletes it after processing. Tidy, and it breaks the moment there are two
consumers.

With a topic and three subscribers, the first to finish deletes the payload and the other two find
nothing. The bug is intermittent, depends on which subscriber wins, and looks like a storage fault.

Options:

* **Retention policy on the store.** Nobody deletes; the store expires payloads on a schedule. This
  is almost always right, and it makes the retention question above explicit rather than implicit.
* **A reference count**, decremented by each consumer. Correct, and it needs to know how many
  consumers exist — which is precisely the thing a topic exists to stop the publisher knowing.
* **A janitor** that deletes payloads whose messages are provably consumed. Correct, and it is
  another moving part with its own failure modes.

Start with retention. Reach for anything else only when storage cost genuinely justifies it.

## The check is a capability

A claim check is a reference that grants access to data. Anyone who obtains it can fetch the
payload — including anyone with access to the bus, which is often a wider set of people and systems
than those authorised to read the payload itself.

That matters when the payload is sensitive and the bus is not, or when the same topic serves
subscribers with different entitlements.

Two mitigations, and they compose:

* **Scope and expire the check.** A shared access signature rather than a bare URI: read-only,
  limited to one blob, expiring on a timescale matched to the message's lifetime. That is Valet Key
  applied to this pattern.
* **Encrypt the payload**, with the key distributed separately from the bus. The check then grants
  access to ciphertext, and holding it is not enough.

Neither is modelled here — the check in this folder is a bare reference — and that gap is worth
knowing about before copying the shape into somewhere that carries personal data.

## The threshold, and why inline sends matter

The pattern is usually described as though every payload goes to the store. Doing that is wasteful:
a short message pays a store write, a store read, and two extra failure modes, to avoid a size
problem it never had.

A threshold fixes it, and the sender picks per message. The receiver does not care, because it
handles both transparently — which is what keeps the pattern from leaking into consumer code.

Where to set it: comfortably below the broker's limit, leaving room for headers and any envelope the
transport adds. A message that is 250KB of payload plus 20KB of headers is rejected by a 256KB
broker, and the error will name the message rather than the headers.

## What this repository's model leaves out

Nothing is durable and nothing is remote, so the round trip that motivates the inline threshold
costs nothing measurable here. There is no retention policy, expiry or lifecycle management —
`CollectAll` stands in for one, which is the only reason the lifetime hazard can be demonstrated at
all. There is no authorisation, so the check is a bare reference rather than a scoped, expiring
credential. There is no fan-out and no cleanup path, so the "who deletes it" question cannot arise.
And payloads are strings sized in UTF-8 bytes rather than streams, so nothing here is genuinely
large.
