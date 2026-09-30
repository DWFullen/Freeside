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
| `src/` | `Freeside.Core` (domain: money types, ledger entries; no infrastructure dependencies), `Freeside.Infrastructure` (EF Core on PostgreSQL, migrations), `Freeside.Web` (ASP.NET Core, Razor Pages), `Freeside.Worker` (background jobs) | — |
| `tests/` | xUnit v3 test projects, one per `src/` project (`Freeside.Infrastructure.Tests` needs Docker), plus `Freeside.Regtest.Tests` (needs the regtest stack) | — |
| [`tools/regtest/`](tools/regtest/) | Docker compose regtest stack: BTCPay Server, bitcoind, NBXplorer, Postgres, Mailpit | — |

More specific layers override less specific ones. **Nothing overrides an `AGENTS.md` §2 or `project.md` §2 invariant except an accepted ADR that states the risk accepted.**

## Stack

| Concern | Choice | Source |
|---|---|---|
| App | ASP.NET Core on .NET 10 (LTS), Razor Pages, EF Core | `project.md` §8. .NET 10 pinned in [`global.json`](global.json) |
| Data | PostgreSQL 18, `bigint` money columns, append-only ledger | `project.md` §8, `AGENTS.md` §2, [#13](https://github.com/DWFullen/Freeside/issues/13) |
| Payments | Strike (default primary rail), BTCPay Server (xpub backups, platform fallback store), LUD-21 Lightning Address | `project.md` §4 |
| Hosting | Azure Container Apps (`web`, `worker`), Cloudflare R2 for artifacts | `project.md` §8, ADR 0004 |
| IaC / CI | Terraform with Azure Verified Modules, GitHub Actions with OIDC | `project.md` §8, [ADR 0012](docs/adr/0012-terraform-avm-state-backend.md) |
| Networks | regtest (local/CI) → signet (dev/UAT) → mainnet (prod only) | `AGENTS.md` §4.2, `project.md` §8.1 |

## Build and test

Needs the .NET SDK pinned in [`global.json`](global.json) (10.0.401, any later 10.0.4xx patch). These are the exact commands CI runs ([`.github/workflows/ci.yml`](.github/workflows/ci.yml)):

```sh
dotnet restore --locked-mode
dotnet build --no-restore -c Release
dotnet tool restore
dotnet ef migrations has-pending-model-changes --project src/Freeside.Infrastructure --configuration Release --no-build
dotnet test --no-build -c Release --filter-not-trait "Category=Regtest"
```

- **The filter** leaves out the tests that need the regtest stack (below).
- **`Freeside.Infrastructure.Tests` needs Docker.** Testcontainers starts the same Postgres image as the regtest stack. Without Docker those tests fail rather than skip.
- **`dotnet tool restore`** installs the pinned `dotnet-ef` from [`.config/dotnet-tools.json`](.config/dotnet-tools.json).

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

### Money and the ledger

- **No binary floating point in `Freeside.Core` or `Freeside.Infrastructure`** (`AGENTS.md` §2, invariant 5). Amounts use `Sats`, `MilliSats` and `Money` (fiat minor units), all `long`. Exchange rates are decimal strings. Conversions use exact integer arithmetic and a named `RoundingRule`.
- **Two checks enforce it:**
  - the banned-API analyzer ([`src/BannedSymbols.txt`](src/BannedSymbols.txt)) rejects floating-point members and APIs that return doubles, at build time;
  - [`FloatingPointBanTests`](tests/Freeside.Core.Tests/FloatingPointBanTests.cs) catches `double`/`float` declarations, casts and literals like `19.99`, which the analyzer can't see.
- **The ledger (`ledger_entries`) is append-only in the database** (invariant 7):
  - the app role holds only `SELECT` and `INSERT`;
  - triggers reject `UPDATE`, `DELETE` and `TRUNCATE`, even for the table owner.
- **Amounts are never negative.** The entry type says whether an entry is a credit or a debit, and balances are always derived from the entries.

### Database and migrations

- **Roles:** before the first migration, a database admin runs [`bootstrap-roles.sql`](src/Freeside.Infrastructure/Database/bootstrap-roles.sql) once. It creates the group roles `freeside_migrator` (applies migrations, owns the tables) and `freeside_app` (what the hosts run as). Login roles are made members of these groups.
- **Adding a migration:** `dotnet ef migrations add <Name> --project src/Freeside.Infrastructure --output-dir Migrations`. The hosts never apply migrations at startup. Apply them with `dotnet ef database update --project src/Freeside.Infrastructure --connection "<migrator connection string>"`, or with the migration bundle job (PR 10).
- **Authentication** (`Database:Authentication`, no default):
  - `Password` is allowed only when `Bitcoin:Network` is `regtest`.
  - Everywhere else it's `EntraManagedIdentity`: a managed identity's token instead of a password, over TLS.
  - Startup refuses anything else.
- **Not wired into Web or Worker yet.** PR 5 is the first code that uses the database, and connects it there.

### Regtest stack

Needs Docker with Compose v2. [`tools/regtest/compose.yml`](tools/regtest/compose.yml) runs BTCPay Server on **regtest** with bitcoind, NBXplorer and BTCPay's Postgres, plus the app's own Postgres and Mailpit. Every credential in it is a throwaway that works only on this local stack.

```sh
make regtest-up      # start, mine 101 blocks, wait until BTCPay is synchronized
make regtest-test    # smoke test against the running stack
make regtest-down    # stop, and delete every volume
tools/regtest/bitcoin-cli.sh -generate 1    # mine a block; any bitcoin-cli command works
```

| Service | Address (127.0.0.1 only) | Credentials |
|---|---|---|
| BTCPay Server | http://127.0.0.1:49392 | `admin@freeside.test` / `regtest-throwaway`, created by the first smoke test run |
| bitcoind RPC | 127.0.0.1:43782 | `freeside` / `regtest-throwaway`; wallet `default` |
| App Postgres 18 | 127.0.0.1:55432 | database and user `freeside`, password `regtest-throwaway` |
| Mailpit | SMTP 127.0.0.1:1025, UI http://127.0.0.1:8025 | Accepts any |

- **Smoke test** (`tests/Freeside.Regtest.Tests`, trait `Category=Regtest`):
  - It creates a store with a throwaway watch-only BIP84 xpub. BTCPay never holds a private key.
  - It checks that invoices get fresh `bcrt1` addresses derived from that xpub, and that a paid invoice settles after one confirmation and not before.
  - It fails, rather than skips, when the stack is down. The CI `regtest` job runs it.
- **Versions:** images are pinned by tag and digest. BTCPay, NBXplorer, bitcoind and BTCPay's Postgres match what the operator's Start9 node runs ([ADR 0013](docs/adr/0013-btcpay-on-operator-start9-node.md)): the StartOS [BTCPay](https://github.com/Start9Labs/btcpayserver-startos) and [Bitcoin Core](https://github.com/Start9Labs/bitcoin-core-startos) packages. When the node's package is updated, re-pin those four in `compose.yml` by hand.
- **Cloud sessions:** in a claude.ai/code session, the SessionStart hook in [`.claude/settings.json`](.claude/settings.json) starts dockerd, and the environment setup script pre-pulls the images.

### Repo checks

The `repo-checks` CI job runs these. They need only bash (and shellcheck):

```sh
tools/ci/check-action-pins.sh     # every workflow `uses:` is pinned to a full commit SHA with a `# vX.Y.Z` comment
tools/ci/check-image-pins.sh      # every compose `image:` is pinned as name:tag@sha256:<digest>
tools/ci/check-placeholders.sh    # PLACEHOLDER(<id>) markers in code match docs/plans/placeholders.md
tools/ci/tests/run.sh             # fixture tests for the three scripts
shellcheck tools/ci/*.sh tools/ci/tests/*.sh tools/claude-cloud/*.sh tools/regtest/*.sh
```

CodeQL (C# and GitHub Actions) and dependency review run as their own workflows on every pull request.

### Dependabot PRs and lock files

[Dependabot](.github/dependabot.yml) proposes weekly updates for NuGet packages, GitHub Actions, the SDK in `global.json`, and the app Postgres and Mailpit images in the regtest stack. It waits 7 days after each release; security updates skip the wait. It leaves the BTCPay stack's images alone: they follow the Start9 node (above).

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
