# Compensating Transaction in practice

Supporting material for [the Compensating Transaction pattern](../README.md).

## Compensation is not rollback, and the difference is the whole pattern

A rollback erases history: the database discards uncommitted work and no observer ever saw it. A
compensation cannot do that, because the original action was **committed and observed**. The seat
was taken. The charge appeared on a statement. Somebody may have acted on it.

So a compensation is a second business event, and it should be modelled as one:

* It appears in the ledger, the audit trail and the customer's history. `Booked` then `Cancelled`
  is the truth; a single erased row is not.
* It can be refused. A hotel may decline a cancellation inside twelve hours, and a system that
  assumed compensation always succeeds has no way to represent that.
* It can have its own consequences — a cancellation fee, a restocking charge, a notification.

Teams that carry the rollback metaphor into the design end up surprised by all three. The metaphor
that works better is the accountant's: you do not erase an entry, you post a reversing one.

## Order, and what actually goes wrong when it is forward

Reverse order is often stated as a rule and rarely justified, which makes it easy to "simplify"
away. The justification is dependency: step N frequently depends on something step N-1 established,
so undoing N-1 first can leave N's counter-action with nothing to work against.

Concretely: a workflow creates a resource group, then a database inside it, then a firewall rule on
the database. Undo forward and the resource group goes first — taking the database with it — and the
firewall-rule compensation now fails against a resource that no longer exists. It usually *looks*
fine, because the end state is the same, and it fails loudly on the day one of those compensations
needed to record something before it disappeared.

Reverse order is not universal. Where steps are genuinely independent, any order works and
parallel compensation is legitimate. The discipline is to make it a decision with a written reason
rather than an accident of loop direction.

## The steps that cannot be undone shape the design

Every workflow has some. An email sent. A push notification delivered. A payment settled to a third
party's account. A physical package handed to a courier.

The best response is structural, and it is available more often than people expect: **order the
steps so the irreversible one is last**. If the confirmation email is sent after every reservation
has succeeded, its irreversibility never matters, because it only ever runs on the happy path. A
workflow that emails first and books afterwards has manufactured a problem that reordering
dissolves.

Where it cannot be moved, the second-best response is to model it explicitly, as this folder does
with a step that declares it has no compensation. That turns "the world is inconsistent" into a
named item in an outcome, which is the difference between a system an operator can finish by hand
and one they must reverse-engineer first.

## Compensations rot, because they only run on the bad day

The step runs constantly and is exercised by every test, every demo, every production request. Its
compensation runs rarely — sometimes for the first time months after it was written, in the middle
of an incident.

Three habits keep them honest:

* **Test the failure path as a first-class case**, not as an afterthought. Every compensation in
  this folder is exercised by a test, including the ones that fail.
* **Run the compensation path in staging deliberately**, on a schedule. A compensation nobody has
  executed since it was written is a hypothesis.
* **Keep the compensation next to the step**, in the same type, so a change to one is visibly a
  change to the other. Separating them into "the workflow" and "the cleanup handlers" is how they
  drift.

## When compensation fails, a human finishes the job

This is the case the pattern's diagrams usually omit. The hotel cancellation returns 503; the
retries are exhausted; the operation is now permanently half-undone.

There is no clever answer, and pretending otherwise is the failure mode. What a good implementation
does is make the situation **legible and actionable**: report exactly which steps remain in effect,
retain the identifiers needed to act on them, and raise it somewhere a person will see. The outcome
type in this folder exists for that reason — `NotUndone` is a work list, not a diagnostic.

The corollary is that a compensation should be retried before it is given up on. This model does not
retry, which is one of the things its README says it does not show; a real implementation would
retry with backoff, and only then escalate.

## What this repository's model leaves out

Every step is a function call in one process, so nothing fails midway through a step, nothing times
out and no compensation is lost in flight. Failed compensations are reported but never retried.
Nothing is durable: a crash between steps would lose the entire record of what had been done, which
is exactly the gap Saga exists to close. And the partial-completion window is instantaneous here,
where in a real system it is the window an operator has to worry about.
