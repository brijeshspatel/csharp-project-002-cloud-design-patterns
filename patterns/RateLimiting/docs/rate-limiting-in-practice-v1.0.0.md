---
doc_id: rate-limiting-in-practice
title: Rate Limiting in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Rate Limiting in practice

Supporting material for [the Rate Limiting pattern](../README.md).

## The arithmetic that catches everybody

A published limit of 100 requests per second, a worker paced at 100 per second, and ten instances of
that worker, is 1,000 requests per second. The limiter is working perfectly and the contract is
being broken by a factor of ten.

The fix is to divide the budget, and the difficulty is that the divisor moves. Autoscaling changes
the instance count without telling the limiter; a deployment briefly runs old and new instances
together; and somebody's local debugging session is an eleventh consumer nobody counted.

Three approaches, in increasing order of cost and accuracy:

* **Static subdivision.** Each instance gets `limit / expected instances`. Trivial, and wrong the
  moment the count changes — wasteful when scaled in, over the limit when scaled out.
* **Coordinated allocation.** Instances lease a share from a shared store and renew it. Accurate,
  and now the limiter has a dependency that can fail, with the same fail-open-or-fail-closed
  dilemma a distributed throttle has.
* **Adaptive.** Pace optimistically, watch for `429`s, and back off the local rate when they arrive.
  Self-correcting and needs no coordination, but it learns the limit by exceeding it, which is
  precisely what the pattern was adopted to stop.

Most systems end up with static subdivision plus honest handling of the occasional `429`, and that
is a reasonable place to land as long as it is a decision rather than an accident.

## Why a bucket beats a window

A fixed window is easier to implement and easier to explain, and it has a flaw that shows up under
exactly the load you cared about.

With a limit of 100 per minute, a client may send 100 in the final second of one window and 100 in
the first second of the next: 200 requests in two seconds, entirely within the rules as written. The
server sees a burst at twice the intended rate and may well throttle — which is the outcome the
client adopted rate limiting to avoid.

A token bucket has no boundary to exploit. Tokens accrue continuously, so the rate holds over every
interval and not merely over the ones that line up with a window. The capacity still permits a
burst, but a *bounded* one that the server has agreed to.

The cost is that a bucket needs a timestamp and a fractional count rather than an integer, and that
fractional token arithmetic is where the subtle bugs live.

## What to do when a 429 arrives anyway

It will. Your model of the limit is a model.

The limit may have changed; it may be shared with a job you did not know about; the service may
throttle on a dimension you are not tracking, such as payload size or request cost; or it may simply
be shedding load for reasons of its own.

Three rules:

1. **Obey `Retry-After` when it is present.** The server knows more than your model does. Backing
   off less than it asked for is how a soft throttle becomes a hard block.
2. **Feed it back into the limiter.** A `429` is evidence your local rate is wrong. Slowing down and
   recovering gradually beats continuing at a rate the server has just rejected.
3. **Do not let Retry defeat the pacing.** With a retry inside the limiter, a refused call is
   re-attempted without consuming a token, and the pacing is bypassed exactly when it matters. The
   limiter belongs on the inside — every attempt, first or retried, takes a token.

## Leave headroom, and count everything that calls

Pacing at exactly the published rate assumes your clock, the server's clock, and the network all
agree. They do not.

It also assumes you have counted every caller. Health checks, warm-up requests, the retry that
re-sent a call you thought had failed, and the metrics exporter polling the same API all consume the
same budget, and none of them usually goes through the limiter.

Pacing at eighty or ninety per cent of the published limit costs a little throughput and removes an
entire category of intermittent, hard-to-reproduce failure.

## What this repository's model leaves out

Nothing ever waits — the clock is advanced by hand, so `RetryAfter` is asserted rather than honoured
by a real delay. There is no `WaitAsync`, no queue of pending permits and no cancellation, which is
most of what `System.Threading.RateLimiting`'s own type exists to provide. State is in one process,
so none of the multi-instance arithmetic above is exercised. No real `429` is ever received. And the
limiter counts requests rather than weighting them by cost, so the request-unit model that Cosmos DB
and similar services actually use is absent.
