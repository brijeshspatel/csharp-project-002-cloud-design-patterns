# Compute Resource Consolidation in practice

Supporting material for [the Compute Resource Consolidation pattern](../README.md).

## The tier's internal argument

Two of tier 6's six patterns push toward more copies: stamps for isolation, geodes for reach. This
one pushes toward fewer units. Read quickly, that looks like a contradiction.

It is not, because they answer different questions:

* **How much idle capacity am I paying for?** Consolidation's question. Its unit of concern is cost
  and operational surface.
* **How much of the system does one failure take with it?** Stamps' question. Its unit of concern is
  blast radius.
* **How far is the user from the service?** Geode's question. Its unit of concern is latency.

A single system commonly answers all three at once: stamps by tenant, each stamp internally
consolidating its small periodic jobs onto one worker, the whole thing replicated across regions for
the data that may travel. The granularities differ, so the answers do not collide.

The genuine tension is that **consolidation reduces the number of failure domains, and stamps exist
to increase it**. Where those meet — consolidating across a stamp boundary, say — the isolation
argument wins, because the money saved is small and the property lost is the whole point of having
stamps.

## Averages consolidate; peaks do not

The arithmetic that justifies consolidation is almost always done on averages, and the failure
almost always comes from peaks.

Two tasks each averaging ten per cent of a host look like a twenty per cent pair. If both peak at
midnight — and scheduled jobs overwhelmingly do, because humans pick round numbers — they are a
hundred and sixty per cent pair for ten minutes every night, and everything else on the host is
degraded during exactly the window that matters.

So the check before consolidating is not "do these fit on average" but:

* **When does each task actually run?** Stagger schedules deliberately; a job that runs at 00:07
  rather than 00:00 costs nothing and removes a collision.
* **What is each task's peak, not its mean?** Size against peaks that coincide, and if they cannot
  be staggered, treat them as simultaneous.
* **What happens when one runs long?** A nightly job that usually takes four minutes and occasionally
  takes ninety will meet the next task's window, and then both are slow.

## Bound each task, or the pattern's main risk is unmitigated

Consolidation without per-task limits is the noisy-neighbour problem waiting to happen, and the
mitigation is the Bulkhead pattern from tier 1 applied within the unit.

In practice that means resource requests and limits per container, a memory cap per process, a
bounded thread pool or connection pool per task — whatever the platform offers. The effect is that a
task exceeding its share is throttled or killed rather than being allowed to consume the host.

Two points teams get wrong. **A limit that is never hit is not evidence it is unnecessary** — it is
evidence it is working, or that the bad day has not arrived. And **limits change the failure mode
rather than removing it**: a capped task fails instead of slowing its neighbours, which is usually
better and is still a failure somebody must handle.

## Consolidation decisions rot

The grouping that made sense two years ago encodes assumptions about task sizes that have since
changed. The nightly report now covers four times the data. The index rebuild now runs hourly. The
licence sweep was replaced by something that is not small at all.

Nothing in the system notices. Utilisation drifts upward, jobs take longer, and the cause is a
decision nobody has revisited because it was never written down as a decision.

Worth building in from the start: **record why a group was formed** — the sizes and schedules
assumed — and review utilisation against that periodically. When a task has outgrown its group, move
it out; splitting is much cheaper than the incident that eventually forces it.

## Do not consolidate across a boundary that exists for a reason

The cost argument is easy to make and applies uniformly, which makes it tempting to apply across
boundaries drawn for other reasons: production and non-production, tenants of different tiers,
workloads with different data classifications.

Those boundaries exist because somebody decided the blast radius mattered. Consolidating across one
converts a compliance or isolation property into a line item, and the saving is rarely large enough
to be worth the conversation that follows an incident.

The rule that holds: **consolidate within a boundary, never across one.** If the money is
significant enough that this feels restrictive, the honest move is to reopen the boundary decision
explicitly rather than to erode it by deployment topology.

## What this repository's model leaves out

**There is no compute.** Tasks are objects and capacity is an integer, so utilisation is arithmetic
rather than measurement — nothing is actually slower, and "degraded" is a label rather than an
effect. There is no scheduling, so coinciding peaks cannot be demonstrated and the second section
above is entirely description. There is no crash, so shared failure is described rather than shown.
There are no resource limits, so the Bulkhead mitigation is named and never exercised. And there is
no billing, which is the pattern's primary motivation and is completely absent.
