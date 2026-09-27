# Freeside

A DRM-free marketplace for games and other digital goods, paid in bitcoin. Buyers pay developers directly: no card network, processor or acquirer sits in a sale, so no payment intermediary decides which lawful games can be sold. Operated by Sprawl (legal entity name pending, D19).

**Status:** pre-Phase 0. Documentation and decisions only; no application code yet.

## Where things are

| Path | Contents | Precedence |
|---|---|---|
| [`docs/adr/`](docs/adr/) | Architecture decision records, including any approved override of an `AGENTS.md` §2 invariant | 1 (highest) |
| [`docs/instructions/project.md`](docs/instructions/project.md) | Freeside product, payments, legal and platform decisions | 2 |
| `docs/instructions/<topic>.md` | Deeper topic files. `payments.md` is pending (D16) | 3 |
| [`AGENTS.md`](AGENTS.md) | Baseline engineering rules for Bitcoin-accepting digital-goods apps | 4 |
| [`CLAUDE.md`](CLAUDE.md) | Loads `AGENTS.md` and `project.md` into every Claude Code session | — |
| [`tools/claude-cloud/setup.sh`](tools/claude-cloud/setup.sh) | Reviewed copy of the claude.ai/code cloud environment setup script | — |

More specific layers override less specific ones. **Nothing overrides an `AGENTS.md` §2 or `project.md` §2 invariant except an accepted ADR that states the risk accepted.**

## Stack

| Concern | Choice | Source |
|---|---|---|
| App | ASP.NET Core on .NET 10 (LTS), Razor Pages, EF Core | `project.md` §8. .NET 10 pinned in [`global.json`](global.json) |
| Data | PostgreSQL, `bigint` money columns, append-only ledger | `project.md` §8, `AGENTS.md` §2 |
| Payments | Strike (default primary rail), BTCPay Server (xpub backups, platform fallback store), LUD-21 Lightning Address | `project.md` §4 |
| Hosting | Azure Container Apps (`web`, `worker`), Cloudflare R2 for artifacts | `project.md` §8, ADR 0004 |
| IaC / CI | Terraform/OpenTofu, GitHub Actions with OIDC | `project.md` §8 |
| Networks | regtest (local/CI) → signet (dev/UAT) → mainnet (prod only) | `AGENTS.md` §4.2, `project.md` §8.1 |

## Working rules

- `main` is protected. Every change lands through a PR with green CI.
- Claude Code cloud sessions (claude.ai/code) push to their own branches and open PRs; they never push to `main`.
- A change to architecture, security posture, custody or funds flow, data retention, product behavior or cost needs an ADR first ([`docs/adr/`](docs/adr/)).
- No secrets in the repo, in the cloud environment's variables, or in plaintext CI variables. Local, CI and cloud sessions use **regtest only**; mainnet credentials exist only in production (`AGENTS.md` §2, invariants 4 and 8).

## Next: Phase 0

IaC (Azure + R2), CI/CD, regtest harness (BTCPay compose), auth (email + passkey), ledger schema, `IPaymentRail`. See `project.md` §10.

## License

None yet. Without a `LICENSE` file, all rights are reserved.
