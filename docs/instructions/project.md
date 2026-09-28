# Project Context: Freeside, a DRM-free game marketplace paid in bitcoin (by Sprawl)

| Field | Value |
|---|---|
| Layer | 2. Overrides `AGENTS.md` defaults. Does **not** override `AGENTS.md` §2 invariants without an ADR |
| Status | Draft. Decisions below come from the user unless marked *Default*, which means assumed and open to override |
| Last updated | 2026-09-27 |

---

## 0. Thesis

**No payment intermediary decides which lawful games can be sold here.** What the store carries is set by law and by the platform's own published standards, and by nothing else.

### 0.1 Why this exists

| Date (2025) | Event | Label |
|---|---|---|
| Jul 15–16 | Steam added rule 15 to its publisher guidelines: *"Content that may violate the rules and standards set forth by Steam's payment processors and related card networks and banks, or internet network providers. In particular, certain kinds of adult only content."* Over 100 games were delisted within about 16 hours | Documented (secondary reporting) |
| Jul 24 | itch.io removed adult content from browse and search, citing payment processors: *"Our ability to process payments is critical for every creator on our platform."* | Documented |
| Late Jul–Aug | Mastercard said it *"has not evaluated any game or required restrictions."* Valve replied that Mastercard *"communicated with payment processors and their acquiring banks,"* and that those processors cited Mastercard Rule 5.12.7: transactions that *"in the sole discretion of the Corporation, may damage the goodwill of the Corporation or reflect negatively on the Marks"* | Documented (secondary reporting) |
| Background | The pressure campaign came from the activist group Collective Shout, targeting Visa, Mastercard and PayPal | Documented (secondary reporting) |

**How it worked:** a card network's discretionary "brand damage" rule was passed down through processors and acquiring banks and applied to a whole platform at once. The platform's only choices were to delist the content or lose card payments for every buyer.

### 0.2 How this design removes that mechanism

| What hit Steam and itch.io | This platform |
|---|---|
| Card networks, processors and acquiring banks sit inside every sale | Buyers pay developers directly in bitcoin. No card network, processor or acquirer is in the sale (P1) |
| One intermediary's policy reaches every merchant at once, because the platform holds the payment relationship | The platform holds no payment relationship for sales. Losing any intermediary the platform uses cannot stop sales (P8) |
| A merchant has no fallback when a processor walks away | Every developer has a self-custodial payout destination. Custodial rails such as Strike are a convenience, never the only rail, and failover is automatic (P9, §4.2) |
| The platform adopts the intermediary's rules as its own (Steam's rule 15) | The platform never adopts an intermediary's content rules. If an intermediary's rules conflict with lawful content, the platform replaces the intermediary, not the content (P10) |

### 0.3 Chokepoints that remain

The design removes the card-network chokepoint. It cannot remove the others. It can only keep each one out of the sale path or make it replaceable.

| Layer | Intermediary | What it could do | Mitigation |
|---|---|---|---|
| Developer's primary rail | Strike, a regulated custodian. Its content policy is not verified yet (D8) | Close a developer's account, or close the platform's Strike Business account, which runs the Strike rail for **every** developer | Required self-custodial backups with automatic failover (P9, §4.2) |
| Platform fee collection | The ACH provider. Stripe, for example, prohibits *"pornography and other mature audience content"* (Documented) | Drop the platform as a customer | Pick a provider that accepts the catalog (D12). Losing the provider hurts fee collection only; sales continue (P8) |
| Hosting and delivery | Azure: Microsoft's Online Services acceptable use policy bans illegal content, not lawful adult content (Documented). Cloudflare R2: no category ban on lawful adult content (secondary source; verify) | Change their terms | Portable IaC, content-addressed artifacts, and a written plan to move providers (`AGENTS.md` §6.3) |
| Domain, DNS and TLS | Registrar, DNS host, certificate authority | Suspend the domain | A registrar with a published due-process policy. Optional Tor onion mirror (`AGENTS.md` §4.10) |
| Law | Age-verification statutes. *Free Speech Coalition v. Paxton* (June 27, 2025) upheld Texas HB 1181, which applies to sites where more than one-third of content is harmful to minors. Many states have similar laws | Require age verification if the catalog crosses a state's threshold | Track the share of adult content, age-gate mature titles, involve counsel (D15) |
| The platform itself | The operator | Give in to pressure | Published content standards. Changing them requires public notice and an ADR. Delisting never removes a title from owners' libraries (P3) |

### 0.4 Launch sequencing: adult titles (D5)

At launch the store carries **no adult titles**. This is a readiness decision, not a content rule taken from an intermediary, so it has written exit criteria (ADR 0009). A "temporary" restriction with no way out is how Steam's rule 15 became permanent policy.

| Item | Rule |
|---|---|
| Submissions | Adult-classified titles are accepted into a holding queue. They are not rejected and not listed |
| Opens when | (1) The D15 age gate and adult-share monitoring are live. (2) Counsel has reviewed age-verification exposure. (3) Every adult-title developer has a working rail that doesn't depend on Strike accepting adult merchants (a self-custodial primary, per D14) and a fee path that doesn't depend on the ACH provider accepting adult content (the pool) |
| Does not wait for | Strike (D8) or the ACH provider (D12) agreeing to adult content. If they refuse, adult-title developers run on self-custodial rails and the pool (P10: replace the intermediary, not the content) |
| Review date | *Default:* no later than 6 months after launch. Missing it requires a public notice explaining why |

