## What and why

<!-- One paragraph. Link the issue, ADR or project.md section this implements. -->

## Invariants touched

Check every box that applies. If any box other than "None" is checked, link the ADR or `project.md` section that permits the change.

- [ ] None
- [ ] Payment state, entitlements, webhooks or ledger (`AGENTS.md` §2: 2, 3, 6, 7, 15)
- [ ] Money amounts, rates or rounding (§2: 5)
- [ ] Keys, secrets, xpubs/descriptors or network binding (§2: 1, 4, 8)
- [ ] Addresses, refunds or buyer PII (§2: 9, 10, 11)
- [ ] Custody or funds flow (§2: 12; `project.md` P1, P2, P8, P9)
- [ ] Payout destinations or their signing (`project.md` P4, P6)
- [ ] Published artifacts or infrastructure (§2: 13, 14)
- [ ] Content rules (`project.md` P10)

## Tests

- [ ] Unit / contract tests added or updated
- [ ] Integration tests on regtest, if payment or entitlement flow changed
- [ ] No test was deleted or weakened to make this pass

## Docs

- [ ] `project.md`, an ADR or a runbook updated if behavior or a decision changed
