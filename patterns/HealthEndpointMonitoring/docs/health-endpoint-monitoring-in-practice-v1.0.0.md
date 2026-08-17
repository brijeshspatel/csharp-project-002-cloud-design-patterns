---
doc_id: health-endpoint-monitoring-in-practice
title: Health Endpoint Monitoring in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Health Endpoint Monitoring in practice

Supporting material for [the Health Endpoint Monitoring pattern](../README.md).

## The deep-check outage

This is the failure mode that turns a resilience pattern into an incident, and it catches teams who
did everything else right.

An application has a readiness check that verifies its database connection. Sensible: an instance
that cannot reach the database cannot serve, so it should not receive traffic.

The database has a brief problem — a failover, a transient network fault, ten seconds of trouble.
**Every instance** checks the same database, so every instance fails its readiness check at the same
moment. The load balancer removes every instance from rotation. There is now nothing serving, and
the outage is total rather than partial.

Worse, recovery can stall. Instances taken out of rotation may be restarted; restarting instances
all reconnect at once; the reconnection storm keeps the database busy; the checks keep failing.

Two things prevent it:

* **Do not let a shared dependency fail liveness.** Liveness answers "should I be restarted?" and
  must depend on nothing external. A database blip must never restart your fleet.
* **Prefer degraded over unhealthy** wherever the application can still do something useful. An
  instance that can serve reads from cache while writes fail is more valuable in rotation than out
  of it.

The general rule: **a health check should report what this instance can do, not what the whole
system can do.** If every instance reports the same answer, the check is measuring the dependency
rather than the instance, and taking them all out of rotation cannot help.

## Liveness and readiness are genuinely different questions

They get conflated because both return a status, and the consequences of conflating them are
asymmetric and severe.

| | Liveness | Readiness |
|---|---|---|
| Question | Should I be restarted? | Should I receive traffic? |
| Checks | Only in-process state | Dependencies too |
| Failure action | Process restarted | Removed from rotation |
| Cost of a false failure | **Restart storm** | Reduced capacity |

A liveness check that touches the database converts a dependency blip into a fleet-wide restart,
which is close to the worst automated response available. A readiness check that touches nothing is
useless, because it will report ready while the instance cannot serve.

A third, **startup**, is worth separating where warm-up is slow: without it, a liveness probe kills
instances that were merely still starting.

## The endpoint is code, and it fails like code

The case worth designing for is not "the dependency is down" — that is the case everybody thinks
about. It is "**the check is broken**".

A misconfigured connection string, a null reference in probe code, a library that throws on a
timeout instead of returning: any of these makes the check throw. An endpoint that does not catch
returns a server error, and the prober reads that as unhealthy — so an instance that was serving
perfectly well is removed from rotation because its *self-assessment* failed.

That is why this implementation catches everything a check throws and reports it as one unhealthy
entry with its message. The endpoint's own job is to answer, always.

It is also why the empty-endpoint case reports unhealthy. A configuration change that drops every
registered check should not silently produce a green light — that is a green result over an empty
measurement, and nobody would ever notice.

## Cost, and the arithmetic of probing

Probes look free and are not. A check running every five seconds across twenty instances is 345,600
executions a day. If each runs a real query, that is real database load, entirely for the purpose of
asking whether the database works.

Three mitigations, in order of preference: **cache the result** for a few seconds, so frequent
probes share one execution; **make checks cheap** — `SELECT 1`, not a representative query; and
**probe less often**, since the interval buys detection latency you may not need.

## What this repository's model leaves out

Checks run **sequentially**; a real endpoint runs them concurrently, so total probe time is the
slowest check rather than the sum. There is **no per-check timeout**, which is the single most
important omission here — a hanging check hangs the endpoint, and the prober is left waiting rather
than told. There is no result caching, no liveness-versus-readiness split, no HTTP surface, and
nothing probing on an interval. The clock is advanced by hand inside the checks, so slowness is
modelled rather than experienced.