---

## 1. Product summary

**Names (D9):** the store is **Freeside**; the parent company is **Sprawl**. Both come from William Gibson's *Neuromancer*: the Sprawl is the crowded, controlled megacity, and Freeside is the free orbital above it. The store is literally "the free side."

| Item | Status |
|---|---|
| Store domains | `freeside.games` and `freesidegames.com` were unregistered on 2026-09-27. **Register both now** (D18). `freeside.com` has been taken since 1992 |
| Parent legal name | *Default:* a compound such as "Sprawl Labs, LLC", not "Sprawl" alone. *SPRAWL* is also a 2023 cyberpunk shooter (Rogue Games) and *The Sprawl* is a tabletop RPG. `sprawl.games` and `sprawlgames.com` were also unregistered, but a "games" domain for the parent could be confused with that game |
| Known associations | Freeside is a lawless district in *Fallout: New Vegas* (Bethesda/Microsoft), shown in the Fallout TV series' season 2. No game company or store named Freeside was found |
| Clearance | USPTO search for FREESIDE (classes 9, 35, 41, 42) and the parent name, plus social handles, before public use (D19) |


A multi-developer store for DRM-free games and other digital goods, similar to GOG and Steam, where buyers pay in bitcoin.

- **Developers** sign up themselves, pass automated curation, and run their own storefront. Buyers pay them **directly**: the platform never holds or routes the money from a developer's sale. The default primary payout rail is the developer's Strike account, and a developer can make a self-custodial rail primary instead. Every developer also registers self-custodial backups (on-chain and Lightning), and checkout fails over to them automatically (§4.2).
- **Buyers** browse a main "what's new" feed covering all developers and can visit each developer's storefront. Everything they buy goes into one **library** of DRM-free, offline installers.
- **Ownership follows GOG:** a purchase is permanent access to download and keep the files. Nothing requires a login or network connection to run.
- **Platform revenue** comes from a $100 per-title listing fee (credited as prepaid fees) plus a **12%** revenue share, both recorded in a developer fee account. The account is settled by ACH debit, for example from a Strike account backed by Strike's line of credit, or from a prepaid BTC pool. Low balances never stop sales (P7).

---

## 2. Project invariants (in addition to `AGENTS.md` §2)

| # | Invariant | Why |
|---|---|---|
| P1 | **The platform never receives, holds or routes buyer funds for a developer's sale.** The only money it receives is its own revenue: listing fees, prepaid-pool top-ups and ACH debits of accrued fees | This is what keeps the platform out of money transmission (§6). Breaking it changes the legal position |
| P2 | **The prepaid pool is closed-loop.** It is spent only on platform fees. It is never used to pay refunds, buyers or any third party, and it cannot be transferred | Using it for anything else looks like transmitting money for others. Also see the prepaid-access flag in §6 |
| P3 | **Buyers never lose ownership.** Delisting, unpaid fees, a developer closing their account or a paused storefront stop *new sales* only. Builds stay downloadable for existing owners | This is GOG's ownership promise, and the developer agreement grants it (§7) |
| P4 | **Changing a developer's payout destination** needs step-up authentication (passkey), a mandatory delay (default 72 h), a notice through every verified channel, and a signature from the developer's **Nostr key** (D11). The key is pinned **outside the platform database**, through NIP-05 on the developer's own domain, fetched live. Checkout checks the signature against that pin before creating a payment request. A hash commitment of each signed config is published to Nostr relays so developers and third parties can detect tampering. The xpub itself is never published (`AGENTS.md` invariant 8). Details go in `docs/instructions/payments.md` | The platform acts as the BTCPay host, and a host can swap a merchant's xpub. Someone who can only write to the database can't forge the signature or the pin. A full compromise of the app server can't be stopped by the server's own checks, only **detected**; that is what the relay commitments are for |
| P5 | **Sell only to US buyers** until an ADR covers other jurisdictions | Platform delivery makes it the EU VAT "deemed supplier" (§6) |
| P6 | **A single static on-chain address is never accepted** as a payout destination | Payments can't be tied to orders, the address gets reused, and the developer's revenue becomes public. Violates `AGENTS.md` invariant 9 |
| P7 | **Sales pause only when a payment fails, never because a balance is low.** A developer with a valid ACH mandate is never paused because their prepaid pool is empty | A user requirement. It keeps the platform from blocking developer revenue over its own bookkeeping (§4.4) |
| P8 | **No intermediary the platform uses can stop sales.** Losing Strike, the ACH provider or any other platform-side intermediary can cut platform revenue, but sales continue on developers' self-custodial rails and fees accrue. P7 pauses apply only to a developer's *own* payment failures, never to the platform's | The thesis (§0) |
| P9 | **A storefront cannot go live without a self-custodial payout destination:** an on-chain xpub/descriptor (required), and optionally a Lightning backup. A custodial rail is never a developer's only rail | A custodian can close accounts, so it can't be the only way a developer gets paid (§0.3) |
| P10 | **The platform never adopts an intermediary's content rules.** What the store carries is set by law and the platform's published standards. When an intermediary's rules conflict with lawful content, the platform replaces the intermediary, not the content. Changing the standards needs public notice and an ADR | The thesis (§0). Steam's rule 15 is the anti-pattern |

