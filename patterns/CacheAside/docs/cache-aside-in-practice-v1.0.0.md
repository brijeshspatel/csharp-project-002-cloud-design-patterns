---
doc_id: cache-aside-in-practice
title: Cache-Aside in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Cache-Aside in practice

Supporting material for [the Cache-Aside pattern](../README.md).

## Invalidation and expiry are both necessary

They look like alternatives and they are not.

**Invalidation** is precise: the moment a value changes, drop its cached copy. The staleness window
shrinks to almost nothing. It works only for writes that pass through code that remembers to do it —
and there is always other code. A batch job. A support engineer with database access. Another
service sharing the store. A replication process.

**Expiry** is imprecise and unconditional. It does not care how the value changed or who changed it;
after the time-to-live nothing is trusted. It is the backstop that makes the pattern safe against
writers you do not control.

Use both. Invalidate what you can see, expire everything as a floor. A cache with only invalidation
serves a stale value for ever the first time something writes behind your back; a cache with only
expiry is always as stale as its TTL allows.

## The stampede

A popular entry expires. A hundred concurrent requests miss simultaneously. All hundred read the
store, all hundred compute the same value, all hundred write it back.

The database sees a hundredfold spike at the exact moment the cache stopped protecting it, and if
the value is expensive the spike can be enough to cause the outage the cache existed to prevent.
Worse, it repeats every TTL.

Three mitigations, in increasing order of complexity:

* **Jitter the expiry.** Add a random fraction to each entry's TTL so entries written together do
  not expire together. Cheap, and it solves the correlated case.
* **A per-key lock.** The first miss loads; the others wait for it. One store read instead of a
  hundred, at the cost of coordination.
* **Refresh ahead.** Refresh a hot entry shortly before it expires, in the background, so it never
  actually misses.

The single-process, single-threaded model in this folder cannot exhibit the stampede at all, which
is precisely why it is worth describing here.

## Per-instance caches multiply the miss rate

`IMemoryCache` is per process. Run ten instances and there are ten caches, each warming
independently: a value read once per instance costs ten store reads, not one, and a deployment
resets all ten at once.

A shared cache — Redis — fixes the arithmetic and introduces a network hop, a dependency that can
fail, and serialisation costs. For small, very hot values the in-process cache often still wins
despite the duplication.

The common answer is both: an in-process cache in front of a shared one. It is also the answer with
two invalidation problems instead of one, so it is worth being deliberate rather than accumulating
layers.

## Choosing the time-to-live

The useful question is not "how long is this data valid?" — usually the honest answer is "until
somebody changes it, which could be any moment". It is **"how wrong can I afford to be, and for how
long?"**

That question has answers. A product name: hours. A price: minutes, because a customer seeing a
stale price is a support conversation. Stock level: seconds or not at all. A feature flag: seconds,
because the point of a flag is turning something off quickly.

Then check the traffic. A TTL shorter than the mean interval between reads means almost every read
is a miss, and the cache is pure overhead. TTL and read rate have to be considered together.

## Caching absence, deliberately

If a lookup for a missing key is not cached, a hot missing key hammers the store on every request —
and missing keys are often hot, because they come from bots, scanners and broken links.

Caching absence fixes that and introduces a new failure: a record created shortly after a negative
lookup is invisible until the negative entry expires. "Not found" for something that exists is a
worse bug than a slightly stale price.

The usual compromise is a short TTL for negatives — much shorter than for values — and invalidating
on create. The implementation here does not cache absence at all, which is the simpler and safer
default.

## What this repository's model leaves out

The cache is per-process and single-threaded, so the distributed case, the stampede and eviction
under memory pressure cannot occur. There is no cache failure, so the discipline of treating an
error as a miss is described rather than exercised. Absence is not cached. There is no jitter, no
per-key locking and no refresh-ahead. And the clock is advanced by hand, so expiry happens exactly
when the demonstration says rather than when it would.
