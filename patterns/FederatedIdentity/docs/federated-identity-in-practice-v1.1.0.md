# Federated Identity in practice

Supporting material for [the Federated Identity pattern](../README.md).

## Offline validation is the benefit and the reason revocation is hard

An application that validates a token locally — signature, issuer, expiry — never calls the provider
during a request. That is why federated identity adds no per-request latency and why a provider
outage does not stop work already in progress.

It is also exactly why **revocation does not work the way people expect**. Nobody is asking whether
the token is still good, so a user who is disabled at 09:00 keeps working until their token expires.
If that lifetime is eight hours, they have eight hours.

The available answers, none of them free:

* **Short lifetimes with refresh tokens.** Access tokens live minutes; refresh happens against the
  provider, where revocation *is* checked. This is the standard answer and it is a compromise: the
  window is the access token's lifetime.
* **Introspection per request.** Ask the provider each time. Correct, and it discards the benefit
  above entirely.
* **A revocation list the application checks.** Faster than introspection, and now the application
  holds state that must be distributed and current — which is its own distributed-systems problem.

The decision worth making explicitly is *how long a disabled user may continue*. Every architecture
has an answer; most have it by accident.

## Validate everything, including the algorithm

Token validation has a specific and well-documented history of near-misses, and all of them look
like working code.

* **The algorithm field.** A token that says `alg: none` and is accepted because the library obeyed
  it. Pin the expected algorithm rather than trusting the token to name it.
* **The issuer.** A correctly signed token from a *different* tenant or provider. Signature valid,
  identity wrong.
* **The audience.** A token minted for application A, replayed against application B. Both trust the
  same provider, so the signature checks out.
* **Expiry, and clock skew.** Checked against whose clock, with what tolerance? A few minutes of
  leeway is normal; hours is a hole.
* **Key rotation.** An application pinning one key breaks when the provider rotates. Fetch from the
  key endpoint and cache with a refresh — the same bounded-refresh shape as an external
  configuration store.

The practical instruction is to use a maintained library and configure it strictly. Every item above
is a default somebody has got wrong, and none of them fails visibly in testing.

## A token is authentication, not authorisation

The token says *this is Ada*. It does not say Ada may cancel this order, and the temptation to treat
claims in the token as permissions is strong because they are right there.

Two problems with that. Claims are as stale as the token, so a permission revoked at 09:00 persists
until expiry — the same problem as revocation, applied to something that changes more often than
account status. And permissions are domain knowledge: whether Ada may cancel *this* order depends on
the order, which the provider knows nothing about.

The workable split is that the provider asserts identity and coarse group membership; the
application decides what that means. Roles in a token are useful for gross routing — is this person
an administrator at all — and should not be the final word on any specific action.

## Trust is transitive, and that is the actual security decision

Delegating authentication means whoever controls the provider controls access to everything trusting
it. That is not a criticism — a specialist provider is almost always more secure than an application's
own password table — but it makes the choice of provider a security decision rather than a
convenience one.

It follows that:

* **The provider's compromise is your compromise.** Its security posture, its incident history and
  its access controls matter as much as your own.
* **Federating with a partner's provider federates their offboarding process too.** An employee who
  leaves that company keeps access until their identity is disabled *there*.
* **Multiple trusted issuers multiply this.** Each is a way in, and accepting "any Microsoft
  account" is a much larger set than intended if the audience and tenant are not checked.

## Plan for the provider being unavailable

Sign-in stops. Sessions already established usually continue, because validation is local — which is
the one place this pattern degrades gracefully by construction.

Worth deciding in advance, though, because the answers differ by system: whether an expiring session
can be extended during an outage, whether an emergency local account exists for operators, and what
staff are told when nobody can sign in and the cause is somebody else's incident.

A break-glass path is the uncomfortable one. It reintroduces exactly the local credential the pattern
removed — so it needs to be tightly scoped, heavily audited, tested, and genuinely rare. Not having
one is also a defensible choice, provided it is a choice.

## What this repository's model leaves out

The signature is a string comparison with a **symmetric** key, so the pattern's central claim — a
verification key checks tokens and cannot mint them — is approximated rather than demonstrated. There
is no network and no protocol: no redirects, no authorisation code exchange, no discovery document,
no key rotation. There is no credential hygiene either: enrolled passwords are stored in plain
text and compared directly, where a real provider holds salted, stretched hashes and nothing
recoverable. There is no refresh token, so the lifetime trade in the first section is described
and never exercised. There is no revocation, no audience claim, and no clock skew. And there is no
multi-factor, conditional access or session management — which is most of what a real provider is
bought for.
