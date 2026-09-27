# ADR 0006: Platform treasury on Strike Business

| Field | Value |
|---|---|
| Status | Proposed |
| Date | 2026-09-27 |
| Overrides | `AGENTS.md` §2 invariant 12 (custodial third party in the funds path), for the platform's own funds only |
| Sources | `project.md` §4.6; D6 |

> Stub. The decision below is summarized from `docs/instructions/project.md`, which is authoritative until this ADR is written out and accepted. Structure: [`0000-template.md`](0000-template.md).

## Context

_To be written._

## Decision

The platform's own money (listing fees, pool top-ups, ACH fee settlements, operating expenses) is held in Strike Business. Developer sale proceeds never touch it (P1). BTC is swept weekly, or above a threshold, to platform cold storage. The platform's BTCPay store stays live as the fallback receiving path.

## Risk accepted

_Required: this ADR overrides an invariant. To be written._

## Options considered

_To be written. `project.md` records some rejected options inline._

## Consequences

_To be written._

## Open items

Blocked on D8 (as ADR 0002), including whether two Strike Business accounts (Rails and Treasury) are allowed.