---

## 3. Actors and scope

### 3.1 Developer (seller)

| Capability | First release | Later |
|---|---|---|
| Self-serve application plus automated curation (§5) | ✅ | |
| Storefront at `/{dev-slug}`: branding, catalog, about page | ✅ | Custom domains |
| Title management: store page, pricing, builds per OS, versioning | ✅ | Bundles, discounts and sales events |
| Payout destinations (§4.2) | Strike handle (primary), xpub/descriptor backup (required), Lightning Address + LUD-21 backup | NWC, own LN node |
| Fee account: link an ACH mandate, optional prepaid pool, statements, debit notices (§4.4) | ✅ | |
| Sales dashboard, refund queue, order lookup | ✅ | Analytics exports |
| Build upload (multipart, resumable), signed manifest | ✅ | CLI uploader for CI pipelines |

### 3.2 Buyer

| Capability | First release | Later |
|---|---|---|
| Main page: new releases, updated titles, tags, search | ✅ | Recommendations |
| Developer storefronts and product pages | ✅ | |
| Checkout: on-chain and/or Lightning, unified QR, WebLN one-click | ✅ | |
| Library: owned titles, installers per OS and version, checksums, changelogs | ✅ | |
| Signed purchase receipts and library export (JSON) | ✅ | |
| Refund requests (§4.7) | ✅ | |
| Wishlist, reviews, follow a developer | | ✅ |
| Open library/download API for third-party launchers | | ✅ Phase 2 |
| Desktop client (install and update) | | Phase 3 |

### 3.3 Accounts and identity (*Default*)

**Model:** each account (an internal ID) has **one or more authenticators**. Email, Nostr and Lightning Login are ways to prove you control an account, not separate account types.

| Authenticator | What it is | Privacy | Recovery | Allowed for |
|---|---|---|---|---|
| **Passkey** (WebAuthn) | Key on the device, or synced by the user's password manager | Per-site | Through another authenticator | Buyers. Developers: **required** (step-up for P4) |
| **Email** | Login link, and the recovery and receipt channel | The email address can be linked across sites | Self-service by email | Buyers, developers |
| **Nostr** (NIP-07 / NIP-46) | Public, portable identity | The npub is linkable across sites by design | Whoever holds the key | Buyers. Developers: **required** (payout signing, D11) |
| **Lightning Login** (LNURL-auth, LUD-04) | A key derived from the wallet's seed for this domain only | Can't be linked across sites; each domain gets its own key | Only with the same seed **and** a wallet that uses the same key derivation (LUD-05) | Buyers. Developers: as an extra login only, never in place of the passkey step-up |

Rules:
- **Any one authenticator can create a buyer account.** An account with a single authenticator gets a persistent prompt to add a second. With no email on file there is no recovery channel; the buyer's exported signed receipts (§7) are their proof of purchase.
- **The Lightning Login host is fixed forever.** Wallets derive the key from the full domain name (LUD-05), so the callback host (*Default:* `freeside.games`) must never change. Changing it gives every Lightning Login user a new key and a different account. Record this in ADR 0011.
- **Switching wallets:** a new wallet may derive a different key. A signed-in user attaches the new wallet with LUD-04 `action=link`.
- **Custodial wallets hold the user's key,** so the custodian could sign in as the user. That's acceptable for buyers, not for developer step-up.
- **QR relay phishing (QRLJacking):** an attacker can show our genuine QR code on their own page. Mitigations: `k1` is single-use, expires within 5 minutes, and is tied to the browser session that requested it; other authenticators are notified of new logins; sensitive actions need a passkey.
- **Implementation:** verify the DER signature over `k1` with NBitcoin (already a dependency), with no new library. Browser completion by polling or server-sent events.
- **Guest checkout** is allowed. The purchase issues a claim token (`AGENTS.md` §5.1) that can be attached to an account later.
- **Buyer data stored:** US state and ZIP (tax sourcing and geo evidence), order history, plus email or npub **only if** the buyer uses those authenticators. No names, postal addresses or payment identities beyond what's required.

---

## 4. Payments and platform revenue

### 4.1 Money flows

```
 Buyer ──(sale: sats)─────────────────────────────► Developer's wallet / Strike account
   │   platform creates the payment request;            ▲
   │   never touches the funds                          │ platform verifies settlement
   ▼                                                    │ (own node / Strike API / preimage)
 Platform: grant entitlement ─► record fee in the developer's fee account (USD)

 The fee account is settled from, in order:
   1. Prepaid pool     Developer ──(BTC top-ups)──────────► Platform Strike Business (§4.6)
   2. ACH mandate      Developer's US bank ──(ACH debit)──► Platform Strike Business (§4.6)
                       (e.g. a Strike account, where the Strike BLOC covers the debit)
   3. Grace allowance  Developers without a mandate only. Past it, new sales pause

 Platform Strike Business ──(BTC sweep above threshold)──► Platform cold storage (multisig)
```

### 4.2 Developer payout destinations

The developer's integrated **Strike account is the default primary rail**. A developer can promote a self-custodial rail to primary (D14). Every developer also registers **self-custodial backups** (P9), and checkout fails over to them automatically.

