---
doc_id: idempotent-consumer-in-practice
title: Idempotent Consumer in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Idempotent Consumer in practice

Supporting material for [the Idempotent Consumer pattern](../README.md).

## Why exactly-once cannot be bought

It is worth being precise, because brokers advertise things that sound like it.

A consumer processes a message and acknowledges. Between those two events the process can die, the
network can drop the acknowledgement, or the broker can fail over. The broker's view is identical in
all three cases and in the case where the consumer never started: **no acknowledgement arrived.**

It has two options. Redeliver, and risk the work happening twice — at-least-once. Or do not
redeliver, and risk the work never happening — at-most-once. There is no third option, because the
information needed to choose correctly does not exist anywhere.

What is marketed as "exactly-once" is always at-least-once delivery paired with de-duplication
somewhere, and de-duplication is bounded — by a time window, a store size, or a partition. **This
pattern is where you put the half you control.**

## The gap this implementation still has

The handler does the work, then records the message. Two operations, not one.

Crash in between and the work has happened, nothing is recorded, and the redelivery does it again.
The pattern has narrowed the window from "always" to "a crash in a specific millisecond" — real
progress, and not a guarantee.

Closing it needs both operations in **one transaction**, which is possible exactly when the log and
the work share a store:

```
BEGIN
  INSERT INTO processed_messages (message_id) VALUES (@id)   -- unique constraint
  UPDATE accounts SET balance = balance - @amount WHERE id = @account
COMMIT
```

The insert fails on a duplicate, the transaction rolls back, and nothing happens twice. Where the
work writes somewhere the log cannot reach — a third-party API, an email provider — the window stays
open and the right response is to say so rather than to imply otherwise.

## Read-then-write is a race, and a unique constraint is not

The implementation here checks whether a message has been seen and then records it. Single-threaded,
that is fine. Concurrently, it is a classic race: two deliveries of the same message both read "not
seen", both do the work, both record it. Competing Consumers makes concurrent redelivery entirely
normal.

**The fix is a unique constraint, not a better check.** Let the database decide: insert first, and if
it fails with a uniqueness violation, this is a duplicate. The decision then happens in one atomic
operation rather than across two, and there is no window to lose.

This matters more than it looks, because the read-then-write version passes every single-threaded
test — including the ones in this folder.

## Key on the identifier, never on the content

Tempting: hash the message body and treat identical bodies as duplicates.

It is wrong. A customer legitimately buying the same item twice for the same amount in the same
minute produces an identical body, and the second purchase is silently swallowed. This failure is
particularly unpleasant because the system reports success.

De-duplicate on a **stable message identifier** assigned by the producer and preserved across
redelivery. If the producer does not supply one, that is the bug to fix first — no amount of
cleverness downstream substitutes for it.

## The log grows for ever unless you prune it

Every processed message adds a row that is never read again after the redelivery window closes. On a
high-volume queue that is millions of rows a day, on the hot path of every message.

Pruning is necessary and reintroduces a window: an entry deleted before its message's last possible
redelivery means that redelivery is worked again.

So retention must exceed the broker's maximum redelivery interval — including the pathological cases:
a message that has been dead-lettered and manually replayed weeks later, or a consumer group that was
paused during an incident. Pick the retention from that number, not from storage cost, and write down
which number you used.

## What this repository's model leaves out

Nothing is durable: a restart forgets every processed message, so every redelivery would be worked
again. Nothing is concurrent, so the read-then-write race cannot occur — and it is the reason real
implementations need a unique constraint rather than a check. The log and the ledger are separate
stores, so the crash-between-work-and-record window is real and not demonstrated. There is no
pruning and therefore no retention question. And no broker is present: redelivery is simulated by
calling the handler twice.
