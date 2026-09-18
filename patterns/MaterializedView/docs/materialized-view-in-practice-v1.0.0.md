# Materialized View in practice

Supporting material for [the Materialized View pattern](../README.md).

## Derived and disposable, or you have built a second database

The single most useful property of a materialised view is that **losing it costs a rebuild, never
data**. That property is what makes it safe to drop, recreate, change the shape of, or run several
versions of at once.

It is also easy to lose. The sequence is always the same: the view has a field the source does not,
because it was convenient. Then something writes to that field directly. Now the view holds
information that exists nowhere else, and it is no longer derived — it is a second source of truth,
with no backup story, no schema discipline, and a rebuild process that would silently destroy it.

**The guard is a rule, not a mechanism**: nothing writes to the view except the rebuild. Where that
is hard to enforce, it is worth making the view physically read-only to everything except the
rebuilding process.

## Full rebuild versus incremental refresh

A full rebuild reads the whole source and replaces the view. Incremental refresh applies only what
changed.

Full rebuild is **self-correcting**: whatever state the view was in, after a rebuild it is right. A
bug in the rebuild produces a wrong view that the next fixed rebuild repairs. It is also linear in
source size, so at some scale it stops being viable.

Incremental refresh is much cheaper and has a correctness burden that grows quietly. Every change
must be applied exactly once — miss one and the view drifts; apply one twice and it drifts the
other way. Drift is cumulative, silent, and typically discovered when someone notices a total that
does not match.

The practical compromise is **both**: incremental refresh for freshness, plus a periodic full
rebuild that resets any accumulated drift. The full rebuild is the thing that makes the incremental
path safe to trust.

## Rebuilds are not instantaneous, and queries happen during them

The model in this folder rebuilds in a single synchronous step. A real rebuild over millions of rows
takes minutes, and the question the model cannot ask is: **what do queries see while it runs?**

Three answers, and one is usually wrong:

* **Build in place.** Queries see a half-built view — some regions updated, some not. Cheap and
  almost always wrong, because a partially updated aggregate is not a state the data was ever in.
* **Build alongside, then swap.** Build into a second structure and switch atomically. Queries see
  the old view, then the new one, never a mixture. Costs double the storage during the build.
* **Version the view.** Readers pin a version; old versions are collected once nobody is reading
  them. Most complex, and it makes "which version am I looking at?" answerable.

Build-and-swap is the usual right answer, and it is what the implementation here does implicitly by
building into a new dictionary before assigning it.

## Event-driven rebuilds narrow the window and add coupling

A scheduled rebuild leaves a view stale for up to the interval. Rebuilding when the source changes —
subscribing to its change feed or its events — narrows that to the propagation delay.

The cost is a dependency: the view now needs the source to publish changes reliably, and a missed
event means silent drift until the next full rebuild. It also makes rebuild frequency a function of
write rate, which is exactly wrong when a bulk import lands.

A common pattern is to debounce: react to events, but rebuild no more often than every N seconds,
and always do a scheduled full rebuild regardless.

## Say how old it is

A view answers instantly and gives no indication that the answer is from eleven minutes ago. People
trust numbers that appear on dashboards.

**Surface the build time next to the figure.** It costs one field and it converts "the total is
wrong" into "the total is from before that order" — which is a different conversation, and a much
shorter one.

## What this repository's model leaves out

The rebuild is synchronous and instantaneous, so nothing exercises queries during a long rebuild —
though it does build into a new structure before swapping, which is the right shape. There is no
incremental refresh, so the drift problem cannot arise. There is no scheduler and no event trigger.
The view lives in the same process as its source, so the multi-store case that motivates the pattern
most strongly is absent. And no staleness indicator is surfaced, which the section above argues
should be.
