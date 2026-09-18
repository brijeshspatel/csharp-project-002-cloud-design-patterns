# External Configuration Store in practice

Supporting material for [the External Configuration Store pattern](../README.md).

## Configuration reaches production faster than code, and gets less scrutiny

This is the pattern's central irony. A code change goes through review, tests, a build, a staged
deployment and a rollback plan. A configuration change goes through a text box.

Both can take the site down, and the configuration change does it faster and to everything at once.
The demonstration in this folder makes the point deliberately: the same mechanism that turned a
feature on in one action set it to nonsense in one action, across all four instances, at identical
speed.

What closes the gap is treating configuration as a deployable artefact:

* **Review it.** Configuration in version control, changed by pull request, applied by pipeline. The
  store becomes the *deployment target* rather than the editing surface.
* **Stage it.** One instance, then one stamp, then the fleet — the same shape as a code rollout, and
  available in most stores through labels or targeted flags.
* **Make rollback trivial and rehearsed.** The history is what makes "what was it before?"
  answerable at three in the morning, which is when the question is asked.
* **Validate on write.** A store that accepts `obviously-wrong` for a boolean flag has passed a
  checkable error through to every instance. Schema validation at the store is cheap and catches the
  most common class of mistake.

## The startup dependency, and why packaged defaults are not optional

An application that reads its configuration remotely has acquired a dependency that is present at
its most fragile moment: startup, usually during an incident, often while scaling out because
something else is already wrong.

If the store is unreachable and there are no defaults, instances fail to start. The failure is
correlated — every instance, every region — and it happens exactly when capacity is most needed.

Packaged defaults change that failure into a degradation: the fleet starts with the values it
shipped with, serves traffic, and picks up the real configuration when the store returns. That is
almost always the right trade, and it has two requirements worth stating:

* **Defaults must be safe**, not merely present. A default that enables an unfinished feature is
  worse than failing to start.
* **Running on defaults must be visible.** An instance quietly serving shipped values while everyone
  believes it has the current ones is the same class of problem as a lagging geode node.

## Cache, but bound the refresh

Reading the store on every request is a network call per request: latency added to everything, and a
store whose availability is now on the critical path of the request rather than of startup.

Caching for ever is the opposite mistake — it recreates the restart requirement the pattern exists
to remove, with the added confusion that some instances may have restarted recently and others not,
so the fleet is inconsistent in a way nothing reports.

The workable middle is a bounded refresh: cache for a short interval, or subscribe to a change
notification with a periodic poll as a backstop. Two details matter. **Refresh must be observable**,
so a node that has stopped refreshing is detectable — the version-per-instance in this folder's model
is the minimum. And **refresh must not stampede**: a thousand instances refreshing on the same
boundary is a load spike the store did not expect, so jitter the interval.

## Secrets are a different problem

Connection strings, API keys and certificates are configuration in the sense that they vary by
environment, and they are not configuration in every way that matters operationally: they need
tighter access control, rotation, expiry, and an audit trail of *reads* rather than only of writes.

Putting them in the general store means everyone who can read configuration can read the database
password, and that set is usually much larger than intended.

The standard arrangement is two stores: a configuration store holding a **reference** to the secret,
and a secret store holding the secret itself, with the application resolving the reference using its
own identity. That keeps one place to look for settings while keeping the sensitive values under
their own controls.

## Drift is invisible without a version

Four instances reading one store should be identical. They are not, if one has stopped refreshing —
and nothing about that instance looks unhealthy. It responds, it serves, it reports no errors, and it
is operating on settings from an hour ago.

A version per instance, reported wherever health is reported, turns that into a fact somebody can
alert on. It costs one field and it is the only thing that makes the fleet's consistency checkable.

The same applies across environments: knowing that staging is on v104 and production on v98 makes
"it worked in staging" a hypothesis with evidence rather than an argument.

## What this repository's model leaves out

The store is an object in the same process, so there is **no network**: no latency per read, no store
outage, and therefore no exercise of the packaged-default path under failure — which is that
fallback's entire purpose. There is no caching and no refresh interval: an instance is current
the moment it reads, and it reports the version its last read observed, so the only drift the
model exhibits is an instance that stops reading and visibly falls behind. The production shape of
it - a node refreshing on an interval that quietly fails - is described rather than shown. There are no
labels or environments, no staged rollout, and no validation on write. There is no access control,
which is a substantial part of the pattern's real operational weight. And there are no secrets, so
the two-store arrangement above is described rather than shown.
