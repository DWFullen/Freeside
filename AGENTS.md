# Base Instructions: Bitcoin-Accepting Web Apps for Digital Goods

| Field | Value |
|---|---|
| Scope | Generic baseline for any web app that sells digital goods and accepts bitcoin (on-chain and/or Lightning) |
| Default host | Microsoft Azure, unless §6.3 gives a reason not to |
| Status | Base layer. Project-specific context lives in separate files (see §0) |
| Last verified | 2026-09-27. Items marked ⏱ depend on versions or pricing. Check them again before relying on them |

---

## 0. How to use this file

**Layering (highest precedence first):**

| Layer | File (suggested) | Contains |
|---|---|---|
| 1 | `docs/adr/NNNN-*.md` | Accepted decisions, including any approved override of an invariant in §2 |
| 2 | `docs/instructions/project.md` | Project context from the user (template in Appendix A) |
| 3 | `docs/instructions/<topic>.md` | Deeper topic files added later (e.g. `lightning.md`, `azure-landing-zone.md`, `delivery.md`) |
| 4 | `AGENTS.md` (this file) | Baseline practices and defaults |

- More specific layers override defaults in this file. **They do not override §2 invariants.** Only an ADR that states the risk accepted can do that.
- Before starting work, read `docs/instructions/project.md`. If it is missing or incomplete, use the defaults here and write down each assumption. Ask only when §1's escalation criteria apply.
- Tool shims: `CLAUDE.md` can contain just `@AGENTS.md`. Tools that use other files (e.g. `.github/copilot-instructions.md`) can point to this file the same way.

---

## 1. Working agreement

- **Build the model before changing things.** In a repo, read the README, tree, build system, dependencies, entry points, config, tests and CI workflows before making major changes.
- **Investigate before asking.** Settle uncertainty from code, config, docs and primary sources. Escalate only when a decision changes architecture, security posture, **custody or funds flow**, data retention, product behavior or cost in a material way, or when it conflicts with an explicit requirement.
- **Label claims** as *Documented*, *Observed*, *Inferred*, *Hypothesis* or *Recommendation*. Do not present speculation as certain.
- **Proposal format:** Problem → Evidence → Options → Recommendation → Why. Say why each rejected option was rejected.
- **Challenge wrong assumptions** directly, and explain the contradiction.
- **Make incremental, buildable changes.** Preserve working behavior. Update tests in the same change. Flag unrelated problems separately instead of quietly widening the scope.
- **Prefer boring technology.** Every new component (queue, cache, service, cluster) needs a stated problem it solves.
- **Check version-sensitive facts** (APIs, pricing, retirement dates, BIP/BOLT status) against primary sources, not memory.
- **When prototyping,** refactor the design as constraints show up. Do not stack exceptions on top of the first architecture.

---

## 2. Invariants (non-negotiable; override only by ADR)

