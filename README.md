# Voice of the Game

**Accessibility-first, multilingual AI match commentary**: an entry for the Microsoft × Premier League *Inside the Game* hackathon.

Voice of the Game turns a synthetic football match into live audio description, multilingual commentary, captions, a Calm mode, and an on-demand match companion. The browser listener includes a live pitch map; a dedicated broadcast overlay and Studio remain planned.

- Research and idea selection: [IDEAS.md](IDEAS.md)
- Full build plan: [BUILD_PLAN.md](BUILD_PLAN.md)
- Accessibility implementation ledger: [please-reasearch-online-then-mighty-hanrahan.md](please-reasearch-online-then-mighty-hanrahan.md)

> The live match is synthetic. Its league, clubs and players are fictional and generated, with a deny-list of real club names. The separate historical chart panel currently uses real CC0 international results. The hackathon requires synthetic, football-realistic data, so this panel's eligibility is unresolved; CC0 licensing is not eligibility approval. Keep accessible synthetic live-match commentary as the project's purpose. No real Premier League data, trademarks or footage are used in the live simulation.

## What works today

Implementation and deployment status updated on **23 October 2026**. The listener is static JavaScript served by the ASP.NET Core gateway, using self-hosted SignalR with JSON and AudioWorklet playback. It is not a Blazor app and does not use Azure SignalR, Fabric or hosted-agent workflows. Live configuration exposes nine channels: English, Spanish and Tamil, each with AudioDescription, Commentary and Calm modes. The additional CLI modes below are offline options.

The offline **HOT path** (BUILD_PLAN.md §3) runs end to end on virtual time, with no cloud services needed:

| Stage | Project | What it does |
|---|---|---|
| (a) Ingest | `Vog.Simulator` | A seeded, deterministic 4-3-3 match simulator: 10 Hz tracking, match events, and the scene details an audio describer needs (kits, weather, celebrations, crowd, VAR) |
| (b) Interpret | `Vog.Stats` | Speed, distance and sprint tags from tracking; pass difficulty with its causes; xG; ball and shot speed; live ball state with restarts inferred from the laws of the game |
| (c) Describe | `Vog.Describe` | Language-neutral utterance intents, realized through reviewed phrase banks (`en-GB` reviewed; `es-ES` and `ta-IN` (Tamil) **draft**: they need native-speaker review before use on air) |
| (d) Schedule | `Vog.Booth` | The **Booth Producer**: decides what is spoken when on a serial audio channel, using priorities, preemption, staleness rules, location obligations and dead-ball colour |
| Composition | `Vog.Pipeline` | Runs everything together. Intents are computed once and rendered per channel (language × mode). |
| Speech and captions | `Vog.Voice` | Azure neural voices authenticated with Microsoft Entra ID, 24 kHz PCM streaming and word-timed caption generation (WebVTT, at most 2 lines of 37 characters). Offline utilities support word-boundary cuts; live playback uses a sample-limit fade for interruption. |
| (e) Render, live | `Vog.Gateway` | Plays a seeded synthetic match at natural 1x speed by default, on a shared wall-clock schedule. Full mode-specific narration streams as audio with word-timed captions. The listener page plays audio through an AudioWorklet, with procedural earcons panned to where the action is. |
| Ask about the match | Foundry agent `vog-match-companion-<lang>` + `Vog.Companion` | Tap-to-ask with local silence auto-stop, optional hold-to-talk, or typed questions through Azure Voice Live in agent mode. A versioned Foundry prompt agent uses five read-only MCP tools at `/mcp/<lang>` to ground answers in match data. Commentary is muted while recording and ducked during answers. |

The browser also provides independent commentary/answer/event/ball/crowd levels, ball sonification, supported-device haptics, sound learning, clear-speech and gentle-sound settings, sound tags and an atmosphere meter. Caption size, colours, opacity, outline, font and position are configurable; preferences are stored on the device. The pitch map has zoom, follow-ball, shape/number markers and a polite position description. Calm changes commentary cadence and pass filtering; crowd and gentle-sound settings are independent.

