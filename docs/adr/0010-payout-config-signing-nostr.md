# ADR 0010: Payout config signing with Nostr keys pinned by NIP-05

| Field | Value |
|---|---|
| Status | Proposed |
| Date | 2026-09-27 |
| Overrides | None |
| Sources | `project.md` §2 (P4); D11, D16 |

> Stub. The decision below is summarized from `docs/instructions/project.md`, which is authoritative until this ADR is written out and accepted. Structure: [`0000-template.md`](0000-template.md).

## Context

_To be written._

## Decision

Changing a payout destination requires passkey step-up, a 72 h delay, notices on every verified channel, and a signature from the developer's Nostr key, pinned outside the platform database via NIP-05 on the developer's domain. Checkout verifies the signature against the pin before creating a payment request. Hash commitments of signed configs are published to Nostr relays; the xpub itself is never published.

## Options considered

_To be written. `project.md` records some rejected options inline._

## Consequences

_To be written._

## Open items

Pending D16: `docs/instructions/payments.md` (config format, developers without a domain, relay commitments, developer-side monitoring).