| Role | Rail | How the payment request is made | How payment is verified | Trust | Phase |
|---|---|---|---|---|---|
| **Primary (default)** | **Strike handle** | The platform's Strike API account creates an invoice paid into the developer's handle ("directing payments") | Strike `invoice.updated` webhook, then a re-fetch of the invoice state | Developer trusts Strike (custodial). Platform trusts Strike's API. The developer shares no secret | 1 |
| **Backup, on-chain (required)** | **xpub/descriptor** (watch-only) | One BTCPay store per developer; a fresh address for every order | The platform's own node (BTCPay/NBXplorer) | No intermediary. Host-swap risk handled by P4 | 1 |
| **Backup, Lightning** | **Lightning Address with LUD-21** | LNURL-pay callback | `verify` URL. Check `sha256(preimage) == payment_hash` | The preimage is cryptographic proof. Only as resistant to being cut off as the developer's Lightning provider | 1 |
| Backup, Lightning | **NWC** (NIP-47) | `make_invoice` | `lookup_invoice` / `payment_received` | The platform stores a connection secret. **Reject any connection whose `get_info` lists `pay_*` methods** | 2 |
| Backup, Lightning | Developer's own LND/CLN (invoice-only macaroon or restricted rune) through their BTCPay store | BTCPay | BTCPay | The platform holds receive-only credentials | 2 |

**What "backup address" means here:**
- **On-chain:** a single static address is rejected (P6). With several buyers paying one address, the platform can't tell which payment belongs to which order, and the developer's whole revenue becomes public. An xpub gives a fresh address per order, and every mainstream wallet can export one.
- **Lightning:** a Lightning Address is accepted as-is, as long as its provider supports LUD-21. Onboarding checks the LNURL-pay callback for a `verify` URL and rejects addresses without one.
- **Rejected:** matching payments to orders by a unique amount sent to a static address. Exchange withdrawals and wallet fee handling change the amount that actually arrives.

**Failover:**

| Item | Rule |
|---|---|
| Order | The developer's chosen priority. *Default:* Lightning goes Strike, then the Lightning backup. On-chain uses Strike's on-chain option if its directed invoices provide one (D8), otherwise the xpub backup. The unified QR combines the best available on-chain and Lightning destinations |
| Platform-wide trigger | A circuit breaker on the Strike API trips on authentication failure, repeated errors or timeouts when creating invoices, or a manual kill switch. While it's open, **every developer** fails over to their backups (P8) |
| Per-developer trigger | Strike rejects the developer's handle (`canReceive: false`, account closed), or invoice creation fails for that handle. That developer fails over; others are unaffected |
| Recovery | The breaker retries on a schedule and closes after several successful invoice creations in a row. The developer is notified at every switch |
| Buyer experience | Nothing changes except the QR may show a different destination. Checkout never fails while any healthy rail exists |
| Security | Backups are payout destinations, so P4 applies in full (signature, delay, notices). Otherwise an attacker who swapped a backup and then knocked Strike out would redirect every sale |
| Developers without Strike | A developer whose country Strike doesn't serve, or who has no account, runs with a self-custodial rail as primary. "Strike is always primary" holds only where Strike is available |

Rules:

- Internal-node Lightning and hot wallets stay **disabled** for developer stores on the platform's BTCPay (BTCPay's default for non-admins).
- **Dual-rail orders** (on-chain plus Lightning in a unified QR): the first rail to settle wins and the other payment request is cancelled. If the buyer pays both, the order is flagged overpaid and the developer refunds the duplicate.
- Base settlement policy applies (`AGENTS.md` §4.5). A Strike invoice in the `PAID` state counts as Lightning settled.
- At setup, the developer dashboard shows the **first N derived receive addresses** so the developer can check them against their own wallet.

### 4.3 Pricing

- *Default:* each developer prices a title in **USD or sats**, and buyers see both. The invoice locks the rate (`AGENTS.md` §4.3).
- **Free titles** are allowed. "Buying" one creates an entitlement with no payment and no fee. Free claims are rate-limited per account.
- **Paid titles start at $1**, checked in USD at invoice time.
- **Pay-what-you-want:** the developer sets a floor (at least $1) and a suggested price. The fee is charged on what the buyer actually pays.
- **On-chain payment is offered only on orders of $10 or more** (*Default*). Below that, checkout is Lightning only, because network fees make small on-chain payments impractical.

### 4.4 Platform fees