1. **No private keys or seeds on internet-facing hosts.** On-chain receiving uses a watch-only xpub or descriptor in the payment processor. Signing keys stay offline or on hardware. *Only exception:* a Lightning node's hot wallet. It needs an isolated host, a balance cap, a sweep policy and a threat model entry.
2. **Two sources of truth.** The processor is authoritative for **payment state**. The app database is authoritative for **entitlements**. Client-side signals (redirect URLs, JS callbacks, "I've paid" buttons) never change either one.
3. **Grant entitlements only after server-verified settlement** under the configured settlement policy (§4.5).
4. **Bind the network explicitly and fail closed.** Every environment declares its Bitcoin network (`regtest` / `signet` / `testnet4` / `mainnet`). The code has no default. At startup, the app checks that the processor or store network and the address prefixes (`bcrt1` / `tb1` / `bc1`) match its config, and refuses to start if they do not. Mainnet credentials exist only in production.
5. **Money uses integers.** Bitcoin amounts are in sats, and in msat for Lightning. Fiat amounts are in minor units with an ISO 4217 code. No binary floating point anywhere on a money path. Exchange rates are stored as decimal strings together with their source and timestamp.
6. **Verify webhooks, then re-fetch.** Check the HMAC signature over the raw body with a constant-time compare. Reject everything if no secret is configured. Then re-read the invoice from the processor API before acting. Handlers must be idempotent.
7. **Append-only payment ledger.** Every payment or entitlement state change writes an immutable audit row recording the source event, actor, previous and next state, and timestamp.
8. **Secrets live in Key Vault** and are read through managed identity. They never appear in the repo, plaintext pipeline variables, images, logs or client bundles. **Treat xpubs and descriptors as confidential**, because they reveal the full payment history.
9. **No address reuse.** One invoice gets one fresh address.
10. **Collect minimal PII.** Do not put customer PII in processor metadata. Support guest checkout unless the project file says otherwise.
11. **Never send refunds automatically to the source address** of a payment. It may belong to an exchange or a shared wallet. Refunds go to an address the customer supplies (§4.8).
12. **No custodial third party in the funds path** unless an ADR accepts it.
13. **Published artifacts are verifiable.** Every downloadable build ships with a SHA-256 checksum and a signed manifest.
14. **Infrastructure is IaC only.** No portal-only changes in UAT or production.
15. **Fail closed.** Ambiguous payment or entitlement state goes to a manual-review queue and is never granted automatically.

---

## 3. Reference architecture (default)

```
                 ┌──────────────────────── Azure ─────────────────────────┐
 Buyer ──HTTPS──►│ Front Door + WAF ──► Web/API (App Service | Container  │
   │             │                      Apps)  ── private ──► PostgreSQL  │
   │             │                        │  ▲                 (orders,   │
   │             │                        │  │ webhook          ledger,   │
   │             │          Greenfield API│  │ (HMAC)          entitle-   │
   │             │                        ▼  │                  ments)    │
   │  checkout   │                 BTCPay Server VM (docker)              │
   └────────────►│                 bitcoind (pruned) · NBXplorer ·        │
                 │                 Postgres · [LN node]                   │
                 │                        │ P2P 8333 / LN 9735            │
                 │  Key Vault ◄─ managed identity (app, pipelines)        │
                 │  Blob (private) ── user-delegation SAS ─► Buyer        │
                 └────────────────────────┼───────────────────────────────┘
                                          ▼
                              Bitcoin / Lightning networks
 Offline: signing device(s) ── holds keys for the watch-only xpub in BTCPay
```

| Component | Default | Trust / notes |
|---|---|---|
| Payment processor | Self-hosted **BTCPay Server** (Greenfield API) | Non-custodial. Holds the xpub only. The LN node, if present, is the one hot wallet |
| Processor host | Linux VM running `btcpayserver-docker`, in its own subnet and resource group | Stateful P2P stack, so PaaS does not fit. Keep it portable (§6.3) |
| App | Stateless web/API plus a DB-backed inbox/outbox | No message broker unless load proves it is needed |
| Checkout UX | BTCPay hosted checkout | Hardened option: keep BTCPay private and render checkout from the Greenfield invoice payment-methods data. Trade-off: more code, smaller attack surface |
| Delivery | Private object storage plus short-lived signed URLs | See the egress economics in §5.3 |

---

## 4. Bitcoin payments

### 4.1 Processor and custody model

| Option | Custody | Use when | Status |
|---|---|---|---|
| Self-hosted BTCPay Server | Self | Default | ✅ Default |
| BTCPay store on a third party's instance | Self (keys); host trusted for availability, privacy and correct address derivation | Prototype, or no ops capacity | ⚠️ ADR |
| Custodial processor API (e.g. OpenNode) | Third party | Fiat auto-conversion is required and custody risk is accepted | ⚠️ ADR (invariant 12) |
| Build directly on a node or library (Bitcoin Core RPC, BDK, NBitcoin) | Self | Needs the processor cannot meet | ❌ Rejected by default: it re-implements invoicing, rate locking, gap handling and LN integration that BTCPay already provides |

