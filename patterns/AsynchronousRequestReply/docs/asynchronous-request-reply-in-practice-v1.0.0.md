---
doc_id: asynchronous-request-reply-in-practice
title: Asynchronous Request-Reply in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Asynchronous Request-Reply in practice

Supporting material for [the Asynchronous Request-Reply pattern](../README.md).

## Not found is the most valuable status

It looks like a detail and it is the difference between a client that recovers and one that hangs.

A client polls a job identifier it got from somewhere: a bookmark, a saved link, a retry after a
crash, a typo. If the status endpoint answers **Pending**, the client concludes the work is queued
and keeps polling — for ever, because nothing will ever move a job that does not exist.

Answering **NotFound** lets the client do something sensible: report an error, offer to resubmit,
stop.

The mistake usually arrives via a helpful default. A store lookup returns `null`, and somewhere a
`??` turns that into a default state whose status happens to be the enum's zero value — which, if
`Pending` is declared first, is `Pending`. The bug is invisible in every test that submits a job
first.

A closely related question: **what does a poll for an *expired* job return?** Once job records are
pruned, an old identifier becomes indistinguishable from a wrong one, and a caller that was slow to
poll gets `NotFound` for work that succeeded. Retention has to exceed the longest realistic gap
between completion and the caller looking.

## Polling costs, and the cost is proportional to the wrong thing

Polling load scales with *callers*, not with work. Ten thousand clients polling every second for a
job that takes two minutes is 1.2 million status requests for one report.

Reducing it, roughly in order of effectiveness:

* **`Retry-After`, and clients that honour it.** Tell the client how often to ask. Most polling
  storms are clients guessing.
* **Exponential back-off** on the client, capped. Poll quickly at first, then less often.
* **Cache status responses briefly.** A one-second cache on a hot job removes most of the load and
  costs a second of staleness that nobody notices.
* **Push instead.** SignalR, WebSockets or a webhook removes polling entirely for clients that can
  receive it — with polling retained as the fallback, because some clients cannot.

## Idempotent submission

The submit call can be retried, by a client that timed out or by a gateway that saw a slow response.
Without protection, each retry starts another job, and a user who pressed the button twice gets two
reports, two charges, or two provisioned machines.

The fix is a **client-supplied request identifier** — an idempotency key — stored alongside the job.
A submit carrying a key already seen returns the **existing** job's acceptance rather than creating
a new one, which is Idempotent Consumer applied to the front door.

It needs the same care as any de-duplication: record after the job is created, key on something
stable, and use a unique constraint rather than a check-then-insert.

## Keep the result out of the status response

It is tempting to return the finished report inside the status payload. It works until the report is
large, at which point every poll transfers megabytes — including the polls that arrive after the
client already has it.

Return a **reference**: a URL, ideally a scoped and expiring one. The status response stays small,
the result is fetched exactly once, and access to it can be controlled separately. This is Claim
Check's reasoning applied to a reply, and Valet Key's applied to the fetch.

## The status store is the real dependency

Everything in this pattern hangs on state that outlives a single request, and it is the piece most
often under-designed.

It must be **durable**, or a restart loses every in-flight job and no caller can learn what happened.
It must be **shared across instances**, or a caller polling a different instance from the one that
accepted the work gets `NotFound` — the same bug as above, arrived at through infrastructure. And it
must be **fast**, because it is read far more often than it is written.

Its availability is now the availability of both submission and polling, which makes it a more
significant dependency than it first appears.

## What this repository's model leaves out

There is no HTTP, so `202 Accepted`, `Location`, `Retry-After` and status codes are absent — and they
are the pattern's actual interface. There is no queue between the gateway and the worker, so the
back end is not genuinely decoupled; the worker is driven explicitly so that transitions can be
observed rather than raced. Nothing is durable and nothing is shared across instances. There is no
polling client, no back-off and no caching. Submission is not idempotent. And there is no expiry, so
the expired-versus-unknown question above cannot arise.
