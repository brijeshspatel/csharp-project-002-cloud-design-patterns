# Gatekeeper in practice

Supporting material for [the Gatekeeper pattern](../README.md).

## The guarantee lives in the network, not in the code

Everything in this folder can be correct and the pattern still provide nothing, because the claim
being made is *the private side is not reachable from outside* — and no amount of C# establishes
that.

In production the mechanism is infrastructure: private endpoints, VNet integration, network security
groups, a service that simply has no public ingress. The code's job is to not undermine it.

Which means the pattern has an acceptance test that is not a unit test: **try to reach the private
side from outside and fail.** Run it from a machine on the public internet, on a schedule, and alert
when it starts succeeding — because the day someone adds a public IP "temporarily for debugging" is
the day the guarantee quietly ends and nothing in any test suite notices.

A gatekeeper whose private side has never been probed from outside is an architecture diagram.

## "No credentials" means none, including read-only ones

The pressure to give the gatekeeper one small credential is constant and always reasonable-sounding.
It needs to check whether a reference already exists. It needs a lookup table for valid product
codes. It needs to write its own audit log.

Each of those gives the exposed component a key, and the guarantee is not "the gatekeeper holds few
secrets" — it is that compromising it yields nothing. A read-only credential to the customer database
is a customer data breach.

The workable answers, in order of preference:

* **Do without.** Validate shape, size, encoding and schema — none of which needs data. Most
  validation is of this kind.
* **Push the check to the private side.** It has the data and it is already receiving the
  submission; let it reject on business grounds and return a reason.
* **Give the gatekeeper its own store**, containing only what is safe to lose — a list of valid
  product codes is not sensitive. Separate credential, separate store, nothing shared with the
  protected one.

The failure to avoid is the fourth option nobody writes down: reuse the private side's credential
because it is already in the key vault.

## Validation and sanitising are different, and both are needed

Validation answers a question: is this well-formed, within limits, of the expected schema? Its output
is yes or no.

Sanitising changes the value: it removes what should never have been sent, so that everything
downstream can treat the input as trustworthy.

A boundary that only validates leaves every downstream component responsible for defending itself
against content that passed. A boundary that only sanitises accepts nonsense and quietly transforms
it into different nonsense. Both together mean the private side can be written without defensive
noise — which is a large part of why the private side is allowed to be simple.

The implementation in this folder makes one specific point about sanitising, learnt by getting it
wrong first: it removes elements **whole**. Stripping `<script>` and `</script>` and leaving
`alert(1)` behind produces a string that looks sanitised, passes a naive test, and has kept the
payload. Half-sanitising is worse than none, because it produces confidence.

Real sanitising uses a maintained library with an allowlist, not hand-written string surgery. The
version here is deliberately crude and the README says so.

## Fail closed, and say why

Two habits that matter more than they look.

**Fail closed.** Anything the gatekeeper does not recognise is refused. The tempting alternative —
forward it and let the private side decide — hands unvalidated input to the component that holds the
credential, which is the one thing the design exists to prevent. If the gatekeeper cannot decide, the
answer is no.

**Say why, distinctly.** "Rejected" is useless operationally. Distinct reasons let an operator tell
apart a client that has shipped a bug, a limit that is set too low for legitimate traffic, and a
scanner probing for weaknesses. Those need three different responses, and a single counter supports
none of them.

Two cautions on the second point. Do not return the reason's full detail to the caller where it
reveals internal structure — log the detail, return a category. And alert on the *shape* of refusals
rather than the volume: a sudden concentration in one reason is a signal, while a flat rate is
usually just the internet.

## What the gatekeeper does and does not vouch for

The private side receives clean input and should treat it as clean. That is the deal, and it is worth
being precise about its limits.

The gatekeeper vouches for the **shape** of the submission: well-formed, within size, no markup, of
the expected schema. It does not vouch for **who sent it** unless it also authenticated them, and it
does not vouch for the submission being *true*.

So a private side that assumes "this came through the gatekeeper, therefore this user is authorised
to modify this claim" has assumed something nobody checked. Authentication and authorisation are
separate concerns that may also live at the boundary — Gateway Offloading is the pattern for the
first — but they are not implied by the sanitising.

## Two components, and the second one is not optional

It is easy to implement this pattern as one deployable with two classes, which is what this folder
does for teachability. In production that provides nothing at all: a compromise of the process is a
compromise of both halves, and the credential is in the same memory.

The split must be a **process and network boundary** — separate deployables, separate identities,
separate credentials, no route from outside to the second one. Where even that feels heavy, the
lightest real version is a queue: the gatekeeper writes a message, the private side reads it, and the
two never connect. That also removes the synchronous coupling, at the cost of the caller not learning
the outcome immediately — which is Asynchronous Request-Reply territory.

## What this repository's model leaves out

There is no network, so "not reachable from outside" is a property of the object graph rather than of
network policy — which in production is the entire mechanism. The two components share a process, so
a compromise of one is a compromise of both, which is exactly what the pattern prevents. There is no
managed identity, no key vault and no real credential. There is no rate limiting or request-size
limit at the transport level. And the sanitiser is crude by design and would not survive a real
attacker.