Directories such as bitcoindev.org are **discovery indexes, not endorsements.** Some listed projects are unmaintained or defunct. Before adopting any dependency, check maintenance activity, releases, audits and the license.

### 4.2 Networks per environment ⏱

| Environment | Network | Notes |
|---|---|---|
| Local / CI | `regtest` | Deterministic. Mine blocks on demand. Polar or a docker compose setup for LN |
| Dev | `signet` or `testnet4` | testnet3 is deprecated. Do not start new work on it |
| UAT | `signet` or `testnet4` | Same network as dev. Separate store, wallet and keys |
| Prod | `mainnet` | Isolated subscription or resource group, credentials and wallet |

Each environment gets its own BTCPay store, wallet, API key and webhook secret. Nothing is shared across environments.

### 4.3 Pricing and amounts

- Price in fiat (minor units) or sats. The project file decides which.
- The processor locks the BTC amount when it creates the invoice. The app stores `{fiat_amount, currency, sats_amount, rate, rate_source, rate_timestamp}` for each invoice.
- The rate source is a trust dependency. Record it, alert when it fails, and never fall back silently to a different source.
- Lightning amounts are msat internally. Round only at display boundaries, using an explicit rounding rule.

### 4.4 Invoice-to-order state mapping (BTCPay Greenfield)

| Processor signal | Order state | App action |
|---|---|---|
| Invoice `New` | `AwaitingPayment` | Show checkout. Expiry comes from the processor, not the app clock |
| `InvoiceReceivedPayment` | `PaymentSeen` | Update UI only. No entitlement |
| `InvoiceProcessing` (full amount seen, unconfirmed) | `Confirming` | No entitlement unless the settlement policy allows it |
| `InvoiceSettled` | `Paid` | Grant entitlement idempotently, write to ledger, send receipt |
| `InvoiceSettled` + `overPaid` / `PaidOver` | `Paid` + flag | Fulfill. Offer a refund of the excess above the project threshold |
| `InvoiceExpired` + `PaidPartial` | `ExpiredUnderpaid` | Start the refund flow (§4.8) |
| `InvoiceExpired` | `Expired` | Close the order |
| Payment `afterExpiration` / `PaidLate` | `PaidLate` | Honor it if the full amount arrived inside the monitoring window. Otherwise send to review |
| `InvoiceInvalid` | `Invalid` | Freeze the entitlement and send to review |
| `manuallyMarked` | Per the mark | Require the operator's identity and a reason in the ledger |

Handle **backward transitions** (for example `Settled → Invalid` after a reorg or a manual mark) by freezing and reviewing. The app must not crash on them.

### 4.5 Settlement policy (defaults; the project file may tighten them)

| Rail / goods class | Grant at | BTCPay `speedPolicy` |
|---|---|---|
| Lightning (BOLT11) | Invoice settled | n/a (settles immediately) |
| On-chain, standard goods | ≥ 1 conf | `MediumSpeed` |
| On-chain, resellable or high-value goods (gift cards, bulk keys, above the project threshold) | ≥ 2 to 6 conf | `LowMediumSpeed` / `LowSpeed` |
| On-chain, 0-conf | **ADR only** | `HighSpeed` |

Why 0-conf needs an ADR: since Bitcoin Core 28.0, `mempoolfullrbf` defaults to on, so any unconfirmed transaction can be replaced. DRM-free goods cannot be revoked once they are downloaded, so a double-spend is unrecoverable revenue loss. Resellable license keys are the main fraud target.

### 4.6 Webhooks and reconciliation

