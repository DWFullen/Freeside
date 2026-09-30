# ADR 0013: BTCPay Server runs on the operator's Start9 node, not in Azure

| Field | Value |
|---|---|
| Status | Accepted. Connection decided 2026-09-30: a Tor onion service ([#132](https://github.com/DWFullen/Freeside/issues/132)) |
| Date | 2026-09-30 |
| Overrides | `project.md` §8 "BTCPay Server VM"; the Phase 0 plan's PR 8d (BTCPay VM + Bastion); the `AGENTS.md` §3 and §6.1 defaults for the processor host. Accepts a deviation from `AGENTS.md` invariant 14 (see Consequences) |
| Sources | `project.md` §0.3, §4.2, §8; `AGENTS.md` §2 (invariants 1, 4, 8, 14), §4.2, §6.3, §7.2, §7.5; [`docs/plans/phase-0.md`](../plans/phase-0.md) |

## Context

- The operator will self-host BTCPay Server on their own **Start9** node, running StartOS. The operator decided that no BTCPay VM is deployed to Azure.
- What the StartOS BTCPay package does, as of 2026-09-30 (*Documented*, [Start9Labs/btcpayserver-startos](https://github.com/Start9Labs/btcpayserver-startos) `master` at `21ce7266`, README and manifest):
  - It runs the **unmodified upstream images**: `btcpayserver/btcpayserver:2.4.4`, `nicolasdorier/nbxplorer:2.6.13` and `btcpayserver/postgres:18.6`. Bitcoin Core is a separate StartOS package at 31.1.
  - It **forces mainnet**: `network` in BTCPay's config and `mainnet` in NBXplorer's are rewritten on every start. One Start9 node therefore can't also serve signet.
  - StartOS updates BTCPay itself (BTCPay's own update check is off), so the node's versions change only when Start9 ships a new package.
  - Backups leave out NBXplorer's state. A restore is followed by a chain rescan.
  - It exposes one interface: the web UI and Greenfield API on port 23000. NBXplorer and Postgres stay private to the service.
- StartOS reaches the outside through Tor (a separate service), clearnet via the home router, or a StartTunnel WireGuard gateway on a VPS (*Documented*, StartOS docs). #132 compared Tor, Cloudflare Tunnel and StartTunnel; the operator chose Tor (see Connection).
- `AGENTS.md` §6.3 already names owned hardware as the alternative when physical custody matters, and a processor that can move within hours as the answer to single-provider risk.

## Decision

1. **No BTCPay in Azure.** No VM, Bastion, subnet, NSG or resource group for BTCPay in any environment.
2. **Production (mainnet)** BTCPay is the operator's Start9 node. It is multi-tenant as before (`project.md` §8): one store per developer, plus the platform's fallback store. Hot wallets and built-in Lightning stay disabled for developer stores.
3. **Dev and UAT (signet)** use a separate signet BTCPay (`btcpayserver-docker`) on a machine the operator controls (placeholder `btcpay-signet-host`; option A in #132). Until it exists, dev and UAT run with the BTCPay rail off.
4. **Local and CI** use the regtest stack in `tools/regtest/compose.yml`. Its BTCPay, NBXplorer, BTCPay Postgres and Bitcoin Core versions **match the Start9 packages**, and are re-pinned by hand when the node's package is updated. Dependabot doesn't bump them.
5. **Placeholders** until the node is set up: `btcpay-base-url` (the onion URL), `btcpay-api-key`, `btcpay-webhook-secret`, `btcpay-mainnet-xpub` and `btcpay-signet-host` ([`placeholders.md`](../plans/placeholders.md)). A missing or unreachable BTCPay opens the rail's circuit breaker. It never falls back to a fake, and it never stops the app (P8).
6. **Checks instead of IaC.** The app's startup check compares preview address prefixes with `Bitcoin:Network`, and refuses to start on a mismatch (invariant 4, PR 6). A read-only connection check verifies the node (PR 8d):
   - health reports synchronized;
   - the BTCPay version matches the regtest pin;
   - the network matches;
   - a test webhook arrives;
   - the round-trip time is reported.

## Connection: Tor onion service (#132)

| Item | Decision |
|---|---|
| Node side | Install the StartOS **Tor** service and add an onion service to BTCPay's `main` interface, as plain HTTP. StartOS recommends HTTP over onion: Tor already encrypts end to end, and the onion address authenticates the server (*Documented*, StartOS docs). The signet host (`btcpayserver-docker`) publishes BTCPay as an onion by default, so every environment uses the same path |
| App side | A **Tor client sidecar** in `web` and `worker` exposes SOCKS5 on `127.0.0.1:9050`. The BTCPay adapter's `HttpClient` goes through `Btcpay:SocksProxy`; .NET 10 hands the `.onion` hostname to the proxy for Tor to resolve (*Observed*, tested against a stub SOCKS5 server). Outside regtest, `http://` is accepted only for a `.onion` host |
| Access control | StartOS documents no onion client authorization, so anyone who learns the address can reach BTCPay's login page. The onion URL (`btcpay-base-url`) is therefore treated as a secret: kept in Key Vault and never logged. Behind it: an API key with minimal permissions (`AGENTS.md` §4.6), and 2FA or passkeys on BTCPay accounts |
| Webhooks (BTCPay → app) | BTCPay 2.4.4 uses its SOCKS proxy only for `.onion` URLs (*Observed*, `WebhookSender.GetClient`), so webhooks to the app's Front Door URL leave over clearnet from the operator's home IP. **Accepted for now.** Optional mitigation: a StartOS outbound gateway for BTCPay. Reconciliation polling (`AGENTS.md` §4.6) covers missed webhooks either way |
| Latency | Tor adds seconds per request. The connection check (PR 8d) reports round-trip times, and the rail's breaker and reconciliation tolerate failed circuits |
| Sidecar image | Built by us (PR 10) from the Tor Project's Debian package on a base image pinned by digest, and scanned like the app images. No official Tor client container was found (*Inferred*; confirm in PR 10) |
| Rejected | **Cloudflare Tunnel:** Cloudflare terminates TLS at its edge, so it would see the API key and every developer's xpub (invariant 8), and it adds an intermediary to the payment path (`project.md` §0.3). **StartTunnel:** end-to-end TLS and fast, but it adds a VPS to run and a public login page. It is the fallback if Tor latency hurts checkout |

## Options considered

| Option | Rejected because |
|---|---|
| Azure VM running `btcpayserver-docker` (the previous plan) | The operator decided against it |
| BTCPay containers in Azure Container Apps | BTCPay's stack is stateful and runs a P2P node, which doesn't fit PaaS (`AGENTS.md` §3). It is also still "BTCPay in Azure" |
| A store on a third party's BTCPay instance | The host is trusted for availability, privacy and correct address derivation. Needs an ADR of its own (`AGENTS.md` §4.1) |
| Signet on the same Start9 node | The StartOS package forces mainnet |

## Consequences

- **Invariant 14 deviation (accepted).** The node is configured through StartOS and BTCPay's own UI, not Terraform. Mitigations:
  - a runbook (`docs/runbooks/btcpay-start9-setup.md`, PR 8d) lists every setting the app depends on;
  - the connection check detects drift in version, network and webhook delivery;
  - Azure itself stays IaC only.
- **Availability depends on home power and internet.** While the node is down:
  - the BTCPay rail's breaker opens, and checkout uses the other rails: Strike and the Lightning Address (LUD-21) backups (P8);
  - the **on-chain backup is unavailable for every developer**, because every developer's store is on this one node. A long outage would leave developers without a working Strike rail with Lightning only.
  - A UPS and an outage alert are in the runbook.
- **Developer xpubs live on home hardware** (invariant 8). They are confidential, not spendable. The runbook covers physical security, full-disk encryption if StartOS offers it (to confirm), and where encrypted backups are kept off the node. The restore drill (`AGENTS.md` §7.5) includes the NBXplorer rescan.
- **P4 host-swap risk is unchanged:** the operator is the host who could swap a store's xpub. The signed payout config and relay commitments still guard against it.
- **Versions follow Start9.** Upstream BTCPay releases reach the node only after Start9 packages them. The regtest stack follows the node, not upstream.
- **Moving the node** (new hardware, colo) is a runbook task: StartOS backup and restore, plus rescan. The app only needs the new `btcpay-base-url`.

## Revisit when

- Sales volume or uptime needs outgrow a home node: colo, or a second node.
- Tor round trips measured by the connection check are too slow for checkout: switch the connection to StartTunnel (#132).
- Start9's package allows networks other than mainnet.
