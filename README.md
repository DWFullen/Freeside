# Freeside

A DRM-free marketplace for games and other digital goods, paid in bitcoin. Buyers pay developers directly: no card network, processor or acquirer sits in a sale, so no payment intermediary decides which lawful games can be sold. Operated by Sprawl (legal entity name pending, D19).

**Status:** Phase 0 in progress ([`docs/plans/phase-0.md`](docs/plans/phase-0.md)). The solution skeleton builds and tests; there are no product features yet.

## Where things are

| Path | Contents | Precedence |
|---|---|---|
| [`docs/adr/`](docs/adr/) | Architecture decision records, including any approved override of an `AGENTS.md` §2 invariant | 1 (highest) |
| [`docs/instructions/project.md`](docs/instructions/project.md) | Freeside product, payments, legal and platform decisions | 2 |
| `docs/instructions/<topic>.md` | Deeper topic files. `payments.md` is pending (D16) | 3 |
| [`AGENTS.md`](AGENTS.md) | Baseline engineering rules for Bitcoin-accepting digital-goods apps | 4 |
| [`CLAUDE.md`](CLAUDE.md) | Loads `AGENTS.md` and `project.md` into every Claude Code session | — |
| [`tools/claude-cloud/setup.sh`](tools/claude-cloud/setup.sh) | Reviewed copy of the claude.ai/code cloud environment setup script | — |
| [`docs/plans/`](docs/plans/) | Phase plans and the list of placeholders still to be supplied | — |
| `src/` | `Freeside.Core` (domain, no infrastructure dependencies), `Freeside.Web` (ASP.NET Core, Razor Pages), `Freeside.Worker` (background jobs) | — |
| `tests/` | xUnit v3 test projects, one per `src/` project | — |

More specific layers override less specific ones. **Nothing overrides an `AGENTS.md` §2 or `project.md` §2 invariant except an accepted ADR that states the risk accepted.**

## Stack

| Concern | Choice | Source |
|---|---|---|
| App | ASP.NET Core on .NET 10 (LTS), Razor Pages, EF Core | `project.md` §8. .NET 10 pinned in [`global.json`](global.json) |
| Data | PostgreSQL, `bigint` money columns, append-only ledger | `project.md` §8, `AGENTS.md` §2 |
| Payments | Strike (default primary rail), BTCPay Server (xpub backups, platform fallback store), LUD-21 Lightning Address | `project.md` §4 |
| Hosting | Azure Container Apps (`web`, `worker`), Cloudflare R2 for artifacts | `project.md` §8, ADR 0004 |
| IaC / CI | Terraform with Azure Verified Modules, GitHub Actions with OIDC | `project.md` §8, [ADR 0012](docs/adr/0012-terraform-avm-state-backend.md) |
| Networks | regtest (local/CI) → signet (dev/UAT) → mainnet (prod only) | `AGENTS.md` §4.2, `project.md` §8.1 |

## Build and test

Needs the .NET SDK pinned in [`global.json`](global.json) (10.0.401, any later 10.0.4xx patch). These are the exact commands CI runs ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)):

```sh
dotnet restore --locked-mode
dotnet build --no-restore -c Release
dotnet test --no-build -c Release
```

- **Warnings are errors.** That includes the analyzers and the `.editorconfig` code-style and naming rules ([`Directory.Build.props`](Directory.Build.props)).
- **Tests run on Microsoft.Testing.Platform** (xUnit v3), selected in `global.json`.
- **Package versions live only in [`Directory.Packages.props`](Directory.Packages.props).** Every project commits a `packages.lock.json`. After adding or changing a package, refresh the lock files with `dotnet restore --force-evaluate` and commit them. `--locked-mode` fails if they are stale.

### Run locally

```sh
dotnet run --project src/Freeside.Web      # http://localhost:5080, health check at /healthz
dotnet run --project src/Freeside.Worker
```

The launch profiles set `Bitcoin__Network=regtest`.

**`Bitcoin:Network` has no default** (`AGENTS.md` §2, invariant 4). Both hosts refuse to start if it is missing, or if it isn't exactly one of `regtest`, `signet`, `testnet4`, `mainnet`. They also refuse to start if a fake service (a stand-in listed in [`docs/plans/placeholders.md`](docs/plans/placeholders.md)) is registered on any network other than `regtest`.

### Repo checks

The `repo-checks` CI job runs these. They need only bash (and shellcheck):

```sh
tools/ci/check-action-pins.sh     # every workflow `uses:` is pinned to a full commit SHA with a `# vX.Y.Z` comment
tools/ci/check-placeholders.sh    # PLACEHOLDER(<id>) markers in code match docs/plans/placeholders.md
tools/ci/tests/run.sh             # fixture tests for both scripts
shellcheck tools/ci/*.sh tools/ci/tests/*.sh tools/claude-cloud/*.sh
```

CodeQL (C# and GitHub Actions) and dependency review run as their own workflows on every pull request.

### Dependabot PRs and lock files

[Dependabot](.github/dependabot.yml) proposes weekly updates for NuGet packages, GitHub Actions and the SDK in `global.json`. It waits 7 days after each release; security updates skip the wait.

A NuGet update can leave `packages.lock.json` stale in projects that reference the updated project ([dependabot-core #13950](https://github.com/dependabot/dependabot-core/issues/13950)). CI's `--locked-mode` restore then fails with `NU1004`. To fix it, check out the Dependabot branch, then run:

```sh
dotnet restore --force-evaluate
```

Commit the changed lock files to the same branch.

## Working rules

- `main` is protected. Every change lands through a PR with green CI.
- Claude Code cloud sessions (claude.ai/code) push to their own branches and open PRs; they never push to `main`.
- A change to architecture, security posture, custody or funds flow, data retention, product behavior or cost needs an ADR first ([`docs/adr/`](docs/adr/)).
- No secrets in the repo, in the cloud environment's variables, or in plaintext CI variables. Local, CI and cloud sessions use **regtest only**; mainnet credentials exist only in production (`AGENTS.md` §2, invariants 4 and 8).

## Next: Phase 0

IaC (Azure + R2), CI/CD, regtest harness (BTCPay compose), auth (email + passkey), ledger schema, `IPaymentRail`. See `project.md` §10. The PR-by-PR plan is [`docs/plans/phase-0.md`](docs/plans/phase-0.md); what is still stubbed out, and what you must supply to replace it, is in [`docs/plans/placeholders.md`](docs/plans/placeholders.md).

## License

None yet. Without a `LICENSE` file, all rights are reserved.
