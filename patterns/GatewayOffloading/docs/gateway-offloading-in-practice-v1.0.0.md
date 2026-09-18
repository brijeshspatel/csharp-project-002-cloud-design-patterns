# Gateway Offloading in practice

Supporting material for [the Gateway Offloading pattern](../README.md).

## Where the line falls: authentication yes, authorisation no

The hardest question in this pattern is what counts as cross-cutting, and there is a workable rule.

**Authentication** — is this token well-formed, correctly signed, unexpired, from the right issuer? —
is identical for every service. It needs no domain knowledge, it is easy to get subtly wrong, and
one implementation reviewed carefully beats nine written hurriedly. Offload it.

**Authorisation** — may *this user* cancel *this order*, given that it shipped yesterday and they are
a delegated administrator of the account that placed it? — needs to know what an order is. Push that
to the edge and the gateway acquires domain knowledge, which is how gateways become applications
that every team must queue behind.

The boundary blurs in one useful place: coarse authorisation, such as "this token has the
`orders.read` scope", is checkable at the edge and often belongs there. The test is whether the
decision needs to load domain state. If it does, it is the service's.

## Offloaded protection is advisory unless bypass is impossible

The model in this folder has a proxy that validates tokens and services that contain no token code
at all. That is the point — and it means **any caller who reaches a service directly is
unauthenticated and gets served.**

In production that is a design decision, not an oversight, and it must be made explicitly:

* **Network isolation.** Private endpoints, VNet integration or network policy, so nothing but the
  gateway can route to the services.
* **Mutual TLS**, so a service accepts connections only from the gateway's certificate.
* **A shared secret header**, which is the weakest of the three and better than nothing.
* **Accept it**, where the services are in a trusted network and the threat model says internal
  callers are fine.

What does not work is assuming it. A service that is reachable from anywhere and contains no
validation is protected by a routing convention, and routing conventions change during incidents.

**Where making the back end unreachable is the actual goal** — rather than a supporting measure —
the pattern is Gatekeeper, and the difference is worth keeping clear: offloading removes duplicated
code and happens to refuse some traffic; a gatekeeper exists so that a compromised edge yields
nothing.

## Pass the edge's findings on

A gateway that validates a token and then forwards the original request has done half a job: the
service knows the caller is authenticated, because it received the request at all, but not *who*
they are. Services then re-parse the token, which puts back the code that was just removed.

The standard answer is for the edge to strip the incoming credential and inject a trusted
representation of the caller — user id, tenant, scopes — as headers. Two conditions make that safe:
the service must trust only the edge, which is the bypass question above, and the edge must strip
any inbound headers of the same names, or a client can simply assert its own identity.

That second one is easy to miss and severe: a gateway that forwards a client-supplied
`X-User-Id` has given every caller the ability to be anybody.

## The blast radius runs both ways

A change at the edge changes behaviour for every service at once. That is the benefit — a security
header added everywhere in one deployment — and the risk, in the same sentence.

Practical mitigations: treat edge configuration as code with review and staged rollout; roll out to
a subset of routes before all of them; keep a rehearsed rollback, since the failure mode is
everything at once; and be specific about what "everything" includes, because a rate limit tuned for
a public API can strangle an internal one that happens to sit behind the same gateway.

## Refused traffic is invisible to the services

Requests rejected at the edge never appear in any service's logs, metrics or traces. That is the
intended saving and it creates a monitoring gap: a misconfigured token check that rejects everything
looks, from every service's dashboard, exactly like a quiet afternoon.

So the edge needs its own observability, and specifically its **rejection rate by reason**, alerted
on. A jump in "token expired" is a client with a clock problem or an expiry that just shortened; a
jump in "no route" is a deployment that half-happened. Neither is visible anywhere else.

## Offloading is where gateways go to become monoliths

Every individual thing put at the edge is defensible. Compression, obviously. TLS, obviously. Then
header rewriting, then a small redirect rule, then a response transformation for one legacy client,
then a lookup to decide which of two backends should serve a customer tier.

At some point the gateway contains business logic that no team owns and everyone depends on, and
changing it requires understanding all of it. The pattern has not failed; the boundary was never
defended.

The question worth asking each time is not "can the gateway do this?" — it usually can — but "would
this still make sense if there were only one service behind it?" Cross-cutting work stops being
cross-cutting when it applies to one route.

## What this repository's model leaves out

There is no TLS, no real compression and no real token — validation is a string comparison. There is
no network, so nothing shows that the services are reachable directly and therefore unprotected when
bypassed, which is the pattern's main operational risk and the whole of the second section above.
The verified identity is not passed downstream, so the header-injection hazard cannot be
demonstrated. There is no rate limiting, no WAF, no caching. And the proxy is a single object with
no availability story, where in production it is the component whose failure takes everything with
it.
