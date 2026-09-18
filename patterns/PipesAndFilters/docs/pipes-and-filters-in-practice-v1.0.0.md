# Pipes and Filters in practice

Supporting material for [the Pipes and Filters pattern](../README.md).

## The entry type is where this pattern goes wrong

Every filter takes the same type and returns the same type. That uniformity is what makes stages
interchangeable, and it is also the pattern's main long-term hazard.

The entry starts small. Then parsing needs somewhere to put the level, enrichment needs somewhere
for the host, formatting needs somewhere for the output — and the type in this folder already
carries five fields, several of which are empty at any given moment. Add a stage that needs
correlation ids and a stage that needs a tenant, and within a year the type is thirty fields, each
meaningful to one stage and noise to the rest.

Three things keep it under control:

* **Notice when most fields are empty most of the time.** That is usually two pipelines wearing one
  type, and splitting them is cheaper than continuing.
* **Prefer a payload plus a metadata bag** over a flat record with a field per stage, once past a
  handful of stages. It is less type-safe and much less prone to accumulating.
* **Keep a stage's output where the next stage reads it**, rather than adding a field per stage and
  leaving all of them populated. Fields that outlive their usefulness are what makes the type grow.

## Rejection and failure are different, and one path is not enough

The filter interface here has two outcomes: transform, or return nothing. That conflates two things
that want different handling.

**Rejection** is a judgement about the data: this line is malformed, this record is a duplicate,
this image is too small. Dropping it is correct, and the batch continues.

**Failure** is a problem with the processing: the enrichment service is down, the database timed
out. Dropping the entry here loses data because of an outage — the entry was fine.

A model with one nullable return cannot tell them apart, which is a real limitation of the shape in
this folder and worth naming rather than glossing. Real implementations distinguish them: rejection
routes to a quarantine or a dead-letter destination with the reason attached, while failure retries
and eventually leaves the entry on the pipe so it is reprocessed rather than lost.

The tell that a system has got this wrong is a silent drop in throughput during a downstream
outage, with no error anywhere and a permanently missing hour of logs.

## Durable pipes change what the pattern is

With method calls as pipes, a pipeline is a design idea: it organises code and nothing more.

With **queues** as pipes it becomes an operational architecture. Each stage is a separate deployable
with its own scaling; an entry that is being processed when a stage crashes returns to the queue
rather than vanishing; and the slow stage gets ten workers while the fast one keeps two. That is
usually the reason to reach for the pattern in a distributed system at all.

The costs arrive with the benefits. Each hop is latency and serialisation. At-least-once delivery
means every filter must be idempotent, because a crash after the work and before the acknowledgement
re-runs it. Ordering is generally lost unless the queue provides it per key. And observability gets
harder: the correlation id that ties one entry's journey together stops being a convenience and
becomes a requirement.

## Filters must be idempotent once pipes are durable

Worth stating separately because it is the most common omission. A redaction filter re-run is
harmless. A filter that appends an audit row, charges a fee, or sends a notification is not — and
under at-least-once delivery it *will* be re-run.

The usual mechanism is a processed-entry log keyed by the entry's identity, checked before acting.
That is Idempotent Consumer, and in a pipeline it is needed **per stage**, since each stage has its
own pipe and its own redelivery.

## Composition is data, so let it be data

The strongest form of this pattern makes the arrangement configuration rather than code: a list of
stage names, resolved at startup. New pipelines then need no deployment, and the composition can be
inspected, diffed and reviewed as configuration.

Two guards make that safe. Validate the composition at startup — a stage that needs parsed input
placed before the parser should fail loudly then, not silently produce empty output for a week. And
keep the list of available stages small enough to reason about; a registry of forty filters and
free-form composition is a workflow engine, and a real one would be a better choice.

The model in this folder stops short of that: compositions are constructed in code, which is enough
to show that two arrangements share their stages.

## What this repository's model leaves out

The pipes are method calls: nothing is durable, nothing is queued, and no entry is lost between
stages — which is what real pipes exist to prevent. Stages cannot scale independently because there
is nothing to scale. There is no retry, no dead-lettering and no back-pressure. Failure is modelled
only as rejection, so the distinction the second section is entirely about cannot be demonstrated
here. Composition is code rather than configuration. And everything is synchronous, so the slow
stage never becomes the bottleneck that motivates the pattern operationally.
