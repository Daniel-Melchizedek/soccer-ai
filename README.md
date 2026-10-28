# Voice of the Game

A multilingual football listening experience built with .NET 10, Microsoft Foundry and Azure Speech. A deterministic simulator supplies match facts; a scheduling engine turns them into live commentary and audio description; grounded agents answer questions about the match.

[Open the live app](https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/) | [Architecture](ARCHITECTURE.md)

Live matches use fictional generated leagues, clubs and players. The separate Football history page uses real CC0 international results. Historical data never feeds the simulation, recaps or companion.

## Features

- **Nine listening channels:** English, Spanish and Tamil, each with AudioDescription, Commentary and Calm. Azure Speech supplies audio with word-timed captions.
- **Live pitch:** five saved appearances, team/goalkeeper shape cues, adjustable markers, zoom, follow-ball, portrait layout and Windows forced-colour support.
- **Personal listening controls:** independent commentary, answer, event, ball and crowd levels; spatial earcons, ball sonification, supported-device haptics, clear speech and gentle sounds.
- **Customizable captions:** size, font, colours, opacity, outline and bottom/panel positioning, with device-local preferences.
- **Match companion:** tap-to-ask, local silence auto-stop, hold-to-ask, keyboard and typed questions. Three Foundry agents use six read-only MCP tools. Score and ball shortcuts use deterministic readouts.
- **Text catch-up and insights:** prioritized commentary received during a question, factual scoped recaps, source-event evidence, Casual highlights and an Analyst count chart with exact values.
- **Match history:** deduplicated completed-match summaries in private Blob Storage, plus a separate historical-results page with accessible tables and source credits.
- **Repeatable Azure setup:** infrastructure, agents, an independent evaluation judge, agent tracing, quality evaluations and managed red-team scans are provisioned from code.

The first listener starts the shared match at zero. Later listeners join it in progress; changing channels or stopping/restarting listening does not reset it. Matches run at natural 1x speed, repeat seed `20261026`, and pause for 120 seconds between matches. Restarting the gateway creates a new first-listener kickoff. There is no forced-goal or highlights demo mode.

The listener is static JavaScript served by ASP.NET Core, with self-hosted SignalR and AudioWorklet playback. Commentary uses reviewed templates and scheduling, not an LLM. See [the architecture](ARCHITECTURE.md) for the component map and data flows.

## Run Offline

Install the .NET SDK selected by [global.json](global.json): SDK baseline `10.0.400`, targeting .NET 10. Run commands from the repository root.

```powershell
dotnet restore
dotnet build --no-restore -c Release
dotnet test --no-build -c Release

dotnet run --project tools/Vog.Replay -- commentary
dotnet run --project tools/Vog.Replay -- commentary --lang es-ES
dotnet run --project tools/Vog.Replay -- commentary --mode calm --from 70 --to 80
dotnet run --project tools/Vog.Replay -- commentary --quiet --latency
dotnet run --project tools/Vog.Replay -- datacard --seeds 20
```

Offline simulation, commentary text and the test suite do not require Azure. Replay supports `ad`, `commentary`, `calm`, `analyst`, `casual` and `kids` modes; these are not all live listener channels. Spanish and Tamil phrase banks retain draft metadata.

## Deploy Or Recreate Azure

Install PowerShell 7+, Azure CLI with Bicep support, Git and the selected .NET SDK. Sign in as an Azure CLI user with resource deployment, role-assignment, ACR build, recovery and data-plane permissions. Unattended service-principal provisioning is not implemented. No local Docker daemon is required.

```powershell
az login
pwsh -File ./infra/provision.ps1 -Subscription <subscription-id>
```

[Provisioning](infra/provision.ps1) defaults to `rg-vog-dev` in `swedencentral`. It:

1. Creates the group and recovers matching soft-deleted Foundry/Key Vault resources without purging them.
2. Reuses the MCP secret, or creates one for a fresh environment; unreadable existing keys stop deployment.
3. Deploys identity/RBAC, Key Vault, private storage, ACR, Container Apps, monitoring, Foundry, both models, guardrail and MCP/tracing connections.
4. Builds the current worktree in ACR, deploys the gateway and writes nonsecret local endpoints to ignored `.env.local`.
5. Uploads the two checked-in historical aggregates, checks gateway health and registers all three agents.
6. Submits quality evaluations with GPT-4.1 as independent judge, followed by managed red-team scans.

Default deployments are `gpt-5.6-terra` version `2026-07-09` (800 GlobalStandard capacity) and `gpt-4.1` version `2025-04-14` (30 capacity). Availability, subscription quota, provider registration and RBAC propagation are prerequisites. Infrastructure and model usage incur charges; the gateway runs one always-on replica because match state and connections are in memory.