- **Subscribe** to `InvoiceReceivedPayment`, `InvoiceProcessing`, `InvoiceSettled`, `InvoiceExpired`, `InvoiceInvalid` and `InvoicePaymentSettled`.
- **Verify** the `BTCPay-Sig` header (`sha256=<hex>`). Compute HMAC-SHA256 over the **raw** request bytes with the webhook secret and compare in constant time. Reject any request that fails.
- **Persist, then acknowledge.** Write the verified event to an inbox table and return 2xx quickly. Process it from the inbox.
- **Deduplicate** on `deliveryId` / `originalDeliveryId`. Make state transitions idempotent on `(invoiceId, targetState)`, and tolerate events that arrive out of order.
- **Re-fetch** the invoice through the API before every state transition. The webhook body only tells you something changed.
- **Reconcile.** Poll invoices in non-terminal states on a schedule, and run a daily comparison of processor records against the ledger. Alert on any drift. Webhooks will be missed eventually.
- **Use minimal API key permissions.** For a typical integration: view, create and modify invoices; modify webhooks; view store settings; create non-approved pull payments (for refunds).

### 4.7 Lightning ⏱

| Node option | Custody | Ops burden | Notes |
|---|---|---|---|
| Self-hosted LND or CLN (via BTCPay) | Self (hot) | High: channels, inbound liquidity, backups, fee management | Full control |
| LSP-assisted self-custodial (e.g. phoenixd, SDK-based) | Self, with an LSP dependency | Low to medium | Check each vendor's current trust model. Some depend on swaps or federations with their own trust assumptions |
| Custodial LN provider | Third party | Low | Violates invariant 12 without an ADR |

- Default to **BOLT11** invoices at checkout. BOLT12 is in the spec (merged Oct 2024) and supported in CLN, Eclair and LDK. LND support is still partial as of Sept 2026. Adopt BOLT12 only when the whole stack supports it.
- Cap the hot balance and sweep above it on a schedule (splice-out, loop-out or submarine swap to cold storage).
- Keep encrypted static channel backups off-host, and test restoring them.
- Offer a unified BIP21 QR (on-chain address plus `lightning=` parameter) when both rails are enabled.

### 4.8 Refunds and exceptions

- Use the processor's refund mechanism (BTCPay: refund produces a pull-payment claim link). The customer supplies the destination.
- Refund policy is set in the project file: fixed BTC amount or fiat-equivalent at refund time, and who pays the network fee.
- Every refund needs a ledger entry and operator attribution. Entitlements are revoked **before** the refund is released.

### 4.9 Wallet hygiene

- Use output descriptors. Prefer native SegWit (BIP84) or Taproot (BIP86) where the processor and signing device both support it.
- **Gap limit:** expired, unpaid invoices use up addresses. When restoring the wallet in other software, raise its gap limit above the longest run of unused addresses, or funds will look missing.
- **Rate-limit invoice creation** per session and IP. Invoice spam drives up address gaps and database growth.
- Separate wallets per environment and per store. Export labels (BIP329) for accounting.
- Have a written consolidation and sweep policy: consolidate when fees are low, use coin control, move treasury to multisig above a threshold.

### 4.10 Privacy

- Bitcoin payments are pseudonymous, not private. Treat address/txid ↔ customer links as personal data: minimize them, restrict access, redact them from logs.
- Checkout pages load no third-party trackers or CDN scripts. Use a strict CSP and self-hosted assets.
- Keep order and invoice IDs out of URLs that leak through `Referer`. Set `Referrer-Policy: no-referrer` on checkout.
- Optional: a Tor onion service for the storefront and checkout, and Payjoin (BIP78) where the processor supports it.
- Never check payments through public block-explorer APIs. That leaks which addresses belong to the merchant and trusts a third party. Use your own node through the processor.

---

## 5. Digital goods delivery (DRM-free)

### 5.1 Entitlements

- Model: `Order → OrderLine → Entitlement(holder, product, version_scope, status)`. Entitlements are separate from payments.
- The holder is an account or, for guest checkout, a **download token**: at least 128 bits of entropy, stored hashed, revocable, rate-limited, and recoverable through a channel the buyer chooses.
- Entitlement states: `active`, `frozen` (under review) and `revoked`. Every change is written to the ledger.

### 5.2 Access and integrity

