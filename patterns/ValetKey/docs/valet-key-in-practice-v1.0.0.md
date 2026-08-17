---
doc_id: valet-key-in-practice
title: Valet Key in practice
type: explanation
version: 1.0.0
status: active
created: 2026-08-17
updated: 2026-08-17
---

# Valet Key in practice

Supporting material for [the Valet Key pattern](../README.md).

## Three limits, and what each one is for

A valet key carries a resource, a permission and an expiry. Teams routinely implement two of the
three and believe they have the pattern.

* **Resource.** Without it, the key opens the container. This is the most common and most damaging
  omission, because it usually works perfectly in testing — the client only asks for what it was
  given — and the exposure surfaces when someone tries the obvious variation on the URL. It is
  worse than it looks: a container key also grants access to files uploaded *later*, by other
  people.
* **Permission.** Without it, a download link can overwrite what it was meant to fetch. The damage
  is bounded by the scope, which is exactly why scope is worth getting right first.
* **Expiry.** Without it, the key is a permanent credential, and it is living in a client — a
  browser history, a mobile app's cache, a shared chat message. Every key ever issued stays valid
  forever, and nobody has an inventory.

Expiry deserves particular respect because it is **the only limit that enforces itself**. Scope
and permission are decided once, correctly or not, and never revisited. Expiry cleans up after
mistakes nobody noticed.

## Revocation is usually not available

The model in this folder keeps a registry of issued keys, so `Revoke` is a line of code. Most real
implementations do not work that way: a shared access signature is a **signature**, verified
mathematically by the storage service with no lookup and no state. That is why it scales, and it
means the service has no list of issued keys and nothing to remove.

The consequences are worth stating plainly:

* An issued key is valid until it expires, whatever happens in between.
* A leaked key cannot be recalled. The lifetime *is* the exposure window.
* Rotating the signing key invalidates every key signed with it — a blunt instrument, and
  sometimes the right one.

Azure's stored access policies restore revocation by adding one level of indirection: the
signature references a named policy, and the policy can be changed or deleted. It costs a lookup
and gives back control. Where revocation genuinely matters, that indirection is the answer — and
where it does not, short lifetimes are.

## Short lifetimes, and the clock

"Short" means as long as the operation plausibly takes, plus a margin — minutes for an upload, not
hours. The temptation is to be generous because a key that expires mid-transfer produces a
confusing failure, and the honest answer to that is renewal rather than length: issue a fresh key
when the client asks, which costs one authorised round trip and keeps the window small.

Clock skew is the practical wrinkle. The issuer and the validating service are different machines,
and a key issued to start "now" can be rejected as not-yet-valid by a service whose clock is a few
seconds behind. Backdating the start time slightly is the standard fix, and it is why this
folder's model takes an `IClock` rather than reading the system clock — the expiry boundary is
testable precisely because time is an input.

## The audit trail is the issue, not the use

The application never sees the access. That is the point of the pattern, and it means the usual
"who read this file?" audit is unavailable at the application layer.

What the application *can* record is every issuance: who asked, what was decided, which resource,
which permission, what lifetime. That log is the one that answers the question that matters after
an incident — where did this key come from? — and it is cheap, because issuance is exactly where
the application still is.

Storage-side access logs complete the picture from the other end, and correlating the two is the
closest thing to a full trail this pattern permits.

## Validate afterwards what you could not validate during

Nothing checks the upload while it happens. The file may be the wrong type, too large, corrupt,
or malicious, and the application finds out only when something reads it.

The usual shape is an event: the store raises a notification on write, and a background process
validates, scans, transcodes or rejects. This is where valet key meets the messaging patterns —
the upload completes, an event is published, and a consumer does what the application would have
done had it been in the path. Treating uploaded content as untrusted until that has happened is
the discipline the pattern makes necessary.

## What this repository's model leaves out

The key is a registry entry rather than a signature, which inverts the revocation story: `Revoke`
works here and would not in most real implementations. There is no network and no separate
storage service, so nothing demonstrates the client actually bypassing the application — the whole
benefit is described rather than shown. There is no issuance log, no renewal for long transfers,
no clock skew, and no post-upload validation. Each of those is a section above.
