# ADR 0012: Terraform with Azure Verified Modules, the state backend, and secrets kept out of state

| Field | Value |
|---|---|
| Status | Accepted |
| Date | 2026-09-27 |
| Overrides | None |
| Sources | `project.md` §8; `AGENTS.md` §2 (invariants 8, 14), §6.1, §7.4; ADR 0004; [`docs/plans/phase-0.md`](../plans/phase-0.md) |

## Context

- `project.md` §8 listed "Terraform/OpenTofu". Azure and Cloudflare are managed in one deployment (ADR 0004), and `AGENTS.md` §6.1 allows one IaC tool per repo. A single tool has to be chosen.
- Azure Verified Modules (AVM) publishes Terraform modules for most of what Phase 0 needs. As of 2026-09-27 (*Observed*, Terraform registry API):
  - All 17 modules Phase 0 needs are **0.x** releases.
  - None publishes a `waf-aligned` example. They offer `default`, `complete`, `private-endpoint` and similar.
  - The modules that depend on `hashicorp/azurerm` require `< 5.0` (keyvault allows `< 5.1`). azurerm 5.7.0 is out, so **4.81.0** is the newest version every module accepts.
  - Defender for Storage has no AVM module.
  - The VM module defaults `generate_admin_password_or_ssh_key = true` and contains `tls_private_key` and `random_password`.
- Terraform stores resource attributes in state in plaintext. That includes values from variables marked `sensitive`: the flag only redacts them in CLI output (*Documented*, Terraform docs). Only **ephemeral** values used in **write-only** arguments (`*_wo`, Terraform 1.11+) are never written to plan or state (*Documented*).
- Some azurerm resources read keys back into state even when we never supply a secret, for example Log Analytics workspace shared keys and the App Insights instrumentation key (*Inferred* from the azurerm attribute reference; checked when those resources land).
- The repository is **public**. Actions logs and job summaries can be read by anyone (*Documented*, GitHub). Environment required reviewers are free on public repos but need GitHub Enterprise on private ones (*Documented*).
- A `terraform plan` runs code chosen by the branch being planned (providers, data sources) with whatever credentials the job holds (*Inferred*). A plan on a pull request is therefore untrusted code.
- GitHub-hosted runners have no small, fixed IP range to allowlist (*Inferred*), so anything they must reach needs a public endpoint protected by identity.

## Decision

### Tool and versions

| Component | Pin |
|---|---|
| Terraform CLI | `= 1.16.4` (`required_version`, CI, `tools/claude-cloud/setup.sh` via the HashiCorp apt repo) |
| `hashicorp/azurerm` | `= 4.81.0` until every AVM module in use accepts 5.x |
| `Azure/azapi` | `= 2.12.0` |
| `Azure/modtm` | `= 0.4.0`. Downloaded because AVM modules declare it, even with telemetry off |
| `hashicorp/random` | `= 3.9.1` |
| `hashicorp/time` | `= 0.14.2` |
| `hashicorp/tls` | `= 4.4.1`. Required by the VM module; no `tls_*` resource may be created |
| `cloudflare/cloudflare` | `= 5.26.0` |

Every provider and module uses an exact version (`=`/`version = "x.y.z"`), never a range. Each root commits `.terraform.lock.hcl` with hashes for `linux_amd64`, `darwin_arm64` and `windows_amd64`.

### Modules

1. **Terraform only, not OpenTofu**, for all infrastructure: Azure and Cloudflare, in one configuration per environment.
2. Use an AVM module wherever one is published. Use `azurerm`/`azapi` resources directly only where none exists, with a `# No AVM module: <reason>` comment on the resource.
3. Every AVM module call sets `enable_telemetry = false`.
4. **All AVM modules are treated as unstable while they are 0.x.** Each version bump is its own PR (Dependabot Terraform updates are not grouped). The reviewer reads the module changelog and the dev plan diff. Nothing auto-merges.
5. **Reference configuration.** There are no Terraform `waf-aligned` examples, so each resource is configured from the module's most security-complete example, the `waf-aligned` e2e test of the matching Bicep AVM module (`Azure/bicep-registry-modules`), and the Azure Well-Architected service guide. `infra/README.md` lists every deviation from that reference, per environment, with the reason.

### State backend

Created by `infra/bootstrap`. The operator runs it once locally with local state, then migrates its state into the backend it created (`terraform init -migrate-state`) and deletes the local state file.

| Setting | Value |
|---|---|
| Resource group | `rg-freeside-tfstate`, separate from every environment |
| Authentication | Entra ID only: `shared_access_key_enabled = false`, `default_to_oauth_authentication = true`. Backend uses `use_azuread_auth = true`, and `use_oidc = true` in CI |
| Data protection | Blob versioning, 30-day blob soft delete, 30-day container soft delete, infrastructure encryption, TLS 1.2 minimum, no public blob access |
| Lock | `CanNotDelete` on the storage account |
| Containers | `bootstrap`, `dev`, `uat`, `prod`. One state file per root, inside its environment's container |
| Access | Each environment's identities get data-plane roles **scoped to that environment's container only**. Only the operator can read `bootstrap` |
| Network | Public endpoint, Entra-only. A deviation from the reference (private endpoint), because GitHub-hosted runners must reach it |
| Backend config | Passed with `-backend-config` from repository variables. No storage account names, subscription IDs or tenant IDs are committed |

### CI identities and flow

Bootstrap creates **two** user-assigned managed identities per environment, with GitHub OIDC federated credentials. Subjects are case-sensitive and start with `repo:DWFullen/Freeside:`.

