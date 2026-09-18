# Strangler Fig in practice

Supporting material for [the Strangler Fig pattern](../README.md).

## The data is the migration

The routing is easy. Two systems reading and writing the same accounts is the part that decides
whether the migration succeeds, and it is entirely absent from the model in this folder.

Once `invoice` is served by the new system and `refund` by the old one, both need the same account
balances. There are three workable answers and one that is not:

* **Shared database.** Both systems read and write the same tables. Simplest to start, and it means
  the new system inherits the old schema — often the very thing being escaped. Acceptable as a
  transitional state, dangerous as a destination.
* **Legacy owns the data; the new system calls it.** Clean ownership, and every migrated feature
  still depends on the system being replaced. Usually correct early, and it means the legacy system
  cannot be switched off until the data moves too — which is its own migration.
* **Synchronise both ways.** Each system owns some data and changes propagate. Most flexible, and
  it introduces reconciliation, conflict resolution and a category of bug that is very hard to find.
* **Duplicate and hope.** Each system keeps its own copy, updated independently. This is not a
  strategy; it is a data-corruption incident with a schedule.

The decision belongs before the second feature migrates, not after. A migration that reaches 60%
before anybody addresses data ownership has built its hardest problem into six places.

## Shift traffic within a feature, not just between features

The model here migrates a feature wholly: one call to `Migrate` and every request for it goes to the
new implementation. Real migrations rarely do that for anything that matters.

The usual refinement is a percentage: 1% of accounts, then 10%, then 50%, then all. The router
decides per request rather than per feature, keyed on something stable so a given account sees a
consistent implementation.

Two techniques earn their place at this stage:

* **Shadow traffic.** Send the request to both, serve the legacy answer, and compare. Differences
  are logged rather than returned, so the new implementation is validated against production
  behaviour without any customer seeing it fail. This finds the undocumented behaviour that no
  specification contains.
* **Automated comparison.** Where results should be identical, assert it continuously rather than
  spot-checking. Where they legitimately differ — the new system rounds correctly and the old one
  did not — that difference is a decision somebody must make, and finding it during shadowing is
  much better than finding it in a customer complaint.

## The fallback count is a map of what nobody wrote down

The router falls back to the legacy system for features nobody enumerated, and counts them. That
count is the most useful number the pattern produces after the migrated proportion.

A legacy system old enough to need replacing has capabilities that exist only in code: a report
somebody generates once a quarter, an endpoint one partner still calls, a branch reached only when a
particular flag is set. None of these are in the feature list, because nobody knew.

Counting the fallbacks turns that unknown into a work queue. The rule worth adopting: **the
migration is not finished when the feature list is empty; it is finished when the fallback count is
zero and has stayed zero.** Those are different dates, sometimes by months.

## The pattern's own worst outcome

Big-bang rewrites fail loudly. Strangler migrations fail quietly, by stopping.

Everything works at 60%. The new system serves the interesting features, the legacy one serves the
tedious remainder, and the team is moved onto something else. Two systems now run permanently, with
two deployment pipelines, two on-call rotations and a routing layer nobody remembers the reason for.
That is worse than either whole system, and the pattern's own comfort is what allowed it.

The defences are managerial rather than technical:

* **An end date, defended.** Not for the whole migration necessarily, but for the legacy system's
  switch-off, with the cost of running both systems visible against it.
* **Migrate the tedious features early**, not last. The ones nobody wants are the ones that will be
  abandoned, so take them while there is momentum.
* **Report the proportion where it is seen.** A number on a dashboard that has not moved in two
  months asks its own question.
* **Count the cost of the middle.** Two systems running is a real, quantifiable expense, and making
  it visible is the strongest argument for finishing.

## Delete as you go

Each migrated feature leaves dead code in the legacy system. Leaving it "just in case" is reasonable
for a sprint and corrosive over a year: it still compiles, it still appears in searches, and
eventually somebody edits it — fixing a bug in an implementation that has served no traffic since
March.

Deleting immediately after a feature's traffic has settled keeps the legacy system shrinking
visibly, which is both good hygiene and good morale. Version control is the "just in case".

The same applies to the router: when a feature's migration is complete and settled, its entry
becomes a permanent `modern` rather than a switch. When they all are, the router itself goes.

## Where the anti-corruption layer fits

The two patterns pair naturally and are not the same thing.

While the migration runs, the new system frequently needs data that the legacy system still owns.
Calling it directly means the legacy model reaches into the new one — precisely what Anti-Corruption
Layer prevents. So the new implementation talks to the layer, the layer talks to the legacy system,
and the router decides which implementation serves each feature.

The distinction that matters when planning: the **router** is temporary and its purpose is to
disappear; the **layer** is temporary only for as long as the legacy system exists, and both should
be deleted together rather than one outliving the other by accident.

## What this repository's model leaves out

Both systems share a process and no data at all, so the hardest part of a real migration — keeping
two systems consistent while both write — is entirely absent, and it is the first section above for
that reason. There is no gradual traffic shift within a feature, no shadow traffic and no automated
comparison. There is no rollback demonstrated, no feature flag and no deployment. And the feature
list is complete apart from one deliberate unknown, where a real legacy system's list is discovered
over months.
