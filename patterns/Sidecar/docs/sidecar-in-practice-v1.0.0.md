---
doc_id: sidecar-in-practice
title: Sidecar in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-18
updated: 2026-08-18
---

# Sidecar in practice

Supporting material for [the Sidecar pattern](../README.md).

## Failure isolation is not resource isolation

The sidecar's process can crash without the application's process crashing. That is real, and it is
narrower than it sounds.

A sidecar with no memory limit that leaks will exhaust the host, and the kernel will kill whichever
process it decides to kill — quite possibly the application. Two processes sharing a machine are
isolated from each other's *bugs* and not from each other's *appetite*.

So the isolation the pattern promises requires asking for it:

* **Memory and CPU limits per container**, set deliberately rather than left unbounded. A sidecar
  that dies at its limit is behaving correctly; one that grows until the host suffers is not
  isolated at all.
* **Restart policy.** A crash-looping sidecar consuming CPU on every restart is its own problem;
  back-off matters.
* **Watch the aggregate.** Three sidecars at 100 MB each is 300 MB per instance, and at a thousand
  instances that is 300 GB of memory doing nothing the product can see.

The last point is the one that surprises people at scale. The overhead is per instance, and it
multiplies exactly as the application scales out.

## Start order is a requirement, and it has two opposite answers

Some sidecars must be running before the application takes traffic. A proxy terminating mutual TLS
is useless if the application starts accepting connections first — early requests fail, or worse,
travel unencrypted.

Others must never block the application. A metrics exporter that cannot reach its collector should
not stop a service from starting; that is the failure this folder's model exists to demonstrate.

The two requirements are opposite and both are legitimate, so **which one applies must be decided
per sidecar and stated**. In Kubernetes this is the difference between an init container, a sidecar
with a readiness gate, and an ordinary container — and choosing wrongly produces either an
application that starts insecure or one that will not start because its telemetry is unavailable.

The same applies to shutdown, and it is more often got wrong: an application draining connections
needs its proxy alive until the last one finishes. A sidecar terminated first turns a graceful
shutdown into dropped requests.

## A silently missing sidecar is an outage nobody sees

The application is serving. Every dashboard it feeds is empty — but empty dashboards look like a
quiet period, not like a failure.

That is why this folder's model surfaces `SidecarStatus` to the host rather than merely isolating
the failure. Isolation without visibility converts a loud failure into a silent one, which is not an
improvement.

What that means in practice: the application's own health endpoint should report sidecar health as a
separate, non-fatal field; the platform should alert on sidecars that are down even though the pod is
"ready"; and the absence of telemetry from an instance should itself be alertable, because that is
the signal that arrives when the exporter cannot report its own failure.

## The interface should be narrow, or it is a library again

A sidecar communicates over something — a local port, a Unix socket, a shared volume, a signal.
Whatever it is should be small and stable.

The pull is towards richness: the application starts sending structured context, then configuration,
then callbacks. At that point the two are coupled as tightly as a library would have coupled them,
except now there is also a process boundary, a serialisation format and two deployment artefacts to
keep in step.

The test worth applying: could the sidecar be replaced with a different implementation, in a
different language, that honours the same narrow contract? If yes, the boundary is doing its job. If
the answer requires knowing the application's internals, the sidecar is a library that has been
deployed separately for no benefit.

## Sidecars accumulate

One is obviously worth it. The metrics exporter, then the log shipper, then the service-mesh proxy,
then the secret-rotation agent, then the policy agent — each addition is individually justified, and
none of them is ever removed.

The result is a pod where the application is a minority of the memory, startup takes thirty seconds
because five things must become ready, and an incident involves working out which of six containers
is responsible.

Two habits keep it in hand. **Count them and publish the count**, alongside the resource overhead
per instance — the number itself creates pressure. And **ask what a new sidecar replaces**: often the
functionality already exists in another sidecar, or in the platform, and the honest comparison is
against consolidation rather than against nothing.

## Sidecar and Ambassador, and why both exist

Ambassador is one job this shape does: proxying outbound calls with retry, timeout and circuit
breaking attached. It is nearly always deployed as a sidecar.

They are separate patterns because the questions they answer are different. *Should this be a
sidecar?* asks about co-location, lifecycle coupling and per-instance overhead. *Should this be an
ambassador?* asks whether outbound-call policy belongs outside the application at all. A sidecar
may collect metrics and never make a call; an ambassador may be a shared egress proxy and not be a
sidecar.

In practice they are usually answered together, and a service mesh is both at once — which is why
understanding them separately is what makes a mesh's behaviour predictable rather than magical.

## What this repository's model leaves out

There is no process boundary: the "sidecar" is a class in the application's own process, so the
isolation that is the pattern's main benefit is asserted rather than demonstrated, and a real
resource leak here would take the application down. There is no container, no shared volume and no
network namespace. There is no start-order dependency, no resource limit and no independent upgrade
path. There is no restart behaviour. And there is one sidecar rather than five, so the accumulation
problem cannot appear.
