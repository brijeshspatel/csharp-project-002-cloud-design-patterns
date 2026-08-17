---
doc_id: ambassador-in-practice
title: Ambassador in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-18
updated: 2026-08-18
---

# Ambassador in practice

Supporting material for [the Ambassador pattern](../README.md).

## The pattern is about where policy lives, not what the policy is

Retry, timeout and circuit breaking are covered by their own patterns and they do not change here.
What changes is the **address** of that logic: outside the application, in a component the
application does not configure and cannot see.

That relocation buys three things a library cannot:

* **Language independence.** Four services in four languages get identical behaviour, which no
  shared library can deliver.
* **Operational tuning.** Changing three retries to five is a configuration change applied to
  running instances, not four pull requests and four releases.
* **Legacy reach.** An application nobody wants to modify gets modern resilience by being deployed
  next to something that has it.

The cost is equally specific: the policy can no longer depend on anything the application knows.
An ambassador cannot decide to skip retrying because *this* order has already been paid — it has no
concept of an order. Policy that needs domain state stays in the application, and trying to teach
the ambassador about domains is how a helper becomes a second implementation of the service.

## Retries hide latency, and hidden latency is confusing

The application makes one call. If that call takes four seconds, the application's own telemetry
records a four-second call to a local helper — with no explanation anywhere in its logs.

This is the most common operational complaint about the pattern, and it has a specific fix: **the
ambassador must emit its own telemetry**, and it must be correlatable with the application's. At
minimum, attempts per call, outcome per attempt, and total time including backoff, tagged with the
same trace id the application used.

Without that, an engineer looking at a slow request sees a slow local call and no cause. With it,
the ambassador's telemetry is strictly better than what the application would have produced, because
it is consistent across every service.

The version in this folder keeps `AttemptsMade`, `AttemptsAbsorbed`, `TotalBackoff` and
`LastFailure` for exactly this reason — they are the minimum an operator needs to explain a slow
call the application cannot explain.

## Bounded retry, and why the bound belongs to the ambassador

An unbounded retry loop is a slow outage. The caller's thread is held, its connection pool drains,
and a backend that is merely slow becomes a whole-service failure — the failure the retry was meant
to prevent, arriving by a different route.

Putting the bound in the ambassador rather than the application has a useful property: it can be
tightened globally when a backend starts misbehaving, without any application change. That is the
same benefit as tuning the retry count, applied to the safety limit.

Two refinements worth having from the start:

* **Jitter**, so many instances retrying do not synchronise into a thundering herd against a
  service that is already struggling.
* **A circuit breaker**, so a backend that is properly down stops being attempted at all. Without
  one, every request pays the full attempt budget before failing, which is the worst possible
  latency profile during an outage.

## The ambassador is on the critical path of everything

It is easy to think of the helper as auxiliary. It is not: every outbound call goes through it, so
its availability is the application's availability for anything remote.

That has practical consequences. The ambassador needs its own health checking and its own resource
limits, so a memory leak in it does not take the application with it. Its upgrades are fleet-wide
events, because it is deployed alongside every instance — a bad ambassador version is a bad
deployment everywhere at once, which argues for staged rollout with the same care an application
release gets. And its failure mode should be considered explicitly: an ambassador that cannot start
should usually stop the application from starting, because an application that silently loses its
resilience policy is worse than one that fails to deploy.

## Ambassador and Sidecar, stated plainly

An ambassador is nearly always deployed as a sidecar, and the two patterns are separate for good
reason.

**Sidecar** is a deployment shape: a helper process sharing a host and a lifecycle with an
application, isolated from it. It says nothing about what the helper does — collecting metrics,
supplying configuration, rotating certificates and proxying calls are all sidecars.

**Ambassador** is a job: proxying outbound network calls with policy attached. It is usually a
sidecar and does not have to be — a shared egress proxy serving many applications is an ambassador
without being a sidecar, at the cost of the locality that makes the sidecar version fast.

The distinction matters when choosing. "Should this be a sidecar?" asks about co-location, lifecycle
and resource overhead. "Should this be an ambassador?" asks whether outbound-call policy belongs
outside the application. They are usually answered together and they are not the same question.

## Service meshes are this pattern, industrialised

Almost nobody writes an ambassador by hand any more, and that is the right outcome. A service mesh
gives every pod a sidecar proxy that handles retries, timeouts, circuit breaking, mutual TLS,
traffic shifting and telemetry, configured declaratively and centrally.

The pattern remains worth understanding for two reasons. It explains what a mesh is doing and why
requests are slower than the application thinks. And there are cases a mesh does not cover — a
specific third-party API needing bespoke handling, an environment with no mesh, or a policy that
must be applied outside the cluster boundary — where a purpose-built ambassador is still the right
answer.

## What this repository's model leaves out

There is no process boundary: the ambassador is a class in the application's own process, so nothing
shows the separate lifecycle, the extra memory, or the local hop a real sidecar costs. There is no
network, so timeouts cannot occur and a failure is a clean `null` rather than a hang — which is the
case timeouts exist for. There is no jitter, no circuit breaker and no connection pooling. There is
no mutual TLS or certificate rotation, which in practice is a large part of why the pattern is
adopted. And the backoff is recorded rather than waited, so the latency the application would
actually experience is a number rather than an experience.