| Identity | Federated subjects | Roles |
|---|---|---|
| `id-freeside-<env>-plan` | `pull_request` (dev only), `ref:refs/heads/main` | Reader on the environment's resource groups; Storage Blob Data Reader on its state container. Plans run with `-lock=false` |
| `id-freeside-<env>-apply` | `environment:<env>` | Contributor on the environment's resource groups; Role Based Access Control Administrator with an ABAC condition that allows only a fixed list of role definitions; Storage Blob Data Contributor on its state container |

- Pull requests run the static checks (`fmt`, `init -backend=false`, `validate`, tflint, Checkov, lock-hash check, `tf-guard`). A dev `plan` runs only when the `AZURE_*` variables exist and the PR branch is in this repository (never from forks).
- `apply` runs only on push to `main`, through GitHub Environments `dev` → `uat` → `prod`, with required reviewers on `uat` and `prod`.
  - The plan job (read-only identity) publishes an action summary and the SHA-256 of `terraform show -json`.
  - The apply job re-plans with the apply identity, **aborts if the hash differs**, then applies that plan file in the same job.
  - **Plan files are never uploaded as artifacts.**
- Checkov runs from its container image pinned by digest. trivy-action is not used: 76 of its 77 tags were force-pushed to credential-stealing code on 2026-03-19 (*Documented*, GHSA-69fq-xp46-6x23).

### Secrets and state

1. **Provider credentials reach Terraform only through environment variables:** `ARM_*` via OIDC, and `CLOUDFLARE_API_TOKEN` read by CI from the ops Key Vault. They are never Terraform variables.
2. **A secret value may reach a resource only through an `ephemeral` variable feeding a write-only argument.** The default is none. Application secrets are declared by **name** (Key Vault secret names wired into Container Apps secret references) and their values are set out of band by an operator with `az keyvault secret set`, following a runbook. A `sensitive` variable is not a way to keep a secret out of state.
3. **Forbidden in any configuration:** `random_password`, `tls_private_key`, any resource or module setting that generates a credential (`generate_admin_password_or_ssh_key` must be `false`), and any secret resource whose value would be stored in state.
4. **Keys the provider reads back** (Log Analytics shared keys, App Insights instrumentation key) are made useless by disabling local or shared-key authentication on the resource. They are listed in `infra/README.md`.
5. Credentials that can only be created with the secret visible to Terraform are created outside Terraform, for example Cloudflare R2 S3 credentials, which are derived from an API token's value.
6. **Two kinds of Key Vault.** The **ops** vault is created by bootstrap. It holds CI-only secrets such as the Cloudflare tokens, with per-secret RBAC per environment identity. Its public endpoint is protected by RBAC, because GitHub-hosted runners must reach it. Each environment's **app** vault is reachable only through a private endpoint.
7. CI enforces rules 3 and 4 and the `enable_telemetry = false` rule with `tools/ci/tf-guard.sh`.

### Public logs

- Workflow logs and job summaries show only `resource address → action`, never full plan output.
- Subscription and tenant IDs are never echoed.

## Options considered

| Option | Rejected because |
|---|---|
| OpenTofu | The user chose Terraform. AVM modules are developed and tested against Terraform (*Inferred*). OpenTofu's client-side state encryption (*Documented*) would soften rule 4, but it doesn't change rules 1–3, which keep secrets out of state in the first place |
| Bicep + AVM | Can't manage Cloudflare. `AGENTS.md` §6.1 prescribes Terraform when non-Azure providers share a deployment |
| Plain azurerm/azapi resources everywhere | More code to own, and it gives up the module defaults (for example, the storage module disables shared keys by default) |
| One identity per environment for plan and apply | A pull-request plan would run branch code while holding write credentials |
| Passing the plan file between jobs as an artifact | Plan files hold every attribute in cleartext, and artifacts on a public repository can be downloaded. Re-plan with a hash check gives the same guarantee |
| `sensitive` Terraform variables for application secrets | Their values are stored in state |
| Private endpoint on the state storage account | GitHub-hosted runners can't reach it. Self-hosted runners would add a component to operate (`AGENTS.md` §1) |
| azurerm 5.x | AVM modules in use reject it |

## Consequences

- **Must be built:** `infra/bootstrap`, the Terraform CI workflow, `tools/ci/tf-guard.sh`, the lock-hash check, `infra/README.md` with the deviation list, and a runbook for setting secret values out of band (PR 8a onward).
- **Dependabot Terraform PRs** record `h1:` hashes for linux_amd64 only (dependabot-core #5225). The lock-hash check fails them until someone runs `terraform providers lock -platform=linux_amd64 -platform=darwin_arm64 -platform=windows_amd64`.
- azurerm stays on 4.x until every module in use accepts 5.x.
- Secret values are set by operators, not by `terraform apply`. The Container Apps configuration must tolerate a secret that doesn't exist yet: its reference stays disabled until the value is set.
- Anyone can read the infrastructure code and workflow logs. The design assumes that; nothing in it relies on obscurity.
- Required reviewers stop working if the repository goes private without GitHub Enterprise.

## Revisit when

- Every AVM module in use accepts azurerm 5.x, or a module reaches 1.0.
- The repository goes private: environment approvals need GitHub Enterprise, or another gate.
- Self-hosted runners are adopted: the state account and ops vault can then go private.
- OpenTofu state encryption, or a similar Terraform feature, becomes a requirement.
