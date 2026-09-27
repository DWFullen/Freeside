# ADR 0005: Fee collection: ACH mandate, prepaid pool, grace allowance

| Field | Value |
|---|---|
| Status | Proposed |
| Date | 2026-09-27 |
| Overrides | None |
| Sources | `project.md` §4.4, §2 (P2, P7); D4, D13 |

> Stub. The decision below is summarized from `docs/instructions/project.md`, which is authoritative until this ADR is written out and accepted. Structure: [`0000-template.md`](0000-template.md).

## Context

_To be written._

## Decision

Fees accrue in USD in each developer's fee account and are settled from, in order: the prepaid BTC pool, an ACH debit mandate (monthly or at $500 accrued), and a $50 grace allowance for developers without a mandate. The grace allowance is the only unsecured credit. An empty pool never pauses a developer who has a valid mandate (P7).

## Options considered

_To be written. `project.md` records some rejected options inline._

## Consequences

_To be written._

## Open items

Blocked on D12: ACH origination provider (tokenized linking, Strike account support, crypto-adjacent and adult-content acceptance, Nacha fraud-monitoring coverage).