| Option | Effect |
|---|---|
| `-Environment`, `-ResourceGroup`, `-Location`, `-Subscription` | Select the deployment environment |
| `-SkipBuild` | Reuse an existing gateway image; still deploy infrastructure and register agents |
| `-SkipEvaluations` | Skip quality and red-team submissions; still provision tracing and the judge |
| `-SkipRedTeam` | Skip only red-team submissions |
| `-EvaluationOutputDirectory` | Choose a new, nonexistent quality-artifact folder |
| `-RedTeamOutputDirectory` | Choose a new or empty red-team-artifact folder |

Each invocation registers new agent/evaluator versions and creates fresh evaluation runs. Recreating resources restores configuration and the two seed aggregates, **not old match archives, traces, evaluation history, images or secret values**. Keep needed records separately. A full destructive delete/recreate cycle has not been tested live.

To deliberately remove the environment, first select the correct subscription:

```powershell
az account set --subscription <subscription-id>
pwsh -File ./infra/teardown.ps1 -ResourceGroup rg-vog-dev
```

Teardown asks for the group name, deletes everything in that group, attempts to purge its AI accounts and Key Vaults, and removes `.env.local`. Azure CLI must be on PATH. Verify purge outcomes in Azure before assuming retained names are available. Provisioning itself never deletes the resource group.

## Run With Azure

After provisioning, use the generated settings and your Azure CLI login:

```powershell
dotnet run --project src/Vog.Gateway
dotnet run --project tools/Vog.Replay -- listen --seconds 30
dotnet run --project tools/Vog.Replay -- speak --from 78 --minutes 2
dotnet run --project tools/Vog.Replay -- ask --url http://localhost:5080
```

