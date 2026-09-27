# ADR 0008: Payout rail priority, required self-custodial backups and automatic failover

| Field | Value |
|---|---|
| Status | Proposed |
| Date | 2026-09-27 |
| Overrides | None |
| Sources | `project.md` §4.2, §2 (P6, P8, P9); D14 |

> Stub. The decision below is summarized from `docs/instructions/project.md`, which is authoritative until this ADR is written out and accepted. Structure: [`0000-template.md`](0000-template.md).

## Context

_To be written._

## Decision

Each developer chooses rail priority (Strike by default). An on-chain xpub/descriptor backup is required before a storefront goes live; a Lightning Address with LUD-21 is an optional backup. A single static on-chain address is never accepted. A platform-wide circuit breaker on the Strike API, plus per-developer triggers, fail checkout over to backups automatically.

## Options considered

_To be written. `project.md` records some rejected options inline._

## Consequences

_To be written._