Still planned: hosted Foundry workflows (Insight Desk, recap, transcreation and post-answer catch-up), Fabric, a dedicated overlay/Studio, optional Blazor migration, Immersive Reader and hands-free live-reference echo cancellation. Backend braille briefs/text groups exist, but the listener has no braille output-mode control or live braille feed. UI presence is not proof of assistive-technology compatibility or accessibility compliance.

### Current Azure Release: 23 October 2026

Live app: https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/?release=charts-mobile-20261023

- Source: base `83229d0` includes the 22 October charts/archive/data-prep commit `ab62f5c`. The deployed image additionally includes the storage-configuration and mobile-chart fixes committed alongside this release record; it is not an image of unchanged `83229d0`. Committing these already-deployed fixes does not require another rollout.
- Final revision: `ca-vog-dev-gateway--charts-mobile-20261023`; image `crvogdevy6po56.azurecr.io/vog-gateway@sha256:ceee86060ce9f0f1cd5e1e71211d407fc420112640de63327538b001bbc82920`. Azure confirms it is the latest ready revision and receives 100% of traffic. The app retains single-revision mode and one 0.5-vCPU/1-GiB replica. Foundry registrations and secrets were not changed.
- Playback remains seed `20261026`, speed `1`, and a `120`-second between-match break. No forced goals, join-triggered resets, highlights or new analytics workflow was introduced.
- Storage account `stvogdevy6po56` contains private `vog-data`; shared-key authentication and anonymous blob access are disabled. Gateway and developer access are container-scoped. The gateway now reads the normalized `VogStorage:BlobEndpoint` key, with a literal local-setting fallback.
- Both `/data/goals-per-decade.json` and `/data/home-advantage.json` return 200 with 16 decades. Chart.js canvases and screen-reader tables populate. Desktop chart pixels were verified; the final deployed 390x844 mobile check also verified nonblank charts and no horizontal overflow after fixing min-content sizing.
- Validation: Bicep compilation passed; all **15 gateway tests** passed, including three storage-configuration tests; editor diagnostics and `git diff --check` passed. This is not a fresh full-solution or accessibility-compliance certification.
- English live audio/captions and a score-grounded typed companion answer were verified earlier in this release session. The final frontend follow-up reconnected the listener and received scoreboard/pitch state; subjective audio quality, concurrent multilingual playback and a completed-match archive round trip were not revalidated.
- `/history` currently returns an empty array. This is expected for a new archive until a full synthetic match completes, not evidence of a tested archive write.
- Submission gate: the real historical dataset must not be described as synthetic or proven eligible. Before submission, resolve whether the panel must be removed from the entry or replaced with honestly labeled simulator-derived data. Three natural goals within 120 seconds also remain unresolved.

### Historical Azure snapshot: 21 October 2026

The following inventory is retained as a dated historical observation. It is superseded by the 23 October release above and must not be used as the current deployment inventory.

Live app: https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/

Read-only Azure CLI queries confirmed the following deployment, separately from the current source:

| Item | Observed value |
|---|---|
| Resource group / region | `rg-vog-dev` / Sweden Central |
| Active revision | `ca-vog-dev-gateway--0000015`, Healthy; created 7 October 2026 |
| Image | `crvogdevy6po56.azurecr.io/vog-gateway:0a9f32f-evalfix-20261007` |
| Gateway scale / resources | Single revision; min/max replicas 1/1; 0.5 vCPU, 1 GiB memory |
| Ingress / identity | External, port 8080; user-assigned managed identity |
| Foundry model | `gpt-5.6-terra`, version `2026-07-09`, `GlobalStandard`, capacity 800, `NoAutoUpgrade`; deployment Succeeded |
| Probes | Readiness and liveness both use `/healthz` |

The resource-group inventory contains the Foundry account/project, managed identity, Key Vault, registry, Container Apps environment/gateway, Log Analytics, Application Insights and its smart-detection action group. No Fabric, Event Hubs, Azure SignalR, Cosmos DB or storage-account resources appeared in this resource group. This is not a subscription-wide inventory, nor proof that the deployed image matches current source. `/healthz` returned `{"status":"ok"}`; it is a process probe, not a Speech/Foundry dependency check.

