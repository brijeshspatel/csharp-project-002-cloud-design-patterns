# Bulkhead in practice

Supporting material for [the Bulkhead pattern](../README.md).

## Thread-pool starvation, which is what this actually prevents

The abstract description — "isolate elements into pools" — undersells the failure. Here is the
concrete version.

A .NET application serves checkout and reporting. Reporting queries a warehouse that becomes slow:
queries that took 200ms now take 30 seconds. Nothing errors; everything is merely slow.

Each in-flight reporting request occupies a thread while it waits. The thread pool grows, but
deliberately slowly — roughly one thread per second beyond its minimum, because a pool that grew
instantly would thrash. Reporting requests arrive faster than that.

Within a minute, every available thread is waiting on the warehouse. **Checkout requests, which
never touch the warehouse, cannot get a thread.** They queue. Latency climbs. Health checks — which
also need a thread — start timing out, so the load balancer removes the instance, and its traffic
moves to the next instance, which repeats the process.

Nothing crashed. No exception was thrown until timeouts began. Every dependency except the warehouse
was healthy the entire time. And the whole application is down.

A bulkhead stops this at the first step: reporting has, say, ten slots. The eleventh reporting
request is refused instantly. Checkout's threads are never taken because reporting cannot reach
them.

## Sizing partitions, and the arithmetic of stranding

Two numbers per partition: how much it may use, and how much is therefore unavailable to everybody
else.

**Total capacity is not the sum of partitions.** Partition 100 slots into four pools of 25, and a
single workload can now use 25 rather than 100 — even when the other 75 are idle. That stranding is
the price of isolation, and it is worth naming rather than discovering.

Useful starting points:

* Size by **measured concurrency**, not by traffic share. A partition needs enough slots for its
  normal concurrent work plus headroom, and concurrency is `arrival rate × duration`.
* **Protect the critical path generously.** Checkout getting a big share and reporting a small one is
  usually right; the loss is only realised when reporting is busy, which is exactly when you wanted
  it constrained.
* **Leave the sum below the real resource limit.** Partitions summing to more than the underlying
  pool have not isolated anything — they can still collectively exhaust it.

## Reject, queue, or wait a little

The version here has two answers: a slot, or an immediate refusal. Real bulkheads usually offer a
third.

* **Refuse immediately.** Fastest feedback, most rejections. Right where the caller has a fallback,
  or where the work will be retried anyway.
* **Bounded queue.** A few waiting slots absorb bursts without unbounded growth. Better utilisation,
  and it reintroduces waiting — which is what the pattern was meant to remove, so the bound must be
  small and enforced.
* **Wait with a timeout.** Wait briefly, then refuse. Middle ground, and the timeout is now a third
  number to size.

The mistake is an *unbounded* queue, which is not a bulkhead. It converts rejection into unbounded
memory growth and unbounded latency, and the compartment stops being watertight.

## The leak that kills a partition slowly

The bug this pattern most often ships with is a slot released on the success path only.

Every failure permanently shrinks the pool. A partition with ten slots and a 1% failure rate loses a
slot every hundred requests; after a thousand it is dead, and rejects everything. The symptom — a
partition that works after a restart and degrades over hours — looks like a memory leak and gets
investigated as one.

`using`, or `try`/`finally`, is the whole fix, and `Releases_the_slot_when_the_work_throws` exists to
make sure it stays fixed.

## Process isolation is the stronger form

Everything here is in-process, which bounds what it can protect. Semaphores do not isolate memory, a
stack overflow, an `OutOfMemoryException`, a crash, or a deployment that takes the host down.

Separate processes do: separate App Service plans, separate Container Apps, separate AKS deployments
with their own resource limits. The isolation is enforced by the platform rather than by your code
remembering to acquire a slot, and it survives failures that no in-process mechanism can contain.

The trade is cost and operational surface — more things to deploy, monitor and pay for. In-process
bulkheads are the cheap 80%; process isolation is what you reach for when the workloads genuinely
must not share a fate.

## What this repository's model leaves out

Nothing is concurrent: slots are held deliberately, so exhaustion is constructed rather than raced,
and no test exercises real contention. Nothing is starved, so the failure described at the top of
this document is explained rather than demonstrated. There is no bounded queue, no acquisition
timeout, no cancellation, and no metrics — and rejection rate per partition is the single most
useful thing to monitor about a real bulkhead. Partitions are fixed at construction.