| Item | Decision | Details |
|---|---|---|
| Listing fee | Per title, similar to Steam's | **$100**, paid in BTC to the platform (§4.6) or by ACH debit. **The amount is credited to the developer's fee account** as an anti-spam deposit and prepaid fees |
| Revenue share | **12%** (`fee_bps = 1200`) | Charged on the sale's fiat value at the invoice's locked rate, excluding any tax. Stored as a configurable basis-point value (`fee_bps`) |
| Unit of account | **USD minor units** | One unit across both BTC and ACH funding. BTC top-ups are credited at the top-up invoice's locked rate. Keeps price moves out of fee disputes |
| Settlement order | 1. Prepaid pool, if funded → 2. Accrued and collected by ACH mandate → 3. Grace allowance (developers without a mandate only) | The developer can leave the pool at zero and rely on the ACH mandate alone |
| **ACH mandate** (primary for US developers) | Debit authorization on **any US bank account** | **Recommended setup:** a Strike account and routing number with Strike's Bitcoin-Backed Line of Credit covering debits automatically. Any other bank works; the developer then funds debits themselves |
| Collection cadence | **Monthly, or immediately** once accrued fees reach $500 | Debits under $10 roll forward to the next cycle (*Default*). Each debit is announced 2 banking days ahead with amount and date |
| Prepaid pool | BTC top-ups (Lightning or on-chain) to the platform's Strike Business account. The platform's BTCPay store is the fallback (§4.6) | **Required for developers without a mandate** (no US bank account, or opted out). Optional for everyone else |
| Grace allowance | *Default:* $50 of accrued fees, for developers without a mandate only | The only balance-driven pause in the system. Alerts go out at 50%, 20% and 0% of the developer's target pool balance, each with a one-tap Lightning top-up link |
| Sales pause triggers | A returned ACH debit not fixed within 7 days. An unauthorized-debit return. A developer without a mandate going past the grace allowance | **Never** an empty pool while a valid mandate exists (P7). **Never** a failure on the platform's side, such as losing the ACH provider or a Strike freeze: fees accrue until collection is restored (P8). Libraries are untouched (P3) |
| Returned debits | Insufficient funds (R01/R09): re-present within Nacha's re-initiation limits, notify, allow 7 days to fix. Account closed or invalid (R02/R03/R04): the mandate is invalid and the developer must re-link. Unauthorized (R10/R11/R29): pause immediately and review | Every attempt, return and re-presentment is a ledger row |
| Bank data | Linked through the ACH provider's tokenized account linking. **The platform never stores raw account or routing numbers** | Keeps Nacha data-security scope with the provider |
| Funding help page | Explains Strike's Bitcoin-Backed Line of Credit setup and links to Strike's own docs | Informational only: no endorsement, and no referral compensation unless counsel has cleared it (§6) |
| Unused pool balance | Refundable when the account closes | Counsel should review this (prepaid access, §6) |
| ~~Open-ended credit (fees owed with no payment method on file)~~ | **Rejected** | Unsecured collections risk. The grace allowance caps it |
| ~~Split at checkout as the fallback~~ | **Rejected** | Two independent payments per order can't be committed together. A buyer who pays the developer but not the platform leaves an order that is paid for but can't be fulfilled |

**Platform exposure by developer setup:**

| Developer setup | Maximum uncollected fees |
|---|---|
| Mandate on a business account (CCD) | One collection cycle (≤ $500 or 1 month), plus 2 banking days for an unauthorized return (R29) |
| Mandate on a personal account (PPD/WEB) | The same, plus the **60-day** unauthorized-return window (R10/R11) |
| No mandate (pool only) | The grace allowance ($50) |

**Strike credit availability ⏱:** businesses in 46 states + DC; consumers in 22 states + DC + PR (as of 2026-04-08). A sole proprietor on a personal Strike account may be in a state without it. That developer can still link any bank account, or use the pool.

Ledger requirements: accrued fees, pool credits, debit attempts, returns and re-presentments are all rows in the append-only ledger (`AGENTS.md` invariant 7), keyed to the order or debit ID. Balances are always derived from the ledger, never stored as a mutable number.

### 4.5 Order and entitlement flow

1. The buyer checks out. The platform confirms the buyer is in the US (P5) and that the developer is not paused (§4.4). For a developer without a mandate, it also reserves the fee against pool plus grace allowance, which stops simultaneous checkouts from overdrawing it. A developer with a valid mandate is never refused for balance reasons (P7).
2. The platform checks the P4 signature, then creates payment request(s) on the highest-priority healthy rail for each layer (§4.2 failover).
3. Settlement is verified on the rail (§4.2), and the order moves to `Paid` (`AGENTS.md` §4.4).
4. In one transaction: grant the entitlement, record the fee (taken from the pool if funded, otherwise accrued for ACH collection), write the ledger entries, issue a signed receipt. If the invoice expires or is invalidated, release any reservation.
5. Reconciliation (`AGENTS.md` §4.6) runs for every rail. For Strike, poll the invoice state as well as handling webhooks.

### 4.6 Platform treasury: Strike Business (ADR 0006)

The platform already needs a Strike Business API account to run the Strike developer rail (§4.2). This section extends that account to hold the platform's **own** money. Strike is custodial, so this is an exception to `AGENTS.md` invariant 12 and needs ADR 0006.

| Item | Decision |
|---|---|
| Scope | The platform's own money only: listing fees, pool top-ups, ACH fee settlements, and operating expenses paid by Strike Business Bill Pay (Azure, Cloudflare, vendors). **Never developer sale proceeds (P1)** |
| Account split | *Default:* two Strike Business accounts, if Strike permits it. **Rails** holds the API key; it creates developer-directed invoices and the platform's own top-up invoices, and is swept daily into Treasury. **Treasury** has no API key and is accessed only by humans with 2FA. If only one account is allowed: sweep daily, and never give a server-held key send or withdraw capability (key scoping to be verified, D8) |
| Directed-invoice guard | Every Strike invoice for a developer sale must name the developer's verified handle as the receiver. A missing handle, or the platform's own handle, is a hard failure before the invoice is created. Reconciliation raises an alarm if any sale invoice settles into a platform account. A misrouted sale would breach P1 |
| API key | Stored in Key Vault. Strike keys never expire, so rotate manually on a schedule and after any incident |
| BTC | Swept to platform cold storage (hardware or multisig, `AGENTS.md` §4.9) weekly, or when the balance passes a threshold (*Default:* $5k equivalent) |
| USD | Keep an operating float of about 3 months of expenses. Convert the excess to BTC and sweep it |
| ACH settlement | The ACH provider pays collected fees into the Strike Business account and routing number, **if the provider accepts it** (D12). Otherwise use a conventional business bank account and move funds to Strike |
| Fallback path | The platform's BTCPay store (watch-only xpub) stays live as a second way to receive top-ups and listing fees. There is a runbook for a Strike account freeze |
| Concentration risk | A frozen Strike Business account stops, all at once: the Strike rail for every developer, the platform's Lightning receiving, and USD settlement. The circuit breaker fails every developer over to their self-custodial backups, so sales continue (P8). Top-ups fall back to the BTCPay store |