The public `/stats` snapshot retrieved during this review reported the following timings in milliseconds. These are accumulated runtime observations, not a controlled benchmark or a new test run:

| Metric | Count | p50 | p95 |
|---|---|---|---|
| Server first audio chunk | 240 | 72 | 244 |
| Client receive to playback | 226 | 444 | 1305 |
| Companion question to first audio | 5 | 3562 | 7335 |

The same snapshot reported 0 server and 13 client stale skips, 5 companion answers, 3 tool calls, 5,524 input tokens and 530 output tokens. Five answers are too few to establish a companion latency service level.

### Sample: audio-description channel, golden match

```text
[ 78:58] Talez swings the corner in.
[ 78:59] Blues with it, in the box.
[ 79:04] Feric receives from Kirye, in the box.
[ 79:07] Feric to Zandor, in the box.
[ 79:08] It's in! Zandor! Reds 0, Blues 1.
[ 79:11] Zandor wheels away, arms wide.
[ 79:13] A VAR check, and the goal stands.
[ 79:15] Reds 0, Blues 1, 79 minutes gone.
[ 79:20] On screen: shot speed, 107 kilometres an hour.
```

### Recorded offline quality

The recorded golden-match results below cover deterministic scheduling and coverage, not all accessibility targets in BUILD_PLAN.md §17. Browser accessibility, co-design outcomes, real playback/caption alignment, load and transcription accuracy require separate validation. These results were not rerun during the documentation review:

| Check (audio description, en-GB) | Result | Target |
|---|---|---|
| Ball-location mention gap in open play, p50 / p95 | 2.8 s / 4.0 s | ≤ 3 s / ≤ 5 s |
| Dead air in open play | 0 % | ≤ 5 % |
| Urgent (goal, shot, tackle…) event → speech, p95 | 1.0 s | ≤ 1.5 s |
| Open-play pass calls → speech, p95 | 2.4 s | ≤ 3 s |
| Dead-ball calls (restarts, substitutions) → speech, p95 | 4.9 s | ≤ 8 s |
| Overlapping utterances / urgent items lost | 0 / 0 | 0 / 0 |

Earlier `vog-replay speak` calibration recorded spoken/estimated median durations of 1.01 for English audio description and 1.05 for Tamil. An earlier queued-synthesis experiment recorded a median 36 ms added latency versus about 600 ms on that development network. The current live `SpeechWorker` starts synthesis after Booth dispatch; pre-synthesis while waiting in the Booth queue remains planned.

The simulator passes its **synthetic data card** over 20 seeds. Means: 2.5 goals (2.6 xG), 25 shots, 81% pass completion, 61 minutes of ball in play, 10 km and 34 sprints per outfield player, plus realistic fouls, cards, corners, throw-ins and goal kicks.

## Run it

Prerequisite: the [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned in `global.json`).

```bash
dotnet test                                   # repository tests; no fixed test count

dotnet run --project tools/Vog.Replay -- commentary                    # full match, English audio description
dotnet run --project tools/Vog.Replay -- commentary --lang es-ES       # Spanish (draft phrase bank)
dotnet run --project tools/Vog.Replay -- commentary --mode calm --from 70 --to 80
dotnet run --project tools/Vog.Replay -- commentary --quiet --latency  # coverage report + latency by template
dotnet run --project tools/Vog.Replay -- datacard --seeds 20           # simulator realism report
dotnet run --project tools/Vog.Replay -- goaltiming --seeds 100 --seed 1 # raw natural-speed goal timestamps; at most 100 seeds
```

Modes: `ad` (audio description), `commentary`, `calm`, `analyst`, `casual`, `kids`.

### With Azure: real voices and captions

