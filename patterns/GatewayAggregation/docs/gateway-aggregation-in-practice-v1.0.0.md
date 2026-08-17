---
doc_id: gateway-aggregation-in-practice
title: Gateway Aggregation in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Gateway Aggregation in practice

Supporting material for [the Gateway Aggregation pattern](../README.md).

## Sequential aggregation gives away the benefit

The model in this folder calls its four backends one after another, which is fine when a call costs
nothing. Over a real network it is the mistake that quietly undoes the pattern.

Four sequential calls of 80 ms each produce a 320 ms response. Four concurrent calls produce an
85 ms one. The client saved three round trips of *its own* latency either way — but a gateway that
serialises has spent the saving on itself and handed back a slower screen than the client could have
assembled by issuing four parallel requests.

So: **fan out, then join**. Every backend call starts before any of them is awaited, and the
response time becomes the slowest call rather than the sum. This is the single highest-value
implementation detail in the pattern, and it is invisible in any in-process model.

The exception is a genuine dependency — here, the profile call, whose result decides whether the
others are worth making at all. Those stay sequential by necessity, and there should be as few of
them as possible.

## Timeouts, and the slowest backend

Once calls are concurrent, the response is as slow as the slowest one. Without a timeout, a single
hung backend holds every home screen in the system until something upstream gives up.

The rule that works: **each backend call gets a timeout shorter than the client's**, and a backend
that exceeds it is treated exactly as a failed one — the panel is empty and named in `Unavailable`.
That converts an outage into a degraded screen, which is the pattern's whole disposition.

Pair it with a circuit breaker per backend. A service that has failed its last twenty calls should
not be called on the twenty-first; the breaker turns a timeout on every request into an immediate
empty panel, which is faster and kinder to the failing service.

## Required and optional must be decided, not defaulted

Every backend is either required — no answer, no response — or optional, where the response degrades.
The model here makes profile required and offers optional, and the difference is visible in the
tests.

Defaulting everything to required is the common accident, and its effect is arithmetic: a screen
needing four services each at 99.9% availability is available 99.6% of the time, which is four times
the downtime of any one of them. Making three of the four optional restores most of it.

Defaulting everything to optional is the opposite mistake — a screen that renders with no name, no
orders and no basket is not a degraded screen but a broken one presented as working.

The decision belongs to whoever owns the screen, and it should be visible in the aggregation code
rather than implied by which exceptions happen to be caught.

## Say what failed

A gateway that returns an empty offers list when the offers service is down has told the client
something false: that this customer has no offers. The client caches it, shows "no offers available
for you", and nobody knows why.

Naming the unavailable backends in the response costs one field and changes what every consumer can
do: show a placeholder rather than an absence, retry that panel alone, avoid caching the partial
result, and report a meaningful error rate. It also makes the gateway's own monitoring honest —
partial responses are countable rather than looking like successful ones.

## The gateway learns the screens, and that is a real coupling

Routing knows paths. Aggregation knows that *this screen needs those four services* — product
knowledge, and it lives in a component the client team does not own.

Consequences worth anticipating: a new panel on the home screen is a gateway change; a client team
waiting on a platform team to ship it; and, over time, an aggregation layer full of endpoints shaped
by particular screens, each with its own flags.

Two ways out, both legitimate. Give each client its own backend, owned by that client's team — the
Backends for Frontends pattern, which exists precisely because this coupling becomes intolerable at
scale. Or let clients specify the shape they want, which is what GraphQL is; that removes the
per-screen endpoints and adds a query surface that must itself be governed.

The failure mode to avoid is a single aggregating endpoint returning the union of everything any
client needs, with each client ignoring most of it. That is the shape that costs bandwidth and
pleases nobody.

## Caching by panel, not by response

Different panels tolerate very different staleness. Offers change daily; a basket changes by the
second. Caching the assembled response uses the shortest tolerance for everything, so the basket
forces a cache lifetime of seconds and the offers are re-fetched needlessly.

Caching each backend's result separately, with its own lifetime, lets the gateway assemble a fresh
basket alongside offers it fetched an hour ago — and that is where a large part of the load
reduction comes from in practice. It is Cache-Aside applied per backend rather than to the screen.

## What this repository's model leaves out

Calls are sequential method calls with no network: no latency, so the sum-versus-maximum problem
cannot appear; no timeouts; no concurrency at all. There is no circuit breaker, so a permanently
failing backend is called on every request. There is no caching, per panel or otherwise. Payload
size is not modelled. And the injected failure is a clean exception thrown immediately, where a real
backend more often hangs — which is the case timeouts exist for and the one this model cannot
produce.