This resolves D6: the platform receives Lightning through Strike, so it doesn't run its own Lightning node.

### 4.7 Refunds (platform floor, developers can offer more)

| Item | Decision |
|---|---|
| Platform floor | **30 days from purchase, even if downloaded and played** (GOG parity). Buyers keep the DRM-free files they already downloaded. Developers accept that cost when they list |
| Developer extension | A developer can offer a longer window. Shown at checkout |
| Abuse controls | *Default:* a buyer with more than 3 refunds in 90 days, or a refund rate above 25%, loses automatic eligibility, and further refunds are at the developer's discretion. Stated in the buyer terms |
| Execution | The buyer submits a refund destination (Lightning invoice or address). The developer sends the refund from their own wallet and records the txid or preimage. The platform never sends refunds (P1, P2) |
| Entitlement | Frozen when the refund is approved, revoked when the refund is confirmed |
| Fee | The platform fee is credited back to the developer's fee account |
| Enforcement | Missing floor-eligible refunds past the SLA (*Default:* 7 days) gives the developer a strike. Three strikes lead to a sales pause and review |

---

## 5. Developer onboarding: automated curation

The user's direction: self-serve, judged automatically against platform standards. Anything flagged goes to a human review queue. The content scope is decided (D5, §0.4). **The written standards document still needs drafting (D17).**

| Stage | Automated checks | Escalate to a human when |
|---|---|---|
| Account | Email and domain verification, passkey enrollment, **Nostr key registration** (NIP-07 or NIP-46 signer) and NIP-05 pin on the developer's domain, sanctions screening (OFAC), agreement acceptance | Sanctions hit, a disposable-email or domain mismatch, or no domain for a NIP-05 pin (handled in `payments.md`) |
| Payout destination | Strike handle `canReceive` check. xpub/descriptor parse plus derivation preview (**required**: no storefront goes live without it, P9). Lightning Address resolution plus LUD-21 `verify` check. Signature from the developer key (P4) | Parse failure or unverifiable ownership |
| Title metadata | Completeness (description, media, system requirements, content descriptors), name and asset collision against the catalog, **illegal-content** detection (for example known-CSAM hash matching) | Collision or illegal-content flag. Mature content is tagged and age-gated. Adult-classified titles go to the holding queue until adult titles open (§0.4); being adult is never grounds for rejection (P10) |
| Build | Malware scan, file type allowlist, size limits, checksum and signed manifest check | Any scan hit |
| DRM-free check (*Hypothesis: feasible, needs a prototype*) | Install and launch in an isolated VM with networking disabled. Pass if the process runs and draws a window for N seconds | Fails, or can't be automated for that OS |
| Ongoing | Refund SLA, complaint rate, DMCA notices | Thresholds crossed |

**Limit of automation:** it cannot confirm that a developer owns the IP. Rely on an ownership attestation in the agreement, a registered DMCA agent, a takedown workflow, and random sampling of approved titles for human review.

---

## 6. Legal and tax position (not legal advice; confirm each item with counsel)

