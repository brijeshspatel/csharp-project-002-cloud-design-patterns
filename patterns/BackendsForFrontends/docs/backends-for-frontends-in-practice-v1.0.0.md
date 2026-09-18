# Backends for Frontends in practice

Supporting material for [the Backends for Frontends pattern](../README.md).

## The pattern is organisational before it is technical

The response-shape argument is real and it is the smaller half. A single shared backend can be made
to serve two shapes — content negotiation, sparse fieldsets, a `view` parameter — and teams do this
successfully all the time.

What cannot be solved that way is **who decides**. A shared backend is owned by someone, and every
change either client needs goes through that owner's backlog. The mobile team wants a shorter name
for a new list layout; that is a change to a component the desktop team depends on, so it needs
review, coordination and a release nobody else wanted.

Backends for frontends is the observation that a component serving two masters serves the slower
one. Give each client team its own backend and the truncation length stops being a negotiation.

The corollary is a real precondition: **if the client team will not own the backend, the pattern
does not apply.** A platform team owning three "backends for frontends" has three shared endpoints
and three times the operational surface, with the same queue in front of all of them.

## Group by need, not by device

"Mobile and desktop" is the textbook split and it is often the wrong axis. What matters is what an
interface requires — payload budget, navigation shape, freshness, authentication model — and those
do not follow device categories reliably.

A responsive web app and a phone app may want the same thing, in which case they share a backend. A
public partner API and an internal admin console are both browsers and want almost nothing in
common. A smart TV differs from a phone as much as from a desktop.

Two questions settle it. Would a change one client needs ever be unwelcome to the other? And is
there a team that would own it? Two yeses mean two backends; anything else means one.

## Keep the backends thin, or you have two domains

The failure mode is gradual and recognisable. A backend shapes a response; then it applies a small
rule about which products to hide; then it computes a discount because that was where the data
was; and eventually the mobile backend and the desktop backend disagree about the price of
something.

The rule that holds: **a backend for a frontend composes and shapes. It does not decide.** Anything
that is true about the business regardless of which screen is asking belongs in the domain.

The diagnostic question when adding logic is whether the *other* backend would need the same rule.
If yes, it is domain logic sitting in the wrong place, and putting it in both is how the two answers
appear.

## The duplication is real, and mostly worth accepting

Both backends fetch the product and build a response. That is duplicated work and it looks wasteful.

Some of it should be removed — a shared client library for calling the domain, shared authentication
handling, shared serialisation. Some of it should not: the shaping code is precisely the part that
differs per client, and factoring it into a common component with parameters is how a shared
backend gets rebuilt inside two projects.

What should never be the answer is one backend calling the other. It reintroduces the coupling, adds
a hop, and makes the mobile backend's availability depend on the desktop team's deployment.

The honest position is that this pattern trades some duplication for independence, and the trade is
only worth making when the independence is worth something — which is to say, when separate teams
own the clients.

## Cross-cutting concerns still belong in front

Authentication, TLS, rate limiting and WAF rules should not be implemented in each backend. That is
Gateway Offloading, and it composes with this pattern rather than competing: a single edge in front,
then a fan-out to per-client backends, then a shared domain.

Getting that layering right is what keeps the backend count affordable. Six backends each
implementing their own token validation is six places to patch; six backends behind one edge that
validates is one.

## Old clients are the pattern's quiet advantage

A mobile app version from eighteen months ago is still installed on real phones, and it expects the
response shape it shipped against. With a shared backend, that constraint holds every client
hostage: nothing can change in a way that old app cannot parse.

With a backend per frontend, the old shape can be kept alive in the mobile backend — or a second
version of it deployed — without the desktop site ever knowing. The client's long tail becomes the
client team's problem to manage, which is where the knowledge to manage it lives.

It is worth planning that explicitly rather than discovering it: decide how long a client version is
supported, measure traffic by version, and retire shapes on a schedule. A backend accumulating
response shapes for every version it has ever served has the same drift problem as the shared
endpoint, just contained to one team.

## What this repository's model leaves out

The backends are classes in one process: no separate deployment, no independent scaling, no failure
isolation — which is a substantial part of the pattern's value and the whole of the ownership
argument in practice. There is no network, so the over-fetching the mobile shape avoids is described
rather than measured in bytes. There is no authentication, so nothing shows the cross-cutting layer
that should sit in front of both. There is no client versioning. And there are two clients rather
than six, so the proliferation problem never bites.
