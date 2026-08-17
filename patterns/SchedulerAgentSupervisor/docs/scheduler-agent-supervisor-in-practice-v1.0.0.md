---
doc_id: scheduler-agent-supervisor-in-practice
title: Scheduler Agent Supervisor in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Scheduler Agent Supervisor in practice

Supporting material for [the Scheduler Agent Supervisor pattern](../README.md).

## The deadline does not tell you what happened

When a step passes its deadline, exactly one thing is known: no answer arrived in time. The three
possibilities behind that are indistinguishable from outside, and they want different responses:

* **The work never started.** The request was lost, or the agent died before beginning. Retrying is
  correct and free.
* **The work is still running.** The agent is slow, not gone. Retrying duplicates it.
* **The work completed and the answer was lost.** The transcode finished, the callback failed.
  Retrying redoes work that is already done — and if the step has side effects, does them twice.

A supervisor that treats the deadline as proof of failure is wrong two times in three. What makes
the retry safe is not the supervisor's judgement but the step's design:

* **Idempotency keyed by the step's identity.** The agent is asked to perform `job-1042/transcode`;
  asking twice produces one transcode. This is the strongest answer and the one to design for.
* **Query before retry.** Ask the downstream service whether the step completed, and act on the
  answer rather than assuming. Costs a round trip and requires the service to support it.
* **Accept duplication.** Legitimate where the step is cheap and harmless — a thumbnail regenerated
  is a wasted second, not a wrong outcome.

What does not work is tuning the deadline until the ambiguity goes away. It does not go away; a
longer deadline just moves the cases around.

## Choosing the deadline

Too short and the supervisor duplicates work that was about to finish, adding load to a system that
is already slow — which makes the next step slower, which triggers more retries. That loop has a
name in production and it is not a pleasant one.

Too long and stalls sit undetected. The customer notices before the system does.

The usable rule is to set it from **observed completion times**, at a high percentile, with
headroom — the 99th percentile plus a margin, revisited as the workload changes. Two refinements
earn their keep:

* **Per-step deadlines.** A probe and a transcode do not belong on the same clock.
* **Progress heartbeats.** An agent that reports "still working" every thirty seconds converts an
  unknown into a known, and lets the deadline apply to *silence* rather than to total duration.
  This is the single largest improvement available to the pattern, and it requires the agent to
  cooperate.

## Retry once, then escalate — and why not more

The instinct is to retry until it works. What that produces is a step that has been retried four
hundred times overnight, consuming capacity, while the job it belongs to has been stuck since
midnight and nobody has been told.

An escalation is a *decision to stop guessing*. It converts a technical condition into an
operational one — a named work item with a person eventually attached. That is almost always better
than indefinite retry, because the cases where retry helps are the transient ones, and transient
means the second attempt usually works.

Where more retries genuinely help — a flaky third party, say — bound them and back them off, and
keep the escalation at the end. The property worth preserving is that the sequence **terminates**
in something visible.

## The supervisor is a component, and it can fail too

A supervisor holding its state in memory forgets, on restart, which steps it had already retried —
so it retries them again, and again after the next restart. The state that says "this step has had
its retry" belongs in the same durable store as the step state.

A supervisor is also a single point of failure for detection. The usual answer is to run several and
have exactly one active at a time, which is Leader Election, implemented separately in this tier. Two
active supervisors both sweeping the same overdue step will both retry it, which is the duplication
problem arriving by a different route.

Both of those are why the pattern names the supervisor as a role rather than describing it as "a
timer somewhere".

## Where the platform already does this

Much of this pattern is available off the shelf, and hand-rolling it is often the wrong choice:

* **Service Bus message locks.** A message locked by a consumer that goes silent becomes visible
  again when the lock expires — a scheduler and supervisor, built in, for the case where the step is
  a message. Its dead-letter queue is the escalation.
* **Durable Functions.** Durable timers make "no answer by the deadline" a branch in ordinary code,
  with the orchestrator's history providing the durable state.
* **Kubernetes job deadlines and pod eviction.** Where agents are pods, the platform already
  notices and restarts them.

The pattern is worth implementing by hand when the agent is a third party outside all of that, or
when the steps span several systems none of which owns the whole job.

## What this repository's model leaves out

Everything runs in one process: the agent's silence is a return value rather than a network that
never answers, and no message is lost, delayed or delivered twice. The supervisor is invoked by
hand rather than by a timer, and its state is in memory, so it cannot fail and restart — the case
that makes durable supervisor state necessary. There is one supervisor, so nothing shows why it
wants electing. There are no heartbeats. And the retry never duplicates real work, because the
agent here has no side effects — which is precisely the hazard the first section is about.
