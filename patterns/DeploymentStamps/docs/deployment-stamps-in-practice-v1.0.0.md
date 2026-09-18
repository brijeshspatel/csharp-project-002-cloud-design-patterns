# Deployment Stamps in practice

Supporting material for [the Deployment Stamps pattern](../README.md).

## Sharing anything undoes the pattern

A stamp is isolated because it shares nothing. Every exception to that is reasonable when proposed
and cumulative in effect.

The reporting database that reads from all stamps. The shared Redis cache "just for sessions". The
single Service Bus namespace, because managing three is tedious. The one Key Vault. Each is a
component whose failure is once again everybody's, and the pattern's central claim — that a failure
costs one stamp's tenants — quietly stops being true.

The workable rule: **a stamp's request path touches nothing outside its own stamp.** Things outside
that path can legitimately be shared:

* The **tenant-to-stamp assignment**, which every stamp arrangement needs somewhere. Make it small,
  boring and extremely available, because it is the one genuine single point of failure.
* **Reporting and analytics**, fed asynchronously *from* stamps rather than queried *across* them.
  A stamp publishing events to a shared analytics store never waits on it.
* **Deployment machinery and templates**, which are not in the request path at all.

The test to apply to any proposed shared component: if it is unavailable, can a healthy stamp still
serve its tenants? If not, it has joined the request path.

## Tenant migration is the hard operation, and it will be needed

Every stamp arrangement eventually needs to move a tenant. They outgrow their stamp, or a stamp is
retired, or a customer's data must move jurisdiction.

Moving a tenant means moving a data store while a customer uses it, and it has the shape of a
strangler migration: copy while the source keeps serving, apply the changes that landed during the
copy, cut the routing over, keep the old copy briefly, then delete it. The difference from a normal
migration is that both sides are live systems and the cutover has to be atomic from the tenant's
point of view.

Three things make it survivable, and all of them are cheaper to build before the first migration
than during it:

* **A per-tenant export and import** that is exercised routinely — ideally the same mechanism used
  for onboarding.
* **A brief, honest read-only window**, which is far easier to reason about than a bidirectional
  synchronisation and is usually acceptable if it is short and scheduled.
* **A routing switch that is instant and reversible**, so a failed cutover is a switch back rather
  than a restore.

A team that has never migrated a tenant does not know whether it can.

## Stamp sizing, and the tenant that fills one

The economics assume tenants are small relative to a stamp. One tenant that consumes most of a stamp
breaks that: the stamp cannot take more tenants, its capacity is stranded, and the tenant has no
isolation from itself.

Two responses, both legitimate. **Give that tenant its own stamp** and price accordingly — this is
how premium tiers usually arise, and the pattern supports it naturally because a stamp is already a
unit of one deployment. Or **cap tenant size per stamp** and treat anything larger as a different
product with a different architecture.

What does not work is hoping. Define "full" as a measurable threshold — storage, throughput, tenant
count, whichever binds first — and stop assigning to a stamp before it gets there, because the
alternative is discovering the limit during someone's peak.

## Staged deployment is the benefit teams actually notice first

The isolation argument is the one in the diagrams. The one that changes daily life is that a release
can go to one stamp and stay there for a day.

That turns "we deployed and it broke" from an incident affecting every customer into an incident
affecting a sixth of them, discovered by monitoring rather than by support. It also makes rollback
meaningful — one stamp back, not a whole platform.

It requires discipline the pattern does not enforce: stamps must be genuinely identical apart from
their data, or a release that works on stamp 1 proves nothing about stamp 2. Configuration drift
between stamps is the failure mode, and it accumulates through exactly the small manual fixes that
incidents encourage.

## The router is small and must be right

Everything else in this pattern is replicated; the tenant-to-stamp assignment is not. It is the one
piece of state that spans stamps, and it has two failure modes worth separating.

**Unavailable** is survivable if stamps can be reached directly by tenants that already know their
home — a cached assignment at the client, or a stamp-specific hostname, keeps existing traffic
flowing while new assignments wait.

**Wrong** is much worse. A tenant sent to the wrong stamp sees an empty world: no data, no history,
no orders. It looks like data loss to the customer and it is not, which makes it a support incident
of unusual unpleasantness. Guard it with the same care as a payment path — the assignment is
authoritative, durable, backed up, and changed only through an audited operation.

## What this repository's model leaves out

**There is no deployment.** Stamps are objects in one process, so the operational cost that is the
pattern's main trade — several deployments to patch, monitor and pay for — is described rather than
felt. There is no data store, so "its own data store" is a statement rather than a demonstration and
cross-stamp queries cannot be shown to be expensive. There is no tenant migration, which is the
hardest operation the pattern requires. There is no staged rollout and no configuration drift. And
the assignment lives in memory, where in production losing it would lose every tenant's home.
