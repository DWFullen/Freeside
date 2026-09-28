# Architecture decision records

An ADR records a decision that changes architecture, security posture, custody or funds flow, data retention, product behavior or cost (`AGENTS.md` §1).

Rules:

- Number sequentially; never renumber or delete. A replaced ADR is marked **Superseded by NNNN**.
- An ADR that overrides an `AGENTS.md` §2 or `project.md` §2 invariant must fill in **Risk accepted**. No other document can override an invariant.
- Start from [`0000-template.md`](0000-template.md).
- Status moves Proposed → Accepted (or Rejected) in a PR.

| ADR | Title | Status | Overrides |
|---|---|---|---|
| [0001](0001-direct-pay-marketplace.md) | Direct-pay marketplace model and its tax consequences | Proposed | — |
| [0002](0002-strike-default-primary-rail.md) | Strike as the developers' default primary rail | Proposed | invariant 12 |
| [0003](0003-us-only-sales.md) | US-only sales | Proposed | — |
| [0004](0004-artifact-delivery-cloudflare-r2.md) | Artifact delivery on Cloudflare R2 | Proposed | — |
| [0005](0005-fee-collection.md) | Fee collection: ACH mandate, prepaid pool, grace allowance | Proposed | — |
| [0006](0006-platform-treasury-strike-business.md) | Platform treasury on Strike Business | Proposed | invariant 12 |
| [0007](0007-thesis-and-content-standards.md) | Thesis and content standards | Proposed | — |
| [0008](0008-payout-rail-priority-and-failover.md) | Payout rail priority, required self-custodial backups and automatic failover | Proposed | — |
| [0009](0009-no-adult-titles-at-launch.md) | No adult titles at launch: exit criteria and review date | Proposed | — |
| [0010](0010-payout-config-signing-nostr.md) | Payout config signing with Nostr keys pinned by NIP-05 | Proposed | — |
| [0011](0011-account-authenticator-model.md) | Account and authenticator model, and the permanent Lightning Login host | Proposed | — |
| [0012](0012-terraform-avm-state-backend.md) | Terraform with Azure Verified Modules, the state backend, and secrets kept out of state | Accepted | — |
