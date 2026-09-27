# ADR 0004: Artifact delivery on Cloudflare R2

| Field | Value |
|---|---|
| Status | Proposed |
| Date | 2026-09-27 |
| Overrides | None. Applies the non-Azure exception in `AGENTS.md` §5.3 and §6.3 |
| Sources | `project.md` §8; `AGENTS.md` §5.3, §6.3 |

> Stub. The decision below is summarized from `docs/instructions/project.md`, which is authoritative until this ADR is written out and accepted. Structure: [`0000-template.md`](0000-template.md).

## Context

_To be written._

## Decision

Store and deliver build artifacts from Cloudflare R2, using presigned GET URLs minted after the entitlement check. Developer uploads land in an Azure Blob quarantine container, pass Defender for Storage malware scanning and manifest verification, and are then promoted to R2 at a content-addressed path. Azure and Cloudflare resources are managed in one Terraform/OpenTofu deployment.

## Options considered

_To be written. `project.md` records some rejected options inline._

## Consequences

_To be written._

## Open items

Verify Cloudflare's terms on lawful adult content (`project.md` §0.3, secondary source only so far).
