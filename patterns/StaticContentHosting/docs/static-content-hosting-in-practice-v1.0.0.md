---
doc_id: static-content-hosting-in-practice
title: Static Content Hosting in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Static Content Hosting in practice

Supporting material for [the Static Content Hosting pattern](../README.md).

## Versioned paths beat cache invalidation

There are two ways to make sure a browser stops using last week's stylesheet. One is to tell every
cache, everywhere, to forget it. The other is to never reuse the name.

Invalidation is genuinely hard: caches sit in the content delivery network, in corporate proxies,
in the browser, and in service workers, and only the first of those takes instructions. A purge
that reaches 99% of caches still leaves users on a broken page, and they are the users least able
to explain what they are seeing.

Versioned paths sidestep it. `site-a3f9c1.css` is immutable by construction, so it can be cached
for a year; a change publishes `site-77b210.css` and the markup points at the new name. Nothing
needs to be told anything. The trade is that the *markup* — the one thing that must change — has
to be short-cached or not cached at all, which is fine, because it is the one request per page
that the application handles anyway.

Content-hash names are better than build numbers: an unchanged file keeps its name across
deployments, so caches keep serving it.

## Deployment order is a real hazard

Two artefacts, one release. Get the order wrong and the site is broken for everyone during the
window, which is far worse than any of the failures the pattern prevents.

**Assets first, then markup.** The new assets sit unreferenced — harmless, because nothing points
at them — and the moment new markup ships, everything it references already exists.

**Keep the previous version live** until no cached markup points at it. Users hold pages for
longer than deploy windows: someone with a tab open from an hour ago is still requesting the old
bundle. Delete-on-deploy breaks exactly those users, invisibly, and only some of them.

That argues for cleaning up old assets on a schedule — weeks later, by age — rather than as part
of a release.

## Do not let a miss become origin load

The front in this folder answers unknown asset paths itself. It is a small detail with a specific
history: a front that forwards misses to the origin converts every broken link, stale reference
and hostile scan into application load, and the traffic that probes for `/assets/../config` is not
the traffic you want reaching the origin.

The same reasoning applies to the content delivery network layer: configure it to serve its own
404 for the asset prefix rather than treating a miss as a reason to consult the origin.

## Public by default is a security decision

Static hosting puts files where anyone with the URL can read them. That is the point for
stylesheets and the failure mode for everything else, and the confusion is common enough to have a
recognisable shape: a container is made public so the front end can show product images, and later
someone puts invoices in it.

The line is worth stating: **static content hosting is for content that would be safe on a
billboard.** Anything else — user uploads, generated documents, anything per-customer — needs the
application to authorise the specific request, and then to hand out a key scoped to one resource
with an expiry. That is the Valet Key pattern, and it is the correct neighbour to reach for rather
than a public container with unguessable names. An unguessable URL is not access control; it is a
password that is logged, shared and never rotated.

## Where the boundary sits between this and a cache

Both put bytes closer to the user, and they are answering different questions. Static hosting is
about **where content lives**: the file's home is storage, and the application never had a copy.
Caching is about **keeping a copy of something whose home is elsewhere**, with the staleness that
implies.

The distinction matters in practice because it decides who owns correctness. A static asset is
correct by construction — it is the file. A cached response is correct only until the thing behind
it changes, which is why Cache-Aside spends most of its design effort on invalidation and this
pattern spends almost none.

## What this repository's model leaves out

Nothing is remote: no network, no content delivery network, no edge, and so no cache lifetimes, no
invalidation, no geography and no bandwidth. Assets and application are deployed together by
construction, so the ordering hazard above cannot arise — which is unfortunate, because that is
where most of the real operational risk lives. There are no content types, no compression and no
cross-origin headers. And there is no access control, so the public-by-default hazard is described
here rather than demonstrated.
