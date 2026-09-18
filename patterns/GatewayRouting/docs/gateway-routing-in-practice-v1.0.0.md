# Gateway Routing in practice

Supporting material for [the Gateway Routing pattern](../README.md).

## The routing table is configuration, and it is also a shared dependency

Putting routes in configuration rather than code means a new service does not require a gateway
deployment. That is the main operational benefit, and it comes with a consequence teams meet later:
**the routing table becomes an artefact several teams change, on the critical path of every
request.**

A wrong prefix does not fail loudly. It sends one resource's traffic to a service that will answer
something — usually a 404 from a service that does not own the resource — and the team whose
service is now receiving it gets paged about traffic they never asked for.

What keeps it manageable:

* **Review route changes as code**, because they are. A pull request against a routing file deserves
  the same scrutiny as one against a controller.
* **Own routes with the service they point at**, so the team that owns `/orders` owns its route,
  rather than a platform team owning everybody's.
* **Validate on load.** Overlapping prefixes, routes to unknown services and duplicate entries can
  all be detected at startup rather than by traffic.
* **Expose which service served a request**, via a response header. Without it, diagnosing routing
  means reading configuration and guessing.

## Refuse rather than default

A default backend — "anything unmatched goes to the monolith" — is common during migrations and is
genuinely useful there. It is also the single easiest way to hide a mistake.

With a default, a typo in a route, a service that failed to register, and a client calling a path
that no longer exists all produce the same thing: traffic quietly arriving somewhere plausible.
The failure surfaces days later as unexplained load or a confusing error from the wrong service.

The model in this folder refuses, and asserts that no service was reached. Where a default genuinely
is wanted — the Strangler Fig migration case, where unmigrated traffic must reach the legacy
system — make it **explicit and observable**: a named fallback route, counted separately, alerted on
when its share stops falling. That is a deliberate migration mechanism rather than an accident.

## Versioning is the pattern's most common use, and its most common trap

`/v1/orders` and `/v2/orders` routing to different services is the textbook example, and it works
well. The trap is what happens next: v1 stays for ever.

Nothing in the gateway pressures anybody to retire it. The route costs nothing visible, the old
service keeps running, and three years later there are five versions and no one knows which clients
use v2. The gateway made versioning cheap, which made *not deciding* cheap too.

The counter-measures are operational rather than technical: count traffic per version and publish
it; give versions a stated support window at introduction rather than at deprecation; and treat a
version with no traffic for a quarter as a candidate for removal, with the route removed first and
the service afterwards.

## The gateway is on every request's path

Two consequences that are easy to underweight.

**Availability.** The gateway's uptime is the system's uptime. Every service behind it can be
perfectly healthy and the product is down. Run several instances, spread them across failure
domains, and keep the gateway's own dependencies minimal — a gateway that calls a database to
resolve routes has taken that database's availability as its own.

**Latency.** Every request pays the hop. Usually small, and it compounds where a gateway routes to a
service that itself routes through another gateway — a shape that appears naturally as
organisations layer platform teams.

Neither argues against the pattern. Both argue for keeping the gateway thin: the more it does, the
more of the system's availability and latency budget it holds.

## Keep business logic out

The strongest predictor of a gateway becoming a problem is what it is allowed to know. Routing on
path, method, header and version is topology. Routing on a request body's contents, a customer tier
looked up from a database, or a feature flag evaluated per user is business logic — and business
logic in the gateway means every team's changes queue behind one component that no team owns.

The line is worth defending early, because each individual exception is reasonable. The question to
ask is not "can the gateway do this?" but "does this belong to a service, and is the gateway simply
the most convenient place to put it?"

## What this repository's model leaves out

There is no network: no latency, no connection pooling, no timeouts, no partial failure — a route
that resolves always succeeds. There is no health probing, so a route to a dead service still
resolves. There is no TLS, no load-balancing across instances, and no rate limiting. The routing
table is code rather than configuration, so a route change here is a deployment and the
shared-artefact problem above cannot arise. And the gateway is one object, so nothing shows the
system's behaviour when the gateway itself is unavailable — which is the risk that matters most.