- After checking the entitlement, mint a **short-lived, read-only, single-object signed URL** (Azure: user-delegation SAS, HTTPS only, expiry in minutes). Never expose container URLs or account keys.
- Support HTTP Range requests so downloads can resume.
- Artifact paths are immutable (versioned or content-addressed). A published artifact is never overwritten.
- Publish SHA-256 checksums and a signed manifest (minisign, GPG or Sigstore). CI checks the uploaded bytes against the manifest.
- **DRM-free means:** no online activation, no required phone-home, and installers that work offline. The purchase grants the right to download. Per-buyer watermarking is a project decision because of its privacy trade-off.
- If third parties upload builds, scan them for malware before publishing (Azure: Defender for Storage malware scanning).

### 5.3 Egress economics ⏱

Large binaries make bandwidth the dominant cost. Example: a 50 GB title × 1,000 downloads = 50 TB per month.

| Delivery path | Published price basis (Sept 2026, North America) | ≈ Monthly egress cost |
|---|---|---|
| Azure Blob → internet | 100 GB free, then $0.087/GB (next 10 TB), $0.083/GB (next 40 TB) | ≈ $4,200 |
| Azure Front Door Standard | $35/mo base plus $0.083/GB edge→client (first 10 TB), tiered after that | Same order of magnitude |
| Cloudflare R2 | $0 egress. $0.015/GB-month storage plus per-operation fees | ≈ $0 egress |

Rule: if expected monthly egress × Azure's per-GB rate costs more than running a second provider, put **artifact storage and delivery** on a zero- or low-egress provider and keep the rest on Azure. Manage both in the same IaC.

---

## 6. Azure platform

### 6.1 Defaults

| Concern | Default | Notes |
|---|---|---|
| Web/API compute | **App Service (Linux)** for a single app. **Container Apps** for 2+ containerized services or scale-to-zero | AKS rejected unless there is a specific need it meets |
| Processor host | Linux VM, Premium SSD, `btcpayserver-docker`, pruned node unless a full index is needed | Admin through Bastion/JIT, no public SSH. Outbound 8333. Inbound 9735 only if the LN node needs it. Set `maxuploadtarget` to cap node upload egress |
| App database | Azure Database for PostgreSQL Flexible Server, private access, PITR on | Never share the database with BTCPay |
| Edge | **Front Door Standard/Premium + WAF** | Azure CDN from Microsoft (classic): no new profiles since 2025-08-15, retires 2027-09-30 ⏱ |
| Object storage | Blob, private, user-delegation SAS | See §5.3 |
| Secrets | Key Vault (RBAC mode) plus managed identity. Key Vault references in app config | Rotate webhook secrets and API keys on a schedule and after any incident |
| Operator identity | Entra ID, MFA/passkeys, PIM for prod, no shared accounts | BTCPay admin: registrations off, 2FA or passkeys on |
| CI/CD auth | OIDC workload identity federation (GitHub Actions or Azure DevOps) | No long-lived client secrets |
| IaC | **Bicep + Azure Verified Modules** when Azure-only. **Terraform/OpenTofu** when non-Azure providers are in the same deployment | One IaC tool per repo |
| Network | VNet integration, private endpoints for the data plane, NSGs deny by default | The processor VM gets its own subnet |
| Observability | Azure Monitor / Application Insights through OpenTelemetry | See the payment metrics in §7.5 |
| Governance | Tags (`env`, `owner`, `cost-center`, `data-class`), Azure Policy, Defender for Cloud | Map to NIST 800-53 when the project requires it |
| Environments | Separate subscriptions per environment (at minimum, separate resource groups). Mainnet resources only in prod | Matches invariant 4 |
| Cost | Budgets and alerts per environment, with egress tracked as its own metric | |

### 6.2 BTCPay exposure decision

| Option | Surface | Effort | Default |
|---|---|---|---|
| Public BTCPay behind Front Door + WAF, hosted checkout | Admin UI reachable from the internet (hardened) | Low | ✅ |
| Private BTCPay (VNet-only), app renders checkout from Greenfield data | Only the app is public | Medium to high | Choose when the threat model requires it |

### 6.3 When not to use Azure (for a component)

