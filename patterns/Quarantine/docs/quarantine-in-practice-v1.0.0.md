---
doc_id: quarantine-in-practice
title: Quarantine in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-18
updated: 2026-08-18
---

# Quarantine in practice

Supporting material for [the Quarantine pattern](../README.md).

## A gate that is slow gets routed around

This pattern fails socially far more often than technically. The checks work; the developer waiting
on them does not wait.

If admitting a package takes a day, somebody will vendor the source into the repository, or pull
directly from the public registry "just for this spike", or find that the build already works
because an older version is cached. None of those people are being reckless — they are being
productive against an obstacle, and the obstacle was put there by people who are not blocked by it.

What keeps a gate respected:

* **Automate everything and make it fast.** Minutes, not days. A gate in the pipeline is a gate;
  a gate in somebody's inbox is a queue.
* **Make the good path the easy path.** If the internal feed is faster and more reliable than the
  public one, nobody looks for a way around it.
* **Make bypass genuinely hard**, at the network or the build level. A rule that says "always use the
  internal feed" is a request; a network policy is a gate.
* **Explain refusals in terms the developer can act on** — which library to use instead, when a fix
  is expected, how to request an exception.

## Unchecked is not clean

The model in this folder refuses an asset with no scan report, and the point is worth stating
plainly because the mistake is so natural: a gate that admits anything with no findings will admit
everything that was never examined.

That happens more often than it sounds. A scanner that timed out. A file format the tooling does not
understand. A new artefact type nobody added to the pipeline. An asset added before the gate existed.
In each case there are no findings, and in none of them is the asset known to be safe.

Fail closed. The cost is a developer waiting for a scan that has not run; the alternative is a gate
whose coverage silently equals whatever the tooling happens to support.

## Exceptions need an owner and an expiry

Every real gate refuses something the team needs. The library with a vulnerability in a code path
nobody calls. The GPL component that is genuinely fine because it is not distributed. The unscannable
artefact from a vendor who will not change.

Refusing to have an exception process does not remove exceptions; it moves them into arguments,
workarounds and eventually a disabled check. So the process should exist, and it should have three
properties:

* **A named owner** who accepts the risk — not "the team", a person.
* **An expiry**, so the exception is revisited rather than inherited. Ninety days is common and the
  precise number matters less than that one exists.
* **A record** attached to the asset, so anybody wondering why the rule did not apply can find out
  without archaeology.

An exception register nobody has read in a year is a list of decisions still in force that nobody
remembers making.

## Admission is not permanent

An asset admitted last year is not clean today. Vulnerabilities are discovered in code that has not
changed — that is the normal case, not the exception — so a gate that only checks on entry protects
against yesterday's threats.

Two mechanisms, and both are needed:

* **Periodic rescanning of the registry**, so newly published advisories are matched against what is
  already admitted. This is the part most often missing.
* **A way to withdraw**, which is harder than it sounds: the asset is already in builds, images and
  running systems, so withdrawal is a remediation project rather than a delete.

The registry should therefore record not only what was admitted, but **what consumed it** — otherwise
"who is using this vulnerable version?" is answered by searching every repository by hand.

## Pin what was admitted

Admitting `serilog` and then consuming whatever `serilog` resolves to tomorrow defeats the gate
completely: the thing checked and the thing installed are different artefacts.

Admission must be **by version, and ideally by digest**. A version can be republished under the same
number in some ecosystems; a content digest cannot. The registry entry then names an exact artefact,
the build resolves to that artefact, and a new version is a new submission.

This is also what makes the gate's answer to "what are we running?" true rather than approximate.

## The gate belongs to whoever bears the risk

A gate imposed by a security team on a delivery team is a negotiation with every refusal. A gate
whose standards the delivery team agreed to is a shared tool.

The catalogue's own wording says **team-agreed quality level**, and that phrase is doing real work.
The checks encode a decision about acceptable risk, and that decision has to be made by people who
also feel the cost of it — otherwise the standard drifts upward without limit, because whoever sets
it pays nothing for strictness.

In practice this means the standard is written down, reviewed periodically, and changed by agreement
rather than by whoever last configured the scanner.

## What this repository's model leaves out

**There is no supply chain.** The gate is a method call: no registry, no download, no digest and no
pinning, so the section above cannot be demonstrated at all. Scanning is a boolean rather than a
tool with findings, severities and false positives. There is no exception process, which is where
most of the operational weight sits. There is no periodic rescanning and no withdrawal, so an asset
admitted here stays admitted for ever. There is no record of what consumed what. And nothing shows
the developer friction that determines whether a real gate is respected or routed around — which is
the first section, and the one that decides whether the pattern works.
