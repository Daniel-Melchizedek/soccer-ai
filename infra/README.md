# Recreate The Environment

From the repository root, after Azure CLI sign-in:

```powershell
pwsh -File ./infra/provision.ps1
```

Defaults: resource group `rg-vog-dev`, region `swedencentral`, current Azure subscription. To select explicitly:

```powershell
pwsh -File ./infra/provision.ps1 -Environment dev -ResourceGroup rg-vog-dev -Location swedencentral -Subscription <subscription-id>
```

## Prerequisites

- PowerShell 7, Azure CLI with Bicep support, Git, and the .NET SDK selected by [global.json](../global.json).
- An authenticated Azure CLI **user** able to create the resource group/resources, assign roles, build in ACR, recover matching deleted resources, and access Foundry and Blob Storage. The script currently uses `az ad signed-in-user`; unattended service-principal sign-in is not implemented.
- Model availability and sufficient quota: the agent uses `gpt-5.6-terra` version `2026-07-09` at 800 GlobalStandard capacity; the judge uses `gpt-4.1` version `2025-04-14` at 30 GlobalStandard capacity. These are usage-billed deployments, not provisioned-throughput reservations. Application resources and model usage incur charges.
- The relevant Azure resource providers must be registered in the subscription. Subscription policy, quota, name retention and role propagation can still block a deployment; failed CLI operations stop the script.

On Windows, the scripts also find Azure CLI in its standard installation directory when it is missing from PATH. No portal actions, access keys in source, or local Docker daemon are required. No resource group is deleted by provisioning.

## What The Script Does

1. Creates the resource group and recovers matching soft-deleted Key Vault/Foundry resources belonging to that group. It never purges them. Recovery permissions and the original subscription/group names are required to reuse retained names.
2. Reuses the existing MCP key, or generates a new key for a fresh environment. A recovered vault without an existing gateway receives a new key through the deployment. An unreadable existing key stops deployment instead of silently rotating it.
3. Deploys [main.bicep](main.bicep): identity/RBAC, monitoring, private storage/container, ACR, Key Vault, Foundry account/project, agent model, independent judge, gateway environment/app, guardrail and language-specific MCP connections.
4. Builds the current working-tree gateway source in ACR and rolls it out. It writes nonsecret endpoints to the ignored local settings file and exports them for agent setup.
5. Uploads the two checked-in historical aggregates to `vog-data/public/` using Entra authentication.
6. Checks gateway health, then registers all three language agents from [the agent YAML](../foundry/agents/vog-match-companion.agent.yaml).
7. Runs [evaluate_agents.ps1](../scripts/evaluate_agents.ps1), which discovers current agent versions, registers language-specific rubric versions, creates evaluations, and submits 12 cases per language with GPT-4.1 as judge.

Infrastructure converges on the declared configuration. Agent versions, evaluator versions and evaluation runs are intentionally new on every invocation. Existing evaluation results are never overwritten. `-SkipEvaluations` avoids new evaluation runs but still provisions the judge. `-SkipBuild` requires an existing gateway image; it is not a fresh-environment rebuild.

Judge parameters are `-EvaluationModelName`, `-EvaluationModelVersion` and `-EvaluationModelCapacity`. To deploy only the judge into an existing account, use the same Bicep module:

```powershell
az deployment group create --resource-group rg-vog-dev --template-file infra/modules/evaluation-model.bicep --parameters accountName=<foundry-account-name>
```

## Evaluation Results

By default, each invocation saves request payloads, registered definitions, pinned runtime versions, cloud IDs and results in a new timestamped folder under [foundry/evaluations](../foundry/evaluations). Use `-EvaluationOutputDirectory` to choose a fresh folder. The source smoke requests/rubrics are the checked-in October 27 baseline; historical cloud IDs and versions in those snapshots are not reused for submission. English retains its original rubric; Spanish/Tamil retain their language-specific variants.

Azure evaluates asynchronously. Provisioning submits all runs and checks them once; it does not wait for their completion. Use the output directory printed by the script to collect status and all result pages without creating new runs:

```powershell
pwsh -File ./scripts/evaluate_agents.ps1 -CollectOnly -OutputDirectory <printed-output-directory>
```

To evaluate an existing project without rebuilding the application:

```powershell
pwsh -File ./scripts/evaluate_agents.ps1 -ProjectEndpoint <https-project-endpoint> -JudgeDeployment gpt-4.1
```

The judge deployment must already exist. An interrupted POST may have succeeded remotely: inspect saved artifacts and Azure state before resubmitting. Collection writes a summary and full paginated output items; individual failed quality checks are reported as results, while failed/canceled runs cause an error. Keep the artifact folder to retain evidence after resource deletion.

## Storage And Data Retention

The gateway uses private Blob Storage in two ways:

- [HistoricalData.cs](../src/Vog.Gateway/HistoricalData.cs) reads `public/goals-per-decade.json` and `public/home-advantage.json` for the separate Football history page. [The seed files](../data/historical) are snapshots of the deployed aggregates captured on October 27, 2026. They derive from Mart Jurisoo's [International football results](https://www.kaggle.com/datasets/martj42/international-football-results-from-1872-to-2017), CC0. Home-win percentages include neutral venues. Historical data does not drive live simulation or agent answers.
- [MatchArchiveService.cs](../src/Vog.Gateway/MatchArchiveService.cs) writes `match-snapshots/<seed>/<timestamp>.json` after completed matches and reads them for recent-match history. Records contain teams, final scores, goals, seed, time and match duration, not audio recordings or full event streams.

The raw `kaggle/results.csv` is an optional input for [Vog.DataPrep](../tools/Vog.DataPrep/Program.cs), not a gateway dependency. Provisioning restores the two aggregates, not that raw CSV. To regenerate the aggregates from a different source snapshot, upload the CSV and run the data-prep tool separately.

Resource recreation is **not backup restoration**. Old match snapshots, stored secrets, cloud evaluation runs, logs, and old container images are not recreated from source. New matches generate new archives; new evaluation runs generate new evidence. Back up any records you need before deleting the group. Restoring a soft-deleted resource may retain some service data, but the workflow does not depend on or guarantee that retention.

## Verification

```powershell
az bicep build --file infra/main.bicep --stdout
pwsh -File ./tests/infra/provisioning.tests.ps1
```

The offline test uses mocked Azure/HTTP/.NET commands in a temporary repository copy. It covers fresh/recovered/existing environments, foreign-resource exclusion, key-read failure, key reuse, seed uploads, new agent/rubric versions, and paginated results. It does not prove live deletion recovery, RBAC propagation, or a full rebuild.

GPT-4.1 was deployed from the judge Bicep module and the real evaluation runner completed all three live runs on October 27, 2026: English 12/12 passed, Spanish 12/12, Tamil 11/12; no execution errors. [Saved summary](../foundry/evaluations/2026-10-27/judge-gpt-4.1/live/summary.json). These were fresh live-agent runs, not regrading identical transcripts, so differences from the original judge are not a controlled judge-only comparison. The existing live group was not deleted or fully redeployed to test the rebuild flow.