| Situation | Problem on Azure | Alternative |
|---|---|---|
| High-volume large-file delivery | Metered internet egress (§5.3) | Zero- or low-egress object storage for artifacts. Web tier stays on Azure |
| Physical custody of LN hot keys matters | The cloud operator controls the hypervisor and disks | Owned hardware or colo for the node. App stays on Azure |
| Single-provider dependency for the revenue path | Account, policy or regional outage stops payments | Keep BTCPay portable (docker-compose, scripted restore, off-site backups) and able to move within hours |

---

## 7. Application engineering

### 7.1 Repo baseline

`README.md`, `ARCHITECTURE.md`, `AGENTS.md`, `docs/adr/`, `docs/instructions/`, `docs/runbooks/`, `.editorconfig`, lockfiles, a task runner (`make`/`just`), `docker-compose` for the local **regtest** stack, `.env.example` (no secrets), and `CODEOWNERS`. Protected `main`. PRs require green CI. The project chooses its branching model (trunk-based or Gitflow).

### 7.2 Testing

| Layer | Must cover |
|---|---|
| Unit | Order/entitlement state machine, including illegal transitions. Money conversion and rounding (property-based) |
| Contract | Webhook fixtures: valid, bad signature, missing secret, replay, out-of-order, backward transitions |
| Integration (regtest) | Create invoice → pay → mine → settle → entitlement. Underpay, overpay, late pay, expiry. LN pay and failure |
| E2E (signet/testnet4, UAT) | Real checkout through download with Playwright |
| Failure injection | Dropped webhook (reconciliation must catch it), processor down (checkout fails cleanly with no stuck orders), rate source down |

### 7.3 Security baseline

- Target **OWASP ASVS 5.0 Level 2** for auth, checkout, payment and download flows. Use the **OWASP Top 10:2025** as the review checklist. Follow **NIST SP 800-218 (SSDF)** practices.
- Session cookies: `Secure`, `HttpOnly`, `SameSite`. CSRF protection. HSTS. Strict CSP. Rate limits on auth, invoice creation and token redemption.
- Never implement cryptographic primitives. Use maintained libraries (Bitcoin Core RPC, BDK / rust-bitcoin, NBitcoin, bitcoinjs-lib) for any Bitcoin data handling.

### 7.4 Supply chain

Lockfiles committed. Renovate or Dependabot. SCA, secret scanning and SAST (e.g. CodeQL) in CI. Base images pinned by digest and CI actions pinned by SHA. Container image scanning. SBOM (CycloneDX or SPDX) for each release. Signed build provenance (SLSA). Reproducible builds for downloadable artifacts where feasible.

### 7.5 Data and observability

- Timestamps in UTC. Money columns are `bigint`. Versioned migrations. Append-only ledger. Backups with a **tested restore** drill for both the app DB and BTCPay.
- Structured logs with a correlation ID that carries through invoice → webhook → entitlement → download. Logs never contain xpubs, secrets, tokens or unredacted PII.
- Metrics and alerts: invoices created, settled and expired; webhook verification failures; inbox lag; reconciliation drift; time from payment to entitlement; LN hot balance and channel state; node sync height.

### 7.6 Required runbooks

Stuck or ambiguous invoice. Missed webhooks and replay. Processor restore from backup. Wallet restore, including the gap-limit issue. LN force-close or channel recovery. Suspected key or secret compromise. Rolling back a bad artifact publish.

---

## 8. Compliance flags (not legal advice; confirm with counsel)

| Topic | Engineering implication |
|---|---|
| Income and tax records | Store the fiat value, rate source and timestamp for every settled sale (§4.3) |
| Sales tax / VAT on digital goods | May depend on buyer location. Collect only the location evidence required |
| **Marketplace model** | Selling your own goods for BTC generally differs from money transmission (FinCEN FIN-2019-G001). **Taking payments for third-party sellers and paying them out can change that.** Get counsel before building seller payouts |
| Privacy law (GDPR/CCPA etc.) | Retention schedule, deletion path, data map covering processor metadata |
| Consumer refunds | Written refund policy for digital goods. Handle EU digital-content withdrawal rules where applicable |
| Sanctions | Screening obligations are a counsel decision. Keep the design able to block a jurisdiction |

