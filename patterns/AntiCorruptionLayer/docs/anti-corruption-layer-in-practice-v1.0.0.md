---
doc_id: anti-corruption-layer-in-practice
title: Anti-Corruption Layer in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-18
updated: 2026-08-18
---

# Anti-Corruption Layer in practice

Supporting material for [the Anti-Corruption Layer pattern](../README.md).

## Leakage happens through the cracks, not the front door

Everyone gets the main translation right. The legacy model escapes through the places nobody
thought of as part of the boundary:

* **Errors.** The legacy system returns `ERR-0042: INVALID STATCD`, and the modern API passes it
  straight to a client. The client now has a dependency on a mainframe error catalogue.
* **Logs and traces.** A modern service logging `fetching ORDNO=0000001042` has taught everyone who
  reads logs — including the next engineer — to think in the legacy model.
* **Identifiers.** The padded order number is convenient as a correlation id, so it ends up in a
  message header, then in an analytics table, then in a report somebody's job depends on.
* **Nulls and sentinels.** The mainframe uses `0001-01-01` for "no date" and `9999` for "unknown
  customer". Translate the shape but pass the sentinel through, and every modern component learns to
  check for it.
* **Ordering and pagination.** "Records come back in ORDNO order" is a legacy behaviour that modern
  code will quietly rely on.

The discipline that catches these: **anything crossing the boundary is translated, including things
that are not data.** Errors become modern errors with modern codes. Sentinels become nulls or
absent values. Identifiers used outside the boundary are modern ones.

## Concessions are the artefact worth keeping

Two models that were designed independently will disagree, and the disagreements are where the
value of the layer actually lives.

The mainframe distinguishes "accepted" from "accepted but held in tonight's batch". The modern
model has no concept of a batch schedule and should never acquire one — so the distinction is
dropped. That is the right call *and* it is a loss, and the difference between a good layer and a
bad one is whether anybody can find out.

Recording concessions explicitly gives three things: a reviewable list of what the integration
cannot express, which is exactly what a domain expert should be asked about; a starting point when
somebody eventually asks why a report from the mainframe disagrees with one from the new system;
and a checklist for the migration, because every concession is a decision that has to be made
properly when the legacy system goes.

**An empty concessions list on a real integration means nobody has looked.**

## Both directions, or the layer is decorative

An inbound-only layer feels sufficient because reading is what you start with. Then something needs
to write.

The write path is where the pressure to bypass is strongest: it is one field, the format is obvious,
and constructing a padded order number inline is three characters of code. Every one of those is
reasonable, and collectively they put the legacy model back into the modern codebase — this time
scattered, rather than concentrated in a layer that could be deleted.

Building both directions from the start costs little and removes the temptation. It also surfaces
the asymmetries early: the modern model can express things the legacy one cannot, and discovering
that at write time — as a concession — is much better than discovering it in production.

## The layer must not become the domain

The layer is the only component that understands both models, which makes it the most convenient
place to put anything involving both. That convenience is the failure mode.

A rule that holds: **the layer decides how to say something in the other model. It does not decide
what the business should do.** Mapping `B` to `placed` is translation. Deciding that orders held in
the overnight batch should not be shown to customers is a business rule, and it belongs to the
service that owns customer-facing behaviour.

The diagnostic question: if the legacy system were replaced tomorrow with something modern, would
this logic still be needed? If yes, it is domain logic in the wrong place.

## Test against real legacy data

Legacy systems have decades of accumulated exceptions, and none of them are in the specification.
Records from before the current numbering convention. Statuses that were used for two years in the
1990s and never removed. Customer numbers with a letter in an unexpected position because of an
acquisition. Dates of `00000000`.

A layer tested only against well-formed examples will fail on the third day of production, in a
batch, at night. The practical approach is to take a real extract — sanitised — and run every record
through the translator, then look at what it rejected or conceded. That exercise routinely finds
more about the legacy system than its documentation contains.

Every one of those discoveries is either a concession or a defect, and both want recording.

## Plan the layer's death

An anti-corruption layer is scaffolding. It exists because a legacy system exists, and when that
system goes the layer should go with it.

Layers that outlive their legacy systems do so because they accumulated the domain logic the
previous section warns about, and then nobody could tell which parts were translation and which
were business. The defence is the same discipline: keep it purely translational, and it will be
deletable.

Where the layer is part of a migration rather than a permanent integration, it usually pairs with
Strangler Fig — the router shifts traffic feature by feature, and the anti-corruption layer serves
whatever has not moved yet. Each feature that migrates removes a piece of the layer, and the last
one removes it entirely. That is the shape to aim for.

## What this repository's model leaves out

Both systems are in one process, so there is no network, no latency, no partial failure and no
legacy system that is unavailable during its overnight batch — which is when most real integrations
discover their assumptions. Errors are not translated because none occur. There are no sentinels, no
schema evolution and no bulk path. And the legacy data is clean and consistent, where real legacy
data is the opposite and that is most of the work.
