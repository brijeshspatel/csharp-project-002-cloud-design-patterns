---
doc_id: throttling-in-practice
title: Throttling in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Throttling in practice

Supporting material for [the Throttling pattern](../README.md).

## Throttling and Rate Limiting are not two names for one thing

They are constantly conflated, including by people who have implemented both, and the confusion is
understandable: both count requests against a limit over a window, and the code can look almost
identical.

The distinction is **which end of the call you are standing at**, and it changes what the component
is *for*:

| | Throttling | Rate Limiting |
|---|---|---|
| Who runs it | The service that owns the resource | The client calling somebody else's service |
| The question | "What will I accept from this consumer?" | "How fast may I call, to stay welcome?" |
| The distinctive move | **Degrade** rather than refuse | **Wait** rather than be refused |
| Failure it prevents | Your service collapsing under load | Your client being throttled by theirs |
| Who is protected | You, and your other consumers | You, from somebody else's throttle |

A throttle that only rejects has thrown away its reason to exist and become a rate limiter facing
the wrong way. A rate limiter that rejects its own caller instead of pacing has become a throttle
applied to the wrong party.

The two are complements: a well-behaved client rate-limits itself so that a well-run service rarely
has to throttle it.

## The distributed counter is the whole difficulty

The version in this folder keeps usage in a `Dictionary` in one process. That is enough to show the
decision logic and nothing like enough to run.

Run two instances behind a load balancer and each keeps its own counter, so a documented limit of
100 per minute is really 200 — and it drifts with your instance count, which autoscaling changes
without telling anybody.

Sharing the counter introduces the real design problem:

* **A round trip per request.** A distributed store consulted on every request adds its latency to
  every request, including the ones that will be accepted.
* **Contention on hot keys.** The noisiest tenant is exactly the key most contended, so the
  throttle's own hot path degrades under the load it exists to manage.
* **A dependency that can fail.** If the store is unavailable, do you fail open — admitting
  everything, at the moment your platform is least healthy — or fail closed, refusing everybody
  because the *limiter* broke? Both answers are bad, and the choice must be deliberate.

The common compromise is local counters with a per-instance share of the global budget, periodically
reconciled: approximate, cheap, no hot path dependency, and wrong at the edges in a bounded way.

## Choosing limits that mean something

**Not all requests cost the same.** A limit counting requests treats a one-row lookup and a
twelve-month aggregation as equal, so a consumer that hits the expensive path exclusively stays
inside a limit set for the cheap one. Weighting by cost — the model Cosmos DB's request units use —
is more work and considerably more honest.

**Decide what the limit is for before choosing the number.** Fairness between tenants, cost control
per customer, and protection from overload are three different goals that produce three different
numbers. A single limit trying to serve all three usually serves none.

**The fixed window's boundary spike is real.** A consumer that spends its whole allowance in the
last second of one window and again in the first second of the next has achieved twice the intended
rate. Where that matters, a sliding window or a token bucket is the fix, at the cost of more state
per consumer.

## Degradation has to be built, and kept working

The middle band is this pattern's most valuable behaviour and the easiest to let rot.

A degraded path serves stale data, samples, or a reduced result. Because it runs only under load, it
is rarely exercised in development, rarely covered by tests, and quietly breaks — and the breakage
surfaces during the incident it existed to soften. Where it has broken, the throttle has silently
become a two-band accept-or-reject, and nobody notices until the graphs are already bad.

**Test it as a first-class path**, and label it plainly in the response. A degraded answer that looks
identical to a full one teaches consumers that nothing is wrong, which removes their reason to slow
down and removes your reason to have degraded at all.

## What this repository's model leaves out

One process, so no distributed counter and none of its consequences. A hand-advanced clock, so the
window boundary is exact rather than raced. No HTTP, so no `429`, no `Retry-After`, no
`X-RateLimit-*` headers. Requests are counted rather than weighted by cost. And the fixed window is
implemented with its boundary flaw intact, described in the README but not fixed — because fixing it
here would hide the reason sliding windows exist.
