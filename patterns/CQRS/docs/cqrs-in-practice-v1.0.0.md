# CQRS in practice

Supporting material for [the CQRS pattern](../README.md).

## CQRS is not Event Sourcing, and the confusion is expensive

The two are named together so often that many teams adopt both while intending one. They are
independent decisions:

* **CQRS** says reads and writes go through different models. It says nothing about how the write
  model stores anything. A mutable table is a perfectly ordinary CQRS write model — the one in
  this folder.
* **Event Sourcing** says state is stored as the sequence of events that produced it. It says
  nothing about how state is queried; a system can event-source with no read model at all.

All four combinations are real and sane. CQRS without Event Sourcing is the common, boring, mostly
correct choice. Event Sourcing without CQRS is workable where replay is fast enough to serve
queries. Both together is powerful and roughly twice the machinery — with the events themselves as
a natural projection feed, which is why the pairing is so often assumed.

The cost of confusing them is adopting the harder one by accident. A team wanting dashboards that
do not contend with checkout needs CQRS; if it hears "CQRS/ES" as one word it may also inherit
event versioning, replay, snapshots and an unfamiliar debugging story it never needed. This
repository implements the two separately for exactly that reason.

## Eventual consistency is a user-interface problem

The read model is stale between projection runs, always. Where that lands is in the interface, and
"the projection is fast" is not a design.

Three approaches, in rising order of honesty:

* **Hope.** The projection is fast and the user rarely notices. It works until the day the
  projection lags, when the system silently shows the wrong thing and confidence goes with it.
* **Optimistic local echo.** The client shows its own write immediately while the read model
  catches up. Best where the user's own action is what they are looking at, and no help for
  anything else on the page.
* **Show the age.** Surface when the figure was last built. It costs one field and converts "the
  total is wrong" into "the total is from before that order" — a shorter and less damaging
  conversation.

The command side can also help: return enough from the command for the client to render the
outcome without waiting for the projection.

## The projection is a component, not a step

The most damaging CQRS failure is not a slow projection; it is one that has stopped. The system
keeps serving, quickly and confidently, an answer from before the projection died — and nothing in
the read path can tell.

That makes the projection a first-class component with the obligations of one:

* **Lag as a metric**, alerted on. Not "is the process running?" — running and stuck are
  indistinguishable from outside.
* **A rebuild procedure that is routine.** A full rebuild is idempotent and self-correcting; a
  team that has rebuilt in anger before will do it calmly when it matters.
* **Idempotency**, so a replay or duplicate delivery is harmless. Full rebuild gives it for free;
  incremental projections must earn it.
* **Ordering, or independence from it.** An incremental projection that applies events out of
  order drifts quietly. Full rebuild sidesteps this too, at linear cost.

Full rebuild versus incremental is the same trade a materialized view makes, and for the same
reasons: rebuild is self-correcting and linear; incremental is cheap and accumulates drift. The
common compromise is both — incremental for freshness, periodic full rebuild to reset drift.

## Two models means two schemas that must evolve

A write-model change usually implies a projection change: a new field is not in the read model
until the projection puts it there, and a removed field lingers until it is removed there too.
Deploy order matters. The safe sequence is the usual expand-and-contract: extend the read model
and projection to handle both shapes, deploy, migrate or rebuild, then remove the old shape.

Rebuild-friendliness pays for itself here. Where the read model can be rebuilt at will, a schema
change is: build the new shape alongside, cut readers over, drop the old. Where it cannot, the
same change is a migration with downtime.

## Where the boundary actually erodes

The refusal in this folder's write model is a small joke with a serious point. In practice the
boundary is lost one reasonable decision at a time: a validation needs a customer's total, so the
write model reads it; an admin screen needs a live figure, so it queries the write model directly;
a report needs a join the read model lacks, so it goes to the source.

Each is defensible; together they leave two models, twice the code and none of the benefit — the
write model is back to serving reads, and the read model is an expensive cache.

Two habits hold the line. Make the boundary mechanical, so crossing it requires deliberate work
rather than a convenient call. And when a query genuinely needs data the read model lacks, extend
the projection rather than routing around it — that pressure is the read model telling you which
shape it is missing.

## What this repository's model leaves out

The projection is called by hand, synchronously, in the same process, so it cannot lag, fail,
duplicate, reorder or stop — which is where the operational cost of CQRS actually lives. There is
no command bus, no queue and no retry. There is no lag metric and no staleness surfaced to a
reader. And the two models never version independently, so the schema-evolution problem above
never arises.