| Topic | Position | Label |
|---|---|---|
| Money transmission | The platform never accepts buyer funds for a developer's sale (P1), so it neither accepts nor transmits value (FinCEN FIN-2019-G001 §2). Its fee income is ordinary service revenue | Documented definition; applied: Inference |
| Prepaid pool | It is closed-loop, non-transferable and only spent on platform fees (P2). Counsel should confirm it is outside prepaid-access rules | Hypothesis |
| Collecting fees by ACH debit | This is the platform collecting its own fees, so P1 still holds. The developer agreement carries the debit authorization (CCD for business accounts, WEB/PPD for personal accounts). Nacha fraud-monitoring rules apply to every non-consumer originator from 2026-06-22; the ACH provider should cover this. Unauthorized returns on personal accounts are allowed for 60 days | Documented (Nacha) |
| Strike Business terms | Before opening the account, read the Strike Business Terms on: **content restrictions (adult content)**, permitted business types, running a marketplace, directed payments to third parties, account freezes and holds, custody, and whether multiple accounts are allowed. I couldn't read the terms automatically | Open (D8), **blocking for the thesis** |
| ACH provider acceptance | Fiat payment providers may restrict businesses connected to crypto **or adult content**. Stripe prohibits *"pornography and other mature audience content"*. The provider must accept both (D12) | Documented (Stripe) / Open |
| Pointing developers to credit products | Informational links only. Any referral fee, affiliate deal or co-marketing with a lender needs counsel review first, because loan-referral rules vary by state | Hypothesis |
| Sales tax: Indiana | A marketplace facilitator **does not have to collect payment**; listing products, relaying offer and acceptance, or fulfillment is enough. Digital products are covered. Registration is required above **$100k** of sales into Indiana | Documented |
| Sales tax: other states | Definitions vary. The platform tracks gross sales per buyer state per year from its ledger, and **counsel is consulted before any threshold is crossed**. Until then, the developer agreement makes developers responsible for their own sales tax as sellers | Documented / Decision |
| How tax is collected after a threshold | **Decided (D7):** tax is added at checkout and paid to the developer along with the price. The platform then collects it through the developer's fee account (ACH debit or pool) and remits it. One payment per order | Decision; counsel to confirm |
| EU / UK | **Blocked** (P5). Under EU Art. 9a the platform would be the deemed supplier, because it delivers the downloads | Documented rule |
| Buyer geo-evidence | IP geolocation plus the buyer's declared US state and ZIP, both recorded on the order | Decision |
| Developer location | *Default:* developers may be anywhere except sanctioned jurisdictions, with OFAC screening at onboarding | Default; counsel to confirm |
| Income reporting | The platform pays no one for sales, so it files no payout forms for developers | Inference; counsel to confirm |
| DMCA | Register a designated agent and publish a takedown process before launch | Required |
| Content standards (P10) | Lawful content is allowed under the platform's published standards. **No adult titles at launch** (D5); submissions are held until the §0.4 exit criteria are met. Mature titles are tagged and age-gated. Illegal content is removed and reported as the law requires (known-CSAM detection; reports to NCMEC under 18 U.S.C. § 2258A). The store is not aimed at children | Decision (thesis, D5) |
| Age-verification laws | *Free Speech Coalition v. Paxton* (June 27, 2025) upheld Texas HB 1181 for sites where more than one-third of content is harmful to minors. Many states have similar laws. **Approach (D15):** account-level self-attestation plus a gate on each adult title, with no ID documents collected. Track the adult share of the catalog, and switch to verified age checks before it nears any state's one-third threshold | Documented / Decision |

---

## 7. Ownership guarantees (GOG philosophy)

- The purchase grants a permanent right to download and keep the DRM-free files. Games never need an account, a client or a network connection to run.
- The developer agreement grants the platform a **perpetual right to deliver purchased builds to existing owners**, including after delisting or account closure (P3).
- *Default:* keep the current build and **every build an owner downloaded**. Owners can reach older versions.
- **Signed receipts:** each purchase gets a receipt signed with the platform's Ed25519 key (order ID, title, version scope, buyer identifier hash, timestamp). Buyers can export their whole library as JSON with the receipts, so they can prove ownership even if the platform goes away.
- **Wind-down commitment:** if the platform ever shuts down, it will give at least 90 days' notice with downloads kept open, and publish the policy up front (*Default*).

---

## 8. Platform and delivery

| Concern | Decision | Notes |
|---|---|---|
| Language / framework | **.NET (ASP.NET Core)** | Same ecosystem as BTCPay and NBitcoin |
| UI | *Default:* server-rendered Razor Pages with progressive enhancement | Strict CSP, minimal JavaScript, no third-party scripts on checkout |
| Data | PostgreSQL Flexible Server, EF Core, `bigint` money columns | Append-only ledger. Balances derived from the ledger |
| Background work | *Default:* `BackgroundService` workers plus a Postgres-backed queue (`FOR UPDATE SKIP LOCKED`) | No message broker (`AGENTS.md` §1) |
| Compute | *Default:* **Container Apps**: `web` and `worker` | Two services, per `AGENTS.md` §6.1 |
| Payments | BTCPay Server VM, **multi-tenant with one store per developer**, plus the platform's fallback revenue store. Platform treasury and Lightning receiving on Strike Business (§4.6) | Hot wallets and internal Lightning disabled for non-admins |
| Fee collection (fiat) | An ACH origination provider with tokenized bank linking (D12), settling into the platform's Strike Business account (§4.6) | `IFeeCollector` interface. Debit scheduler is a `BackgroundService`. Provider webhooks go into the inbox (`AGENTS.md` §4.6) |
| Rail adapters | A `IPaymentRail` interface with implementations `StrikeDirected` (primary), `BtcpayOnchain` and `LnurlVerify` (backups), later `Nwc`. A `RailSelector` applies priority and circuit-breaker state per developer | Each emits normalized invoice events into the inbox. Failover switches are ledger rows |
| Artifact storage and delivery | **Cloudflare R2** (zero egress, `AGENTS.md` §5.3) with presigned GET URLs minted after the entitlement check | The platform pays for downloads out of its fee share. With zero egress that stays viable |
| Upload and quarantine | The developer uploads to an Azure Blob quarantine container → Defender for Storage malware scan → manifest check → promoted to R2 at a content-addressed path | Costs one Azure egress per build, not per download |
| IaC | **Terraform** with Azure Verified Modules (Azure and Cloudflare in one deployment, `AGENTS.md` §6.1) | ADR 0012: exact version pins, Entra-only state backend, no secret values in state |
| CI/CD | *Default:* GitHub Actions with OIDC to Azure | |
| Testing | xUnit, Testcontainers (Postgres), BTCPay regtest compose, Playwright for .NET | Rail contract tests for each adapter. Strike tested with mocks; check whether Strike has a sandbox (§9) |

### 8.1 Environments