---

## 9. Anti-patterns (reject)

| Anti-pattern | Why |
|---|---|
| Marking an order paid from the redirect or success page | The client controls it. Violates invariant 2 |
| Seed or xprv on the web tier, or in Key Vault for on-chain signing | A server compromise becomes theft of funds |
| Floats for amounts | Rounding drift. Violates invariant 5 |
| Static or reused receive addresses | Privacy leak and ambiguous attribution |
| Checking payments through public explorer APIs | Third-party trust and privacy leak |
| One store or wallet shared across environments | Crosses the network boundary. Violates invariant 4 |
| Automatic refund to the sender address | Funds may go to an exchange hot wallet |
| Custodial processor by default | Counterparty risk. Violates invariant 12 |
| Kubernetes, microservices or a message broker for one storefront | Complexity with no problem to justify it |
| Portal-only infrastructure changes | Unreproducible drift |
| Adopting a tool because a directory lists it | Directories are not endorsements (§4.1) |

---

## 10. Primary references

- BTCPay Server: Greenfield API — https://docs.btcpayserver.org/API/Greenfield/v1/ · eCommerce integration guide — https://docs.btcpayserver.org/Development/ecommerce-integration-guide/ · Deployment — https://docs.btcpayserver.org/Deployment/
- Bitcoin Core 28.0 release notes (testnet4, full-RBF default) — https://bitcoincore.org/en/releases/28.0/
- BIPs 21, 32, 78, 84, 86, 94, 329 — https://github.com/bitcoin/bips
- BOLTs (11, 12) — https://github.com/lightning/bolts · Offers status — https://bitcoinops.org/en/topics/offers/
- Bitcoin Optech (protocol change tracking) — https://bitcoinops.org
- Discovery index (not an endorsement) — https://www.bitcoindev.org
- OWASP ASVS — https://github.com/OWASP/ASVS · OWASP Top 10:2025 — https://top10.owasp.org/2025/
- NIST SP 800-218 (SSDF); NIST SP 800-53
- Azure Well-Architected Framework — https://learn.microsoft.com/azure/well-architected/
- Azure bandwidth pricing — https://azure.microsoft.com/pricing/details/bandwidth/ · Front Door pricing — https://azure.microsoft.com/pricing/details/frontdoor/ · Classic CDN retirement — https://learn.microsoft.com/azure/cdn/classic-cdn-retirement-faq
- Cloudflare R2 pricing — https://developers.cloudflare.com/r2/pricing/

---

## Appendix A — Project context template (`docs/instructions/project.md`)

```markdown
# Project Context: <name>

## Product
- Goods type(s):                      # games, ebooks, audio, software, license keys…
- Price range (min / typical / max):
- Resellable items? (keys, gift cards):
- Seller model: first-party only | marketplace (third-party sellers + payouts)

## Payments
- Rails: on-chain | Lightning | both
- Processor + host: (default: self-hosted BTCPay on Azure VM)
- Lightning node model: self-hosted | LSP-assisted | none
- Settlement policy overrides (per price band):
- Pricing currency + rate source:
- Refund policy (BTC-fixed vs fiat-equivalent; fee payer):
- Hot-wallet cap / sweep destination policy:

## Accounts & delivery
- Account model: guest tokens | accounts | passkeys | other
- Typical artifact size / expected downloads per month:
- Delivery provider: Azure Blob | other (justify via §5.3)
- Watermarking: yes/no

## Platform
- Language / framework / DB:
- Environments → networks mapping:
- IaC tool:
- CI/CD platform:

## Compliance
- Jurisdictions sold into:
- Control baseline (e.g., NIST 800-53 moderate) if any:
- Data retention requirements:

## Non-goals
-

## Accepted ADRs overriding this baseline
-
```