Open `http://localhost:5080`, choose a language and mode, then select **Start listening**. Headphones help with spatial sound. Browser microphone permission is required for spoken questions, but typed questions remain available. The gateway requires Azure endpoints and access to the MCP secret in Key Vault; it is not a cloud-free UI server. Configuration details are in [Architecture](ARCHITECTURE.md#azure-resources-and-configuration).

To register new agent versions after changing the [agent definition](foundry/agents/vog-match-companion.agent.yaml), run `dotnet run --project tools/Vog.FoundrySetup`. Foundry uses its registered gateway MCP URL: a local gateway and the deployed gateway do not automatically share match state. For Replay against the deployed app, explicitly pass its URL; the `ask` command does not load `.env.local` when choosing a URL.

## Evaluations And Tracing

Normal provisioning creates the default project-wide Application Insights connection and reader roles. New agent calls produce service-side traces; the gateway's telemetry setting alone would not enable them. Traces can include inputs, outputs and tool data. Access is restricted through RBAC, retention is 30 days, and old conversations are not backfilled.

Use the [smoke request](foundry/agents/trace-smoke-request.json) to invoke the English agent, then inspect its Traces tab after ingestion delay:

```powershell
az rest --method post --url "<https-project-endpoint>/openai/v1/responses" --resource https://ai.azure.com --body '@foundry/agents/trace-smoke-request.json' --query '{id:id,status:status,error:error}'
```

For an existing project, submit evaluations without rebuilding the gateway:

```powershell
pwsh -File ./scripts/evaluate_agents.ps1 -ProjectEndpoint <https-project-endpoint> -JudgeDeployment gpt-4.1
pwsh -File ./scripts/redteam_agents.ps1 -ProjectEndpoint <https-project-endpoint> -JudgeDeployment gpt-4.1
```

The [quality runner](scripts/evaluate_agents.ps1) uses 12 cases per language from the checked-in baseline and discovers current agent/rubric versions. The [red-team runner](scripts/redteam_agents.ps1) pins the current agent versions and uses managed Foundry Evals with Sensitive Data Leakage and Task Adherence, one Base64 strategy and one turn. Use `-Locales en-gb` to limit a red-team submission to English. One turn is not a one-case limit or cost cap; this is limited preview coverage, not safety certification or equivalent multilingual coverage.

Both runners submit asynchronously and check status once. They print artifact directories containing cloud IDs, versions and results. Collect later without creating more runs:

```powershell
pwsh -File ./scripts/evaluate_agents.ps1 -CollectOnly -OutputDirectory <quality-output-directory>
pwsh -File ./scripts/redteam_agents.ps1 -CollectOnly -OutputDirectory <redteam-output-directory>
```

Standalone runners do not load `.env.local`: pass the project endpoint or export `VOG_FOUNDRY_PROJECT_ENDPOINT`. The judge must already exist. Preserve artifact folders and inspect cloud state after an interrupted POST before resubmitting. Collectors retrieve paginated output and report failed/canceled runs. Generated transcripts may contain sensitive content and must be reviewed before sharing or committing.

Live verification during the tracing rollout confirmed a completed smoke request and ingested `responsesapi` spans. The three initial red-team scans completed with no execution errors: English 21 passed / 5 failed, Spanish 34 / 0, and Tamil 22 / 16. These are results for the tested agent versions (11, 4 and 3), not guarantees for later registrations. English and Tamil failures remain review items; differing generated case counts are not a controlled language comparison.

## Verification

```powershell
dotnet test -c Release
node --experimental-vm-modules --test (Get-ChildItem tests/browser/*.test.mjs).FullName
pwsh -File ./tests/infra/provisioning.tests.ps1
az bicep build --file infra/main.bicep --stdout
```

.NET tests use xUnit v3 and Microsoft.Testing.Platform. Browser-module tests need Node.js and use its built-in test runner. The provisioning test is an offline mocked PowerShell harness covering fresh/recovered/existing environments, secret reuse/read failures, uploads, version discovery, submissions, skip switches, pagination and collection recovery. It does not deploy or delete cloud resources.

[CI](.github/workflows/ci.yml) builds and tests .NET, runs offline replay checks and scans Git history with Gitleaks. Browser tests, provisioning tests and cloud evaluations are separate checks. `/healthz` is only a process probe, not a Speech/Foundry/storage dependency test.

## Scope And Limitations

- Spanish/Tamil phrase banks retain draft labels. Native-language, screen-reader/co-design, real microphone and subjective audio/caption validation are separate from automated tests; no WCAG certification is claimed.
- Live match state is in one gateway process. Multiple replicas are not synchronized; restarts reset the schedule. Completed-match uploads are best-effort without retry, and a full live archive write/read round trip remains unverified.
- Speech pending work is bounded to 64 utterances per channel, but per-utterance PCM and companion buffers are not all bounded. Public listener access lacks durable user quotas. The 50-question conversation limit resets on reconnection.
- After a sent question fails or times out, Stop/Start listening is required before asking again to isolate late answers. Text catch-up only includes commentary received by that browser. Spoken catch-up and hands-free echo cancellation are not implemented.
- Backend braille briefs/text groups do not imply a browser braille or text-only selector. Hosted Foundry workflows, Fabric and a separate Studio/overlay are not implemented.
- Historical results are real data on a separate page. Page separation does not prove compliance with a synthetic-data-only competition; submission scope still needs an eligibility decision. No natural three-goal sequence within a 120-second demonstration has been verified, and no scoring is manufactured for a demo.

## Data, Security And Credits

Live simulation data is fictional; no actual clubs, trademarks or footage feed it. The separate [Football history page](src/Vog.Gateway/wwwroot/football-history.html) uses the two [checked-in aggregates](data/historical), restored to private Blob Storage during provisioning. Its allowlisted public proxy exposes only those aggregates, not raw CSVs. Home-win percentages include neutral venues; the latest decade is incomplete. [Vog.DataPrep](tools/Vog.DataPrep/Program.cs) can regenerate aggregates from an optional stored results CSV and is not needed for normal deployment.

Azure service access uses Azure CLI locally and managed identity when hosted. The MCP application secret is stored in Key Vault and Foundry connections, not agent YAML or generated local settings. Blob shared-key and anonymous access are disabled. Keep credentials, dotenv files, generated audio and unreviewed evaluation/trace content out of Git. Vendored licence and attribution files are retained alongside their libraries.

| Work | Attribution / licence | Use |
|---|---|---|
| [International football results](https://www.kaggle.com/datasets/martj42/international-football-results-from-1872-to-2017) | Mart Jurisoo, CC0 Public Domain | Separate historical aggregates, never simulation input |
| [football-results-dashboard](https://github.com/bbenz/football-results-dashboard) | Brian Benz, MIT | Historical-chart inspiration |
| [Chart.js](https://www.chartjs.org) | Chart.js contributors, MIT | Locally served 4.5.1 charts for Analyst and Football history |
| [Atkinson Hyperlegible](https://brailleinstitute.org/freefont) | Braille Institute, SIL OFL 1.1 | Self-hosted caption and pitch-number font |
| [SignalR](https://github.com/dotnet/aspnetcore) | .NET Foundation and contributors, MIT | Real-time browser transport; locally served 10.0.11 client |
| [Playwright](https://playwright.dev) | Microsoft, Apache 2.0 | Browser verification tooling |
| Microsoft Foundry, Azure Speech, Voice Live, Container Apps, Blob Storage | Microsoft services | Agents, speech, hosting and persistence |