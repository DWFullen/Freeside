# ADR 0011: Account and authenticator model, and the permanent Lightning Login host

| Field | Value |
|---|---|
| Status | Proposed |
| Date | 2026-09-27 |
| Overrides | None |
| Sources | `project.md` §3.3 |

> Stub. The decision below is summarized from `docs/instructions/project.md`, which is authoritative until this ADR is written out and accepted. Structure: [`0000-template.md`](0000-template.md).

## Context

_To be written._

## Decision

An account has one or more authenticators: passkey, email, Nostr, Lightning Login. Developers must enroll a passkey and a Nostr key. The Lightning Login callback host is fixed permanently, because wallets derive the login key from the domain (LUD-05).

## Options considered

_To be written. `project.md` records some rejected options inline._

## Consequences

_To be written._

## Open items

Blocked on D18: register the domains and choose the canonical host (default `freeside.games`).
