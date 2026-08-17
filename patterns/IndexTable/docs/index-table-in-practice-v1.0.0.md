---
doc_id: index-table-in-practice
title: Index Table in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Index Table in practice

Supporting material for [the Index Table pattern](../README.md).

## The key, not the data — and why that line matters

An index table can hold three things, in increasing order of danger:

* **The primary key alone.** A lookup is two reads. A missed update leaves a dangling pointer,
  which the resolving read detects and can treat as not found. Wrong answers are impossible;
  missing answers are the failure mode.
* **The key plus a few hot fields.** A lookup is one read for those fields. A missed update now
  serves **wrong data**, silently, and nothing downstream can tell. This is a materialised view
  wearing an index's name, without the rebuild discipline that makes views safe.
* **The whole record.** The store is now duplicated, every write is doubled, and the two copies
  drift the first time one write fails.

The implementation in this folder holds the key alone, deliberately. When the second shape is
genuinely wanted, build it as a materialised view — the pattern that owns denormalised copies and
the rebuild story that keeps them honest.

## The two-write problem

The store does not know the index exists, so every mutation is two writes the application must
make. The interesting question is what happens between them, and after a failure of the second.

Write the **data first, index second**: between the writes, the record exists but cannot be found
by the indexed field. Queries under-report — usually tolerable, briefly.

Write the **index first, data second**: between the writes, the index points at nothing. A
resolving read that treats a dangling entry as not found makes this window safe too; one that
throws makes it an outage.

The failure cases decide the order. A lost index write leaves a record unfindable until a rebuild;
a lost data write leaves a dangling entry forever. Both argue for data first, index second, plus a
periodic rebuild that reconciles whatever the failures left behind.

Where the store offers transactions or batch writes across both tables — same partition, same
container — use them and the window disappears. Most partitioned stores offer it only within a
partition, which the index, keyed differently, is usually not in.

## Rebuild is one scan, so make it routine

Everything in the index is derivable from the store by a single scan. That makes the rebuild the
answer to most operational questions: integrity in doubt — rebuild; new field to index — build the
new table alongside and cut over; schema of the index wrong — rebuild into the right one.

A rebuild over live traffic has the same shape as a materialised view's: build alongside, then
swap. Building in place serves half an index to every query that arrives mid-build.

## Choosing the index key

The index key is the field queries arrive by — but partitioned stores add a wrinkle: the index
table itself is partitioned, and a badly chosen index key concentrates it.

Indexing users by email spreads well; emails are unique. Indexing orders by status produces three
partitions — `pending`, `shipped`, `delivered` — one of which holds nearly everything and becomes
the hot partition the primary store was partitioned to avoid. For low-cardinality fields, compose
the key — status plus date, status plus region — or accept that this field wants a different
pattern entirely.

## Normalised, denormalised, and where this pattern sits

A fully normalised store answers every question with joins it may not support; a fully
denormalised store answers one question perfectly and every other by rewriting the data. The index
table is the smallest step from the first toward the second: the data stays normalised, in one
place, in write shape — and each query shape that matters pays for exactly one small derived
structure.

That framing also says when to stop. One index table is cheap. Ten, each hand-maintained on every
write path, is an application reimplementing a relational database's index manager — badly. At
that point the honest options are a store that indexes natively or a query model built as views.

## What this repository's model leaves out

The index and the data are updated in adjacent statements in one process, so the two-write window
above never opens. There is no rebuild-while-serving, no reconciliation sweep, and no failure
between the writes. The index never grows enough for its own partitioning to matter. Each
omission is the section above it.
