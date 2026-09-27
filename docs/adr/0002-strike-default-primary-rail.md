# ADR 0002: Strike as the developers' default primary rail

| Field | Value |
|---|---|
| Status | Proposed |
| Date | 2026-09-27 |
| Overrides | `AGENTS.md` §2 invariant 12 (custodial third party in the funds path), for the developer's funds path only |
| Sources | `project.md` §0.3, §4.2, §2 (P8, P9); D14 |

> Stub. The decision below is summarized from `docs/instructions/project.md`, which is authoritative until this ADR is written out and accepted. Structure: [`0000-template.md`](0000-template.md).

## Context

_To be written._

## Decision

A developer's integrated Strike account is the default primary payout rail. This is allowed only because P9 requires every storefront to have a self-custodial backup and checkout fails over to it automatically. A developer may make a self-custodial rail primary instead (D14).

## Risk accepted

_Required: this ADR overrides an invariant. To be written._

## Options considered

_To be written. `project.md` records some rejected options inline._

## Consequences

_To be written._

## Open items

Blocked on D8: Strike Business terms (content restrictions, marketplace use, directed payments, freezes), on-chain support in directed invoices, receive-only key scoping, test environment.
