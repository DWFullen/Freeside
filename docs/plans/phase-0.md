# Phase 0 plan

| Field | Value |
|---|---|
| Scope | `project.md` §10, Phase 0: IaC (Azure + R2), CI/CD, regtest harness, auth (email + passkey), ledger schema, `IPaymentRail` |
| Status | Approved 2026-09-27. PR 0 and PR 1 merged in #1 (2026-09-28), PR 2 in #131 (2026-09-29). PR 3 in progress |
| Related | [ADR 0012](../adr/0012-terraform-avm-state-backend.md), [ADR 0013](../adr/0013-btcpay-on-operator-start9-node.md), [`placeholders.md`](placeholders.md) |
| Tracking | GitHub issues: [#22 Phase 0](https://github.com/DWFullen/Freeside/issues/22), [#2 Open decisions](https://github.com/DWFullen/Freeside/issues/2), [#23 ADRs](https://github.com/DWFullen/Freeside/issues/23), [#24 Phase 1](https://github.com/DWFullen/Freeside/issues/24). Each PR below has its own issue |

Every PR builds and passes its tests on its own. PRs land in order unless the dependency column allows otherwise.

## Rules that apply to every PR

- No secret values, cloud credentials or mainnet anything in the repo, in Terraform state, or in a Claude Code session. Nothing is applied to Azure or Cloudflare from a session.
- **Placeholder policy.** Build everything that doesn't need an account or key we don't have yet.
  - Each external service sits behind an interface in `Freeside.Core`. A `Fake` implementation serves local and CI; a real adapter is selected by configuration.
  - Fakes are allowed only when `Bitcoin:Network=regtest`. Any other network with a fake registered fails at startup (`AGENTS.md` §2, invariants 4 and 15).
  - Secrets are declared by name only: Key Vault secret names, empty entries in `.env.example`, `dotnet user-secrets` locally. ADR 0012 explains why a `sensitive` Terraform variable is not enough.
  - Every stand-in is marked `PLACEHOLDER(<id>)` in code and listed in [`placeholders.md`](placeholders.md).
- BTCPay is not a placeholder for dev and test: it runs on regtest in docker compose with throwaway keys.
- Email goes through `IEmailSender`, with Mailpit in compose for local and CI.
- Terraform follows ADR 0012.
- If a PR needs a tool the cloud environment lacks, it updates `tools/claude-cloud/setup.sh`, and the change is re-pasted into the environment settings.
- Problems outside a PR's scope are flagged in the PR, not fixed silently.

## Decisions that refine the original request

| # | Decision | Recorded in |
|---|---|---|
| R1 | Terraform AVM modules publish no `waf-aligned` examples (*Observed*). Each resource's reference is the module's most complete security example, the matching Bicep AVM `waf-aligned` test, and the Azure WAF service guide. Deviations are listed per environment in `infra/README.md` | ADR 0012 |
| R2 | Two identities per environment: read-only `plan` (pull requests, `main`) and `apply` (GitHub Environment only). PR code never holds write credentials | ADR 0012 |
| R3 | `apply` re-plans and aborts if the plan hash differs from the reviewed plan. Plan files never leave the runner | ADR 0012 |
| R4 | The repo is public, so logs show only `address → action` summaries. No plans run for fork PRs | ADR 0012 |
| R5 | An ops Key Vault (CI-readable, public endpoint, RBAC per secret) plus a private app Key Vault per environment | ADR 0012 |
| R6 | Front Door Standard in dev (custom WAF rules, public origin, `X-Azure-FDID` check). Premium with managed rules and Private Link to Container Apps in uat and prod | `infra/README.md` (PR 8c) |
| R7 | The app renders checkout itself because of the unified multi-rail QR, so BTCPay's checkout page is never public (`AGENTS.md` §6.2 option 2). BTCPay is reached only through a Tor onion service, via a Tor client sidecar ([#132](https://github.com/DWFullen/Freeside/issues/132)) | ADR 0013 |
| R8 | PR 8 is split into 8a–8d, and PR 10 is added for container images and deployment | This file |
| R9 | BTCPay runs on the operator's Start9 node, not in Azure. No BTCPay VM, Bastion, subnet or resource group. Signet for dev and UAT runs on a separate machine the operator controls. Every node detail is a placeholder until the node is set up | [ADR 0013](../adr/0013-btcpay-on-operator-start9-node.md) |

## PR sequence

| PR | Issue | Title | Needs Docker | Depends on | Status |
|---|---|---|---|---|---|
| 0 | [#28](https://github.com/DWFullen/Freeside/issues/28) | Docs: ADR 0012, `project.md` §8, README, plans | no | — | Done ([#1](https://github.com/DWFullen/Freeside/pull/1)) |
| 1 | [#29](https://github.com/DWFullen/Freeside/issues/29) | Solution skeleton + CI | no | 0 | Done ([#1](https://github.com/DWFullen/Freeside/pull/1)) |
| 2 | [#27](https://github.com/DWFullen/Freeside/issues/27) | Supply chain | no | 1 | Done ([#131](https://github.com/DWFullen/Freeside/pull/131)) |
| 3 | [#30](https://github.com/DWFullen/Freeside/issues/30) | Regtest harness + dockerd session hook | yes | 1 | In progress |
| 4 | [#31](https://github.com/DWFullen/Freeside/issues/31) | Data layer, money types, append-only ledger | yes (Testcontainers) | 1 | Planned |
| 5 | [#32](https://github.com/DWFullen/Freeside/issues/32) | Inbox/outbox + Postgres job queue | yes | 4 | Planned |
| 6 | [#33](https://github.com/DWFullen/Freeside/issues/33) | `IPaymentRail`, `RailSelector`, BTCPay adapter, fakes | yes | 3, 5 | Planned |
| 7 | [#34](https://github.com/DWFullen/Freeside/issues/34) | Auth: email login link + passkeys | yes | 5 | Planned |
| 8a | [#35](https://github.com/DWFullen/Freeside/issues/35) | Terraform bootstrap + Terraform CI + `setup.sh` | no | 2 | Planned |
| 8b | [#36](https://github.com/DWFullen/Freeside/issues/36) | Azure: network, observability, Key Vault, ACR, Postgres | no | 8a | Planned |
| 8c | [#37](https://github.com/DWFullen/Freeside/issues/37) | Azure: Container Apps, Front Door + WAF, quarantine storage + Defender | no | 8b | Planned |
| 8d | [#38](https://github.com/DWFullen/Freeside/issues/38) | BTCPay on the Start9 node: connection placeholders, connection check, runbook | no | 6, [#132](https://github.com/DWFullen/Freeside/issues/132) | Planned |
| 9 | [#39](https://github.com/DWFullen/Freeside/issues/39) | Cloudflare R2 | no | 8a | Planned |
| 10 | [#40](https://github.com/DWFullen/Freeside/issues/40) | Container images, provenance, deploy + migration job | no | 1, 4, 8c | Planned |

### PR 0: Docs
- **Scope:** ADR 0012 and its index row. `project.md` §8 IaC row, the README stack table and ADR 0004 now say "Terraform (ADR 0012)". This file and `placeholders.md`.
- **Tests:** none (docs only).
- **Invariants:** 8, 14 (recorded).
- **Outside the repo:** none.

### PR 1: Solution skeleton + CI
- **Projects:**
  - `src/Freeside.Core`: domain code. It may reference only `Microsoft.Extensions` options, configuration and DI abstractions; an architecture test enforces this.
  - `src/Freeside.Web`: ASP.NET Core Razor Pages with `/healthz`.
  - `src/Freeside.Worker`: `BackgroundService` host.
  - `tests/Freeside.{Core,Web,Worker}.Tests`: xUnit v3 on Microsoft.Testing.Platform. `global.json` opts `dotnet test` into it, because the .NET 10 SDK no longer runs xUnit v3 4.x through VSTest.
  - `Freeside.slnx` ties them together.
- **Build config:**
  - `Directory.Build.props`: nullable on, warnings as errors, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild`, deterministic builds, lock files.
  - `.editorconfig`: an explicit `IDE1006` severity, so the existing `_camelCase` naming rule is enforced at build time (without it the rule is silently skipped).
  - `Directory.Packages.props` for central package management. `packages.lock.json` per project.
  - `nuget.config` clears inherited sources and maps every package to nuget.org.
  - `global.json` pins the exact SDK with `rollForward: latestPatch`.
- **Network binding:** a required `Bitcoin:Network` (`regtest | signet | testnet4 | mainnet`) with no default. It is parsed strictly: exact lowercase only, and enum ordinals are rejected. It is validated at startup in both hosts.
- **Fake guard:** fakes are registered only through `AddFake<TService, TFake>()` and implement `IFakeService`. Startup fails when the network isn't regtest and any fake is registered.
- **CI:** `.github/workflows/ci.yml` runs build and test on `pull_request` and on push to `main`. Actions are pinned by full commit SHA; `permissions: contents: read`; `dotnet restore --locked-mode`.
- **Tests:**
  - Parser theory.
  - Web and Worker hosts fail to start when the network is missing or invalid, and start on regtest and signet.
  - `/healthz` returns 200.
  - Fake guard: fakes are allowed on regtest and rejected on signet.
  - Core dependency allowlist.
  - Mainnet is parse-tested only.
- **Invariants:** 4, 15.
- **Outside the repo:** rename `master` → `main`. Add a ruleset on `main`: require a PR, require the `build-test` check, block force pushes.

### PR 2: Supply chain
- **Dependabot** (`.github/dependabot.yml`), weekly, with a 7-day cooldown on new releases (security updates skip it):
  - `nuget`: minor and patch grouped, majors separate.
  - `github-actions`: grouped. Updates move the SHA and its `# vX.Y.Z` comment together.
  - `dotnet-sdk` (`global.json`): semver-major ignored, since a new SDK major is a decision.
  - `terraform` moves to PR 8a and `docker-compose` to PR 3. Those directories don't exist yet, and an empty directory makes Dependabot error.
- **Workflows:**
  - CodeQL: `csharp` with build-mode none, and `actions`, both with `security-extended` queries.
  - Dependency review, failing on moderate-or-worse vulnerabilities.

  Both need the repository to be public (#9).
- **Repo checks** (`repo-checks` job in `ci.yml`):
  - `tools/ci/check-action-pins.sh`: every `uses:` must be a 40-character commit SHA with a `# vX.Y.Z` comment, a local action, or a digest-pinned docker image.
  - `tools/ci/check-placeholders.sh`: `PLACEHOLDER(id)` in code must match `placeholders.md` both ways, using its "In code" column.
  - Fixture tests (`tools/ci/tests/run.sh`).
  - shellcheck.
- **setup.sh:** installs the exact SDK version pinned in `global.json`, falling back to the 10.0 channel. Installs shellcheck.
- **README:** repo checks, and how to fix lock files on Dependabot PRs (dependabot-core #13950: `dotnet restore --force-evaluate`).
- **Outside the repo** ([#42](https://github.com/DWFullen/Freeside/issues/42), [#41](https://github.com/DWFullen/Freeside/issues/41)):
  - Make the repository public (decision #9).
  - Enable Dependabot alerts and security updates, secret scanning with push protection, and private vulnerability reporting.
  - Keep code scanning "default setup" off.
  - Turn on the Actions policy "require actions pinned to a full-length commit SHA".
  - Add `repo-checks`, CodeQL and dependency review to the required checks on `main`.
  - Re-paste `setup.sh`.

### PR 3: Regtest harness
- **Compose file:** `tools/regtest/compose.yml` with every image pinned by tag and digest: bitcoind (`btcpayserver/bitcoin`), NBXplorer, BTCPay Server, Postgres for BTCPay, Postgres for the app, Mailpit. Ports bind to 127.0.0.1 only.
  - **Versions:** the BTCPay stack matches the operator's Start9 node (ADR 0013): the StartOS BTCPay package at `21ce7266` (BTCPay 2.4.4, NBXplorer 2.6.13, `btcpayserver/postgres` 18.6) and Bitcoin Core 31.1. It is re-pinned by hand when the node's package is updated; Dependabot ignores those images. The app's Postgres is 18 ([#13](https://github.com/DWFullen/Freeside/issues/13)).
  - NBXplorer's own regtest warm-up mining is off; `up.sh` mines the first 101 blocks, so the chain doesn't depend on which of the two runs first.
- **Commands:** `make regtest-up`, `make regtest-down` and `make regtest-test`. `tools/regtest/wait.sh` waits for BTCPay's `/api/v1/health` to report `synchronized`.
- **Smoke test** (`Category=Regtest`):
  1. Create the first user, an API key and a store with a throwaway watch-only BIP84 xpub.
  2. Create a BTC-denominated invoice (no rate source involved).
  3. Assert the address starts with `bcrt1`.
  4. Pay, mine one block, and wait for `Settled`.
  5. A second invoice gets a fresh address.
  - Also checked:
    - BTCPay's address preview matches the addresses derived locally from the xpub (`project.md` §4.2).
    - Before the block is mined, the payment is still `Processing`. The payment doesn't signal RBF, because BTCPay never settles a replaceable payment at 0-conf anyway.
  - The throwaway admin is opted in to Greenfield basic auth. Otherwise BTCPay allows basic auth only in an account's first five minutes, and a reused stack couldn't create an API key.
- **CI:** a `regtest` job. The unit job filters `Category!=Regtest`; the regtest project ignores exit code 8 (zero tests ran) for that filter, and `make regtest-test` requires at least one test to run.
- **Repo checks:** `tools/ci/check-image-pins.sh` fails on any compose image not pinned as `name:tag@sha256:<digest>`.
- **Session:**
  - `.claude/settings.json` gets a SessionStart hook (`tools/claude-cloud/session-start.sh`) that starts dockerd when `CLAUDE_CODE_REMOTE=true`.
  - `setup.sh` pre-pulls the pinned images.
  - Dependabot gets the `docker-compose` ecosystem.
- **Invariants:** 4, 8, 9.
- **Outside the repo:** re-paste `setup.sh`.

### PR 4: Data layer
- **Projects:** `src/Freeside.Infrastructure` (EF Core + Npgsql) and Testcontainers tests.
- **Money types** in Core: `Sats`, `MilliSats`, `Money` (minor units + ISO 4217), and `ExchangeRate` (decimal string, source, timestamp). Conversion uses integer rational arithmetic with named rounding rules. BannedApiAnalyzers bans `double`, `float` and `Half` in Core and Infrastructure.
- **Append-only ledger enforced in the database:** triggers reject `UPDATE`, `DELETE` and `TRUNCATE`, and the app role holds only `SELECT, INSERT`.
- **Migrations:** versioned EF Core migrations, never applied at app startup. CI runs `has-pending-model-changes`.
- **Postgres auth:** password auth only on regtest; Entra managed identity everywhere else.
- **Tests:** migrations apply to an empty database; ledger mutations are rejected; no floating-point columns; FsCheck property tests for conversion and rounding.
- **Invariants:** 5, 7.

### PR 5: Inbox/outbox + job queue
- **Tables:** `inbox_messages` (unique `(source, dedupe_key)`), `outbox_messages`, `jobs`.
- **Worker:** runners claim work with `FOR UPDATE SKIP LOCKED` and a lease. Retries back off exponentially, then go to a dead state.
- **Tests:** concurrent runners process each job exactly once; an expired lease is reclaimed; retry and dead-letter; duplicate inbox inserts are ignored; an outbox row rolls back with its transaction.
- **Invariants:** 6, 7.

### PR 6: Rails + BTCPay adapter
- **Core:** `IPaymentRail`, normalized invoice states and events, and a payment state machine per `AGENTS.md` §4.4. Backward transitions freeze and go to review.
- **RailSelector skeleton:** priority per layer, circuit breaker, and the $10 on-chain minimum.
- **Fakes:** `FakeStrikeRail` and `FakeFeeCollector` (`IFakeService`).
- **BTCPay adapter:**
  - Checks the HMAC over the raw body with a constant-time compare, and fails closed when no secret is configured.
  - Persists to the inbox and acknowledges. The worker re-fetches the invoice before every transition; transitions are idempotent on `(invoiceId, targetState)`.
  - Optional SOCKS5 proxy (`Btcpay:SocksProxy`) for the Tor sidecar (ADR 0013). Outside regtest, `http://` is accepted only for a `.onion` host. The base URL is never logged.
- **Startup network check:** preview address prefixes are compared with `Bitcoin:Network`. A mismatch refuses to start. An unreachable or unconfigured BTCPay (its placeholders not yet set, ADR 0013) only opens the rail's breaker.
- **Tests:**
  - Contract tests per `AGENTS.md` §7.2: valid, bad signature, missing secret, replay, out of order, backward transition, tampered body.
  - A regtest end-to-end test with a real webhook.
  - The proxy path, through a local SOCKS5 proxy added to the regtest stack. The real onion hop needs the Tor network, which CI and the session can't reach; the PR 8d connection check covers it.
- **Invariants:** 2, 3, 4, 6, 9, 15; P8.

### PR 7: Auth
- **Identity:** ASP.NET Core Identity on .NET 10, which has built-in passkeys (`MakePasskeyCreationOptionsAsync`, `PasskeySignInAsync`, …). No extra library. Our own Razor Pages under `/account` and a self-hosted `passkeys.js`.
- **Email login link:**
  - Single use, 15-minute TTL, stored as a hash.
  - A GET shows a confirm button and a POST consumes the link, so link scanners can't use it up.
  - Responses don't reveal whether an account exists; rate limits apply.
  - Sent through the outbox.
- **Hardening:**
  - Strict CSP, HSTS, `Referrer-Policy: no-referrer`, `__Host-` cookies, antiforgery.
  - Data Protection keys live in Postgres, protected by a Key Vault key outside regtest.
- **Tests:** WebApplicationFactory + Testcontainers + Mailpit for the link flow. Playwright with a Chromium virtual authenticator for passkeys.
- **Invariants:** 8, 10.

### PR 8a: Terraform bootstrap + CI
- **`infra/bootstrap`:**
  - State storage account and ops Key Vault (AVM).
  - Per environment: `plan` and `apply` identities (AVM UAMI with federated credentials), and resource group `rg-freeside-<env>`. No BTCPay resource group: BTCPay runs on the operator's node (ADR 0013).
  - Role assignments.
- **Shared config:** `infra/.tflint.hcl` and three-platform lock files.
- **CI:** `.github/workflows/terraform.yml` per ADR 0012.
- **setup.sh:** Terraform 1.16.4 from the HashiCorp apt repo.
- **Dependabot:** add the `terraform` ecosystem for `/infra/**`, not grouped.
- **Outside the repo:**
  - Re-paste `setup.sh`.
  - Run the bootstrap locally as subscription Owner and migrate its state.
  - Create GitHub Environments `dev`, `uat` and `prod`, with required reviewers on `uat` and `prod`.
  - Set the variables the bootstrap outputs.

### PR 8b: Azure core
- **Layout:** `infra/modules/platform` and `infra/envs/{dev,uat,prod}`, with no `.tfvars`; environment differences live in `locals`.
- **Network:** VNet, subnets, deny-by-default NSGs, private DNS zones.
- **Observability:** Log Analytics and App Insights, with local auth disabled.
- **App Key Vault:** RBAC, purge protection, private endpoint, and a Data Protection key.
- **ACR:** admin disabled.
- **Postgres Flexible Server:** Entra-only, private endpoint, PITR.
- **Governance:** tags and budgets.

### PR 8c: Azure edge and app
- **Container Apps:** workload-profile environment, `web` and `worker` with user-assigned identities, and Key Vault secret references by name.
- **Tor client sidecar** in `web` and `worker` (ADR 0013): SOCKS5 on `127.0.0.1:9050`, image digest as a variable (image from PR 10).
- **Front Door + WAF:** per R6.
- **Quarantine storage:** shared keys off, user-delegation SAS only, a public endpoint (developers upload directly), and Defender for Storage malware scanning on upload.
- **App code:** the `X-Azure-FDID` check.

### PR 8d: BTCPay on the Start9 node
No Azure resources for BTCPay ([ADR 0013](../adr/0013-btcpay-on-operator-start9-node.md)). The app reaches it over a Tor onion service ([#132](https://github.com/DWFullen/Freeside/issues/132)). Needs PR 6, and the Tor sidecar from PR 8c and PR 10.
- **Configuration per environment:**
  - `Btcpay:BaseUrl`: the BTCPay onion URL (`btcpay-base-url`, a Key Vault secret, because StartOS onion services have no client authorization).
  - `Btcpay:SocksProxy`: `socks5://127.0.0.1:9050`, the sidecar.
  - Key Vault secret names for the API key and webhook secret.
  - The URL, key and secret are placeholders until the node exists.
- **Connection check:** a read-only command, run against a configured environment, that checks:
  - `/api/v1/health` reports synchronized;
  - the server version matches the regtest pin;
  - the platform store's preview addresses carry the environment's prefix;
  - a test webhook arrives;
  - round-trip times are reported.
- **Runbook** `docs/runbooks/btcpay-start9-setup.md`:
  - installing the StartOS BTCPay and Bitcoin Core packages;
  - registrations off, 2FA or passkeys on;
  - the API key with minimal permissions;
  - the platform store with the watch-only xpub;
  - the webhook;
  - backups, and a restore drill with the NBXplorer rescan;
  - a UPS and outage alerts;
  - re-pinning `tools/regtest/compose.yml` after a Start9 package update.

### PR 9: Cloudflare R2
- **Bucket:** one R2 bucket per environment in the same Terraform roots.
- **CORS:** GET and HEAD from the canonical origin.
- **Lifecycle:** only aborts incomplete multipart uploads. Nothing expires (P3).
- **Immutability:** a bucket lock if the provider supports it.
- **Credentials:** R2 S3 credentials are created outside Terraform.

### PR 10: Images + deploy
- **Images:** chiseled .NET 10 images pinned by digest.
- **Build pipeline:** CycloneDX SBOM, `actions/attest-build-provenance`, push to ACR over OIDC.
- **Deploy:** `terraform apply` with image digests.
- **Migrations:** an EF migration bundle as a Container Apps Job under a migrator identity.
- **Tor client sidecar image** (ADR 0013): built from the Tor Project's Debian package on a base image pinned by digest, with the same SBOM, provenance and scanning as the app images.

## Phase 1: what can be built now, and what is blocked

**Buildable with fakes and regtest:**
- Catalog, storefronts, feed, search, product pages.
- Checkout on the BTCPay regtest xpub rail plus `FakeStrikeRail`, the unified BIP21 QR, WebLN, failover and the circuit breaker.
- The LUD-21 rail against a fake LNURL server.
- Library, entitlements, guest claim tokens, library JSON export.
- Payout destination onboarding (except the Nostr signature).
- The fee account ledger, pause rules and debit scheduler against `FakeFeeCollector`.
- Pool top-ups through the platform BTCPay store.
- Refunds and SLA strikes, the developer dashboard.
- Mature tagging, the adult holding queue and the self-attestation age gate.
- US state/ZIP capture.
- Reconciliation jobs.
- Build upload to quarantine (Azurite) with a fake malware scanner.
- Metadata curation checks, the DMCA workflow.

**Blocked:**

| Decision | Blocks |
|---|---|
| D8 | Real Strike adapter, the directed-invoice guard against the real API, Strike top-ups and treasury |
| D12 | Real ACH adapter, bank linking, mandate text, return webhooks |
| D16 | Payout config signing, NIP-05 pinning, relay commitments, the P4 signature check at checkout |
| D17 | Opening developer onboarding |
| D18 | Passkey RP ID, Lightning Login host, email domain, Front Door domain, CORS origin |
| Not yet raised as decisions | Rate source (Q10), receipt signing key type (Q11), manifest signing scheme, sanctions screening source, CSAM hash-matching vendor, IP geolocation provider, the DRM-free VM check (*Hypothesis*: needs a prototype) |

## Open questions

| # | Issue | Question | Needed by |
|---|---|---|---|
| Q5 | [#10](https://github.com/DWFullen/Freeside/issues/10) | Log Analytics and App Insights keys land in state through azurerm. Is it acceptable once local auth is disabled (the default plan), or should those two use azapi instead of AVM? | PR 8b |
| Q7 | [#11](https://github.com/DWFullen/Freeside/issues/11) | Per-developer BTCPay webhook secrets: one Key Vault secret each, or envelope encryption in Postgres with a Key Vault key (*Recommendation*: envelope encryption, recorded in an ADR) | Phase 1 onboarding |
| Q8 | [#12](https://github.com/DWFullen/Freeside/issues/12) | One subscription per environment, or one subscription with separate resource groups. Region *Default*: `eastus2` | PR 8a |
| Q9 | [#13](https://github.com/DWFullen/Freeside/issues/13) | **Resolved 2026-09-29: PostgreSQL 18.** It is GA on Azure Flexible Server (*Documented*, Microsoft Tech Community, "PostgreSQL 18 now GA on Azure Postgres Flexible Server", late 2025); 19 is still in beta upstream. The app database uses 18 in compose, Testcontainers and Azure. BTCPay's own database is `btcpayserver/postgres` 18.6, the image the Start9 BTCPay package ships (ADR 0013) ⏱ | PR 3 |
| Q10 | [#14](https://github.com/DWFullen/Freeside/issues/14) | Rate source (`AGENTS.md` §4.3). Strike quotes its own rate | Phase 1 checkout |
| Q11 | [#15](https://github.com/DWFullen/Freeside/issues/15) | Azure Key Vault holds no Ed25519 keys (*Inferred*). Keep Ed25519 receipts with the key in app memory, or switch to ECDSA P-256 signed inside Key Vault | Phase 1 receipts |
| Q12 | [#16](https://github.com/DWFullen/Freeside/issues/16) | Budget amounts per environment. *Default:* dev $150/month, uat $300/month | PR 8b |
| Q13 | [#132](https://github.com/DWFullen/Freeside/issues/132) | **Resolved 2026-09-30: Tor onion service** through a Tor client sidecar; StartTunnel is the fallback if Tor latency hurts checkout; Cloudflare Tunnel rejected. Signet for dev and UAT runs on a machine the operator controls (ADR 0013) | PR 8c |
| — | [#7](https://github.com/DWFullen/Freeside/issues/7) | *Recommendation:* the passkey RP ID is as permanent as the Lightning Login host. Add it to ADR 0011 when D18 is decided | D18 |
| Visibility | [#9](https://github.com/DWFullen/Freeside/issues/9) | **Resolved 2026-09-29:** the repository is public again, so CodeQL, dependency review, secret scanning and environment required reviewers stay free (ADR 0012 unchanged) | PR 2 |