Prerequisites: the [Azure CLI](https://aka.ms/installazurecli) and PowerShell 7, signed in with `az login`.

```powershell
./infra/provision.ps1        # everything in rg-vog-dev (Sweden Central), then builds and deploys the gateway:
                             #   Foundry resource + project (keys disabled), gpt-5.6-terra, agent guardrail;
                             #   Key Vault, container registry, Container Apps, Log Analytics + App Insights;
                             #   registers the Foundry agents; writes .env.local (git-ignored, no secrets)
./infra/provision.ps1 -SkipBuild  # skips image build; still deploys infrastructure, writes settings and registers agents

dotnet run --project tools/Vog.Replay -- speak --from 78 --minutes 2   # out/audio/*.wav + word-timed *.vtt
dotnet run --project tools/Vog.FoundrySetup                            # re-register agents after editing foundry/agents/*.yaml
dotnet run --project tools/Vog.FoundrySetup -- --ask en-GB "Who scored?"   # ask a Foundry agent directly

./infra/teardown.ps1         # deletes the resource group and purges the soft-deleted AI account and key vault
```

Model use is pay-per-token. The gateway runs one always-on replica (about 0.5 vCPU), because the live match and listener connections are in memory and Foundry must be able to reach its MCP endpoint whenever a fan asks a question.

### Live: listen in a browser

```powershell
dotnet run --project src/Vog.Gateway                                  # http://localhost:5080
dotnet run --project tools/Vog.Replay -- listen --seconds 30          # smoke test: joins a channel like a browser
```

Open http://localhost:5080, pick a language and mode, and select **Start listening**. Headphones help: earcons are panned to where the action is, as seen from the main stand. The gateway needs the settings in `.env.local` (or the same names in its environment) and won't start without them. Live utterances are dispatched to active audio channels, rather than synthesized per listener. Workers warm on join, and warm-up or already queued work can continue after listeners leave; this is not a strict no-listener cost guarantee.

The app defaults to seed **20261026**, natural **1x match-clock speed**, and a **120-second between-match break**. Full mode-specific narration and normal speech preemption are enabled. The pitch map uses uniform metres-to-screen scaling and a stable 28 CSS-pixel yellow ball with a separated white outline and dark borders. There is no separate demo mode, highlights playback or injected scoring.

Optional acceleration remains explicit through `Broadcast__Speed`; `Broadcast__Seed` and `Broadcast__BreakSeconds` can also override the defaults. Above 4x, spoken commentary is limited to goals and period changes; this is not equivalent to detailed real-time audio description. The full text-only stream and match data remain available. Companion MCP grounding must reference the same configured match and schedule as the listener. Azure now runs the natural-speed correction; a differently configured local gateway is not automatically synchronized with it.

**Three natural goals within 120 real seconds remain unresolved.** The offline `goaltiming` command records raw `MatchEngine.Run()` goal PTS, score changes and the first emitted kickoff without dividing by a speed. Seeds 1-100 had no qualifying opening or later three-goal window: the shortest span was 246,800 ms (seed 36). The default seed has one goal at 4,857,200 ms; seed 3's shortest triple spans 1,836,600 ms at 1x. From simulation start, the 12-second opening delay plus two minimum 55-second post-goal waits already exceed 120 seconds before actual play. No universal impossibility is claimed for a window starting at first kickoff or later, but no candidate was verified. Changing restart rules, realism or recording format needs user approval.

Local corrective verification: isolated Release build and all 119 tests passed; Playwright measured 10 DOM-clock seconds and 10.2 seconds of pitch PTS over 10 wall seconds. An English sample delivered routine narration, captions, PCM and two worklet playback acknowledgements; all nine channel joins confirmed their selected modes. Renderer pixel/interaction checks passed 72 cases across desktop/mobile, DPR 1/2 and zoom/follow combinations. Full screenshot review, concurrent nine-language/mode speech and corrected local Foundry grounding remain unverified. See [NEXT_SESSION.md](NEXT_SESSION.md) for exact checks and release boundaries.

Historical developer-network measurement, from a cold start, English audio description (not rerun in this review):

| Measure | p50 | p95 |
|---|---|---|
| Booth start → first audio chunk sent (server) | 0.59 s | 0.72 s |
| Line received → playback started (browser) | 0.81 s | 1.39 s |

A line that could only start well behind the booth's schedule is skipped, so a hold-up doesn't leave the channel permanently behind: after 1.5 s for most lines, after 4 s for urgent ones, and never for goals and red cards. Skips are counted at `/stats`. The next step is to synthesize lines while they wait in the booth's queue rather than when they start, which S1 measured at a median of 36 ms added.

### Ask about the match (Foundry agents)

On the listener page, select **Tap to ask**, speak, and let local silence detection stop recording, or tap again. Optional hold mode, Alt+Shift+A and typed questions are also available. Score and ball quick-readouts use deterministic match data; Who scored uses the agent. The gateway relays agent questions to Azure Voice Live in agent mode, connected to the channel's Foundry agent (`vog-match-companion-en-gb`, `-es-es`, `-ta-in`). Its read-only MCP tools provide match facts; instructions restrict answers to the match. Post-answer catch-up and hands-free live-reference echo cancellation are not implemented.

```powershell
dotnet run --project tools/Vog.Replay -- companion --say "Who has the ball?"          # straight to the agent over Voice Live
dotnet run --project tools/Vog.Replay -- ask --url $env:VOG_GATEWAY_URL               # through a gateway, typed and spoken
```

Historical Azure measurement (Sweden Central; not rerun in this review). See the dated snapshot above for the observations retrieved on 21 October:

| Measure | p50 | p95 |
|---|---|---|
| Booth start → first speech chunk sent (commentary) | 0.09 s | 0.79 s |
| Question finished → first answer audio (agent and tool call) | 6.1 s | 8.7 s |

Answer latency includes relay, Voice Live, agent and MCP tool work; these aggregate timings do not isolate each component. Bringing it down remains work for the companion. Current speech/companion queues are unbounded, and the 50-question limit is per conversation, resetting on reconnection/language change; it is not durable authentication, rate limiting or cost protection. Queue bounding, session lifecycle and provisioning-secret error handling need later code hardening.

Matches use a fixed wall-clock schedule from 1 October 2026. Gateways can agree on match state only when their seed, speed, break and schedule agree. The companion reads its registered gateway's MCP context; matching state must be checked rather than inferred from the schedule alone.
## Repository layout

```
src/
  Vog.Contracts      shared records: events, tracking, scenes, utterance intents
  Vog.Domain         pitch geometry, fictional league, RNG, pass / xG / threat models
  Vog.Simulator      seeded match engine
  Vog.Stats          interpret stage
  Vog.PhraseBanks    YAML phrase banks per language and persona
  Vog.Describe       describers, realizer, reminders
  Vog.Booth          Booth Producer and coverage metrics
  Vog.Pipeline       offline composition and the golden match
  Vog.Voice          Azure neural TTS, captions, WAV, word-boundary cuts
  Vog.Companion      match tools (read-only, grounded in the live match) and the Voice Live agent-mode session
  Vog.Gateway        live broadcast over SignalR, listener page, companion relay, MCP endpoint for the agents
foundry/agents/      Foundry agent definitions (instructions, tools, reasoning effort), reviewed like code
tools/Vog.Replay     CLI: commentary playback, coverage report, data card, speech rendering, gateway smoke tests
tools/Vog.FoundrySetup  registers foundry/agents/*.agent.yaml as new Foundry agent versions
infra/               Bicep + Azure CLI provisioning and deployment (provision.ps1, teardown.ps1)
tests/               xUnit v3 test projects
```

## Secrets and personal data

Nothing sensitive belongs in this repository (BUILD_PLAN.md §12):
- The `.gitignore` excludes azd state, env files (including `.env.local`), local settings, keys, workflow checkpoints, generated audio and co-design data.
- The AI resource has key authentication disabled. Locally, apps use your Azure CLI sign-in; in Azure, a managed identity.
- The only secret is the key Foundry presents to the gateway's MCP endpoint. It is generated once and kept in Key Vault (the gateway reads it at startup) and in the Foundry project connection, so it never appears in an agent definition, a setting or the repository.
- CI runs a gitleaks secret scan. Locally, install [gitleaks](https://github.com/gitleaks/gitleaks) and run `gitleaks git` before pushing.

## Credits and attributions

| Work | Author / Licence | How it is used |
|---|---|---|
| [International football results from 1872 to 2017](https://www.kaggle.com/datasets/martj42/international-football-results-from-1872-to-2017) | Mart Jürisoo, **CC0 Public Domain** | Pre-aggregated into two JSON files (goals per decade, home-win %) for the "Football through the ages" panel. No individual match rows are redistributed. |
| [football-results-dashboard](https://github.com/bbenz/football-results-dashboard) | Brian Benz, **MIT** | Inspiration for the pattern of aggregating historical football data and serving AI-explained charts on Azure |
| [Chart.js](https://www.chartjs.org) | Chart.js contributors, **MIT** | Renders the historical data charts in the browser |
| [Atkinson Hyperlegible](https://brailleinstitute.org/freefont) | Braille Institute, **SIL OFL 1.1** | Optional accessible caption font (self-hosted; licence in `fonts/atkinson-hyperlegible/`) |
| [Microsoft Playwright](https://playwright.dev) | Microsoft, **Apache 2.0** | Browser-based testing via the Playwright MCP server |
| [SignalR](https://github.com/dotnet/aspnetcore) | Microsoft, **MIT** (part of ASP.NET Core) | Real-time browser communication for audio, captions and pitch data |
| Microsoft Foundry, Azure AI Speech, Azure Voice Live, Azure Container Apps, Azure Blob Storage | Microsoft | AI agents, neural TTS, real-time voice Q&A, hosting, and match/data archiving |

> **Synthetic data notice:** The live match, league, clubs and players are fictional and machine-generated. The separate historical charts use the credited CC0 international football results dataset. No real Premier League data, trademarks, player names or footage are used in the live simulation.

## Historical charts and storage

`infra/modules/storage.bicep` creates a private `vog-data` container with shared-key authentication disabled. The gateway identity and provisioning developer receive container-scoped Storage Blob Data Contributor access. `infra/main.bicep` supplies `VogStorage__BlobEndpoint` to the gateway and outputs it for local configuration.

Download the credited Kaggle dataset separately, then upload and aggregate it:

```powershell
az storage blob upload --account-name <account> --container-name vog-data --name kaggle/results.csv --file <path-to-results.csv> --auth-mode login --overwrite
dotnet run --project tools/Vog.DataPrep --configuration Release -- --storage https://<account>.blob.core.windows.net/
```

The tool writes `public/goals-per-decade.json` and `public/home-advantage.json`. These are private blobs: the gateway exposes only the two allowlisted aggregate files through `/data/{file}`. Expand **Football through the ages** to load both charts and their screen-reader tables. Home-win percentage counts home-team wins across all dataset matches, including neutral fixtures.

The 23 October 2026 data preparation processed 49,945 matches with no skipped rows and produced 16 decades in each chart. All four user-supplied CSV files were SHA-256-identical to the public Kaggle download. Only the results dataset feeds these two charts; scorer, shootout and former-name datasets are not used. Raw CSV data is not checked into Git or served by the gateway. These real aggregates are not established as eligible under the synthetic-data rule.

Completed simulated matches are archived under `match-snapshots/` and exposed through `/history`. A newly provisioned store has no recent matches until a full live match completes.

## Demo configuration

The live deployment uses the source defaults: seed `20261026`, speed `1.0`, and a `120`-second between-match break. It does not promise three goals during a two-minute viewing window. The submission video must remain under two minutes; changing realism or the recording format requires an explicit decision.

The source code defaults (`appsettings.json`) use `Seed: 20261026, Speed: 1.0` for natural 1× pacing with full narration for all modes.

Override locally:

```powershell
# Natural 1× (full commentary, first goal ~81 real minutes):
dotnet run --project src/Vog.Gateway

# Optional acceleration; not the deployed default or a scoring guarantee:
dotnet run --project src/Vog.Gateway -- --Broadcast:Seed=3 --Broadcast:Speed=6 --Broadcast:BreakSeconds=30
```