| Env | Bitcoin network | Strike | Buyers |
|---|---|---|---|
| Local / CI | regtest | Mock | Test fixtures |
| Dev | signet | Mock or sandbox | Internal |
| UAT | signet | Sandbox, or a gated small-value mainnet canary via ADR | Internal plus invited testers |
| Prod | mainnet | Production | US only |

---

## 9. Decisions

### 9.1 Resolved (2026-09-27)

| # | Decision |
|---|---|
| D1 | Revenue share **12%** |
| D2 | Listing fee **$100**, credited to the fee account |
| D3 | Refund floor **30 days, even after download and play** (GOG parity), with abuse controls (§4.7) |
| D4 | Debit **monthly, or at $500 accrued**. Kept defaults: $10 minimum debit, $50 grace allowance, 7 days to fix a return |
| D5 | **No adult titles at launch**; they open when the §0.4 exit criteria are met |
| D6 | Platform Lightning and treasury on **Strike Business** (§4.6) |
| D7 | Post-threshold sales tax **collected through the developer fee account** (counsel to confirm) |
| D10 | **Free titles allowed, $1 minimum, pay-what-you-want with a developer floor.** On-chain only for orders of $10 or more |
| D11 | Payout configs signed by the developer's **Nostr key, pinned off-platform** through NIP-05. Details in `payments.md` (D16) |
| D13 | Fee accounts kept in **USD** |
| D14 | **The developer chooses their primary rail.** Strike is the default |
| D15 | Age checks: **self-attestation plus adult-share monitoring**; verified checks before nearing any state threshold |
| D9 | Store name **Freeside**, parent company **Sprawl** (§1) |

### 9.2 Open

| # | Decision | Owner | Needed by |
|---|---|---|---|
| D8 | Strike Business: content restrictions, terms fit (marketplace, directed payments, freezes), whether directed invoices can offer on-chain, whether API keys can be restricted to receive-only, whether two business accounts are allowed, test environment | Claude to research; user to confirm with Strike | Before opening the account |
| D12 | ACH origination provider: tokenized linking, support for Strike accounts (instant verification or micro-deposits), terms that allow a crypto-adjacent business and, eventually, adult content, Nacha fraud-monitoring coverage | Claude to research | Phase 1 |
| D18 | Register `freeside.games` and `freesidegames.com`, and choose the canonical host. It also becomes the permanent Lightning Login host (§3.3). Optional: a parent domain | User | Now |
| D19 | Trademark clearance for FREESIDE and the parent name (classes 9, 35, 41, 42), with Fallout and SPRAWL as known associations; check social handles | Counsel + user | Before public use of the name |
| D17 | Draft the published content standards: quality bar, excluded categories if any, mature and adult tagging, how the standards can change (public notice + ADR, P10) | User + Claude | Before onboarding opens |
| D16 | Write `docs/instructions/payments.md`: signed payout config format, NIP-05 pinning (and what happens for developers without a domain), relay-published commitments, a developer-side monitoring tool, rail priority and circuit breaker | Claude | Phase 1 |

---

## 10. Phases

| Phase | Scope |
|---|---|
| 0: Foundations | IaC (Azure + R2), CI/CD, regtest harness, auth (email + passkey), ledger schema, `IPaymentRail` |
| 1: MVP | Developer onboarding with automated curation and Nostr key registration, storefronts, catalog, main feed, Strike rail (primary) with xpub and LUD-21 backups and automatic failover, checkout, library, downloads, signed receipts, listing fee, fee account (ACH mandate and prepaid pool), refunds, US geo-gate |
| 2 | Nostr and Lightning Login for buyers, NWC and own-node rails, open launcher API, wishlist and reviews. Adult titles open once the §0.4 criteria are met (review within 6 months of launch) |
| 3 | Desktop client, P2P distribution for public content (BitTorrent v2 with web seeds), international expansion via ADR |

---

## 11. ADRs to write

| ADR | Subject |
|---|---|
| 0001 | Direct-pay marketplace model (P1) and its tax consequences |
| 0002 | Strike as the developers' default primary rail. This is a custodial exception to `AGENTS.md` invariant 12 for the *developer's* funds path, allowed only because P9 requires self-custodial backups |
| 0003 | US-only sales (P5) |
| 0004 | Artifact delivery on Cloudflare R2 (`AGENTS.md` §6.3) |
| 0005 | Fee collection: ACH mandate as primary, prepaid pool as fallback, grace allowance as the only unsecured credit (P2, P7, §4.4) |
| 0006 | Platform treasury on Strike Business, a custodial exception to `AGENTS.md` invariant 12 for the platform's own funds (§4.6) |
| 0007 | Thesis and content standards: no intermediary's content rules are adopted (P10) |
| 0008 | Payout rail priority chosen by the developer, required self-custodial backups and automatic failover (P8, P9, §4.2) |
| 0009 | No adult titles at launch: exit criteria and review date (§0.4) |
| 0010 | Payout config signing with Nostr keys pinned by NIP-05, and relay-published commitments (P4, D11) |
| 0011 | Account and authenticator model, and the permanent Lightning Login host (§3.3) |

## Non-goals

- Taking custody of developers' sale proceeds, or paying developers out
- DRM, online activation, or always-online requirements
- Tokens, NFTs or EVM chains
- IPFS as primary storage
- Selling to buyers outside the US before ADR 0003 is replaced
- Adopting any payment, banking or hosting intermediary's content rules as platform policy (P10)
