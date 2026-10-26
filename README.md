# Voice of the Game

**Accessibility-first, multilingual AI match commentary**: an entry for the Microsoft × Premier League *Inside the Game* hackathon.

Voice of the Game turns a synthetic football match into live audio description, multilingual commentary, captions, a Calm mode, and an on-demand match companion. The browser listener includes a live pitch map; a dedicated broadcast overlay and Studio remain planned.

- Research and idea selection: [IDEAS.md](IDEAS.md)
- Full build plan: [BUILD_PLAN.md](BUILD_PLAN.md)
- Accessibility implementation ledger: [please-reasearch-online-then-mighty-hanrahan.md](please-reasearch-online-then-mighty-hanrahan.md)
- Actual-device demo and synthetic data card: [DEMO.md](DEMO.md)

> The submitted app uses synthetic matches with fictional generated leagues, clubs and players. Historical charts and the public `/data` proxy have been removed from the current worktree; no real historical data is used in the submitted app. The legacy DataPrep utility and private historical blobs remain unused, and earlier real-data usage remains credited below. No actual club data, trademarks or footage are used in the simulation. Synthetic-only data does not by itself establish full hackathon eligibility.

## What works today

Implementation status updated on **26 October 2026**: scoped recaps are deployed, all three Foundry companions are registered, and local/deployed browser checks passed as detailed below. The listener is static JavaScript served by the ASP.NET Core gateway, using self-hosted SignalR with JSON and AudioWorklet playback. It is not a Blazor app and does not use Azure SignalR, Fabric or hosted-agent workflows. Live configuration still exposes nine channels: English, Spanish and Tamil, each with AudioDescription, Commentary and Calm modes. The new Casual/Analyst insight views are separate from these listening channels and from the additional offline CLI modes.

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
| Ask about the match | Foundry agent `vog-match-companion-<lang>` + `Vog.Companion` | Tap-to-ask with local silence auto-stop, optional hold-to-talk, or typed questions through Azure Voice Live in agent mode. The gateway exposes six read-only MCP tools at `/mcp/<lang>`, including `get_match_recap`; all three registered agents have the updated allowlist and recap grounding prompt. Commentary is muted while recording and ducked during answers. |

The browser also provides independent commentary/answer/event/ball/crowd levels, ball sonification, supported-device haptics, sound learning, clear-speech and gentle-sound settings, sound tags and an atmosphere meter. Caption size, colours, opacity, outline, font and position are configurable; preferences are stored on the device. The pitch map has zoom, follow-ball, shape/number markers and a polite position description. Calm changes commentary cadence and pass filtering; crowd and gentle-sound settings are independent.

Still planned: hosted Foundry workflows (Insight Desk, workflow-based recap, transcreation and spoken post-answer catch-up), Fabric, a dedicated overlay/Studio, optional Blazor migration, Easy-read captions, Immersive Reader and hands-free live-reference echo cancellation. Deterministic scoped recaps and local text catch-up are implemented as described below, not as hosted workflows. Backend braille briefs/text groups exist, but the listener has no braille output-mode control or live braille feed. UI presence is not proof of assistive-technology compatibility or accessibility compliance.

### Pending-Feature Batch: Local Source Only

The following changes are implemented and tested locally on 26 October 2026, but **not committed or deployed**. The deployed application remains `8cf6074`.

- Speech workers now use the existing 64-entry priority-aware queue. Overflow rejects/evicts an utterance, records a server skip and sends the browser a skip notification. Even critical-only overflow remains bounded by retaining newer calls. Failed synthesis gets at most one retry on a fresh synthesizer, only before any nonempty audio was emitted; partial audio is never replayed. Warm-up keeps retrying with cancellation. Per-utterance PCM chunk buffering remains unbounded, and the existing `skippedStale` metric also includes overflow skips.
- Post-answer **text-only** catch-up captures received commentary from question activation/submission through answer playback completion. It prioritizes goals/cards, retains up to six major moments, labels source language, and distinguishes empty, truncated and failed intervals. It cannot report events absent from received commentary. It adds no model calls and does not speak or replay commentary. Explanatory catch-up text remains English.
- Overlapping questions are blocked. Stop, channel change and reconnect discard catch-up and stale answer callbacks; channel changes establish a fresh connection. A 120-second watchdog bounds stalled interactions. After a sent question fails or times out, Stop/Start is required before asking again because responses lack server-side question correlation tokens. This deliberately conservative recovery remains a UX limitation.
- Recap controls and generated labels follow English, Spanish or Tamil selection. Spanish/Tamil translations are explicitly draft; server facts, explanations and limitations remain English with accurate language tags. Focus and open source evidence survive label changes.
- Validation: **132 .NET Release tests** and **24 Node browser tests** passed, zero failures/skips. Local browser fixtures verified all three recap languages at widths 1280/390/320, open evidence and source-language tags, with no horizontal overflow or captured page errors. Fixtures were removed; these checks are not live Azure acceptance. Mobile screenshot output remains compressed, so full visual/native-language/assistive-technology review is still required.
- Local gateway launch is blocked by missing `VOG_KEY_VAULT_URI` and local configuration. Do not provision again just to restore developer settings. A static frontend-only preview is running at `http://127.0.0.1:5093/`; it has no SignalR, recap/history API or companion backend.

Run the new checks with `dotnet test --solution VoiceOfTheGame.slnx --configuration Release --artifacts-path TestResults/pending-features` and `node --experimental-vm-modules --test tests/browser/insights.test.mjs tests/browser/catchup.test.mjs`.

### Scoped Insights: Current Worktree

The public, `no-store` endpoint `/insights?scope=recent|match|halftime|fulltime&lang=en-GB|es-ES|ta-IN` uses the same `get_match_recap` MCP tool over a single immutable snapshot per recap. Recaps report deterministic raw key-event counts, source event IDs, goal score effects and dismissal explanations. Scores are taken at the scope end. They do not infer window xG, window possession, tactics, momentum or probabilities.

- `recent` covers at most two match-clock minutes, clamped within the current half; `match` covers the match so far.
- `halftime` is unavailable until the first half ends, then remains first-half-only. `fulltime` is unavailable until the final whistle.
- Local panel controls support English, Spanish and Tamil (draft translations); server-authored evidence remains English, with that limitation disclosed. The deployed `8cf6074` panel is still English-only. Casual selects up to five decisive/latest highlights; Analyst shows the last 40 highlights and a raw event-count table. Native shot meters and expandable source event IDs expose the evidence.
- Auto-refresh runs every 15 seconds, pausing in hidden tabs or while focus is in the panel or evidence is open. The nine live listening channels and natural defaults remain unchanged: seed `20261026`, speed `1`, break `120` seconds.
- Reported full-solution Release result: **128 tests passed**, including **23 companion** and **15 gateway** tests. Six new recap tests cover actual goal-event evidence/deduplication, halftime isolation, availability validation, second-half boundaries, inclusion of the final event in fulltime counts, and empty intervals. These are test results, not deployed-browser acceptance.

### Latest Recap Release Verification: 26 October 2026

Live app: https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/?release=recaps-8cf6074

- Source **`8cf6074`**, following recap checkpoint **`db16cb8`**, fixes tab-return refresh so focused/open evidence stays stable. ACR run **`dtq`** built a clean Git archive; immutable image `crvogdevy6po56.azurecr.io/vog-gateway@sha256:b90a7eeb4363d94d1f493de2f2102e8175880038ffc2176eb8c4ae2f99c1ab43` was created at `2026-10-26T06:51:43Z`.
- Revision **`ca-vog-dev-gateway--recaps-8cf6074`** is Healthy, latest-ready, one replica, 100% traffic in Single revision mode. Image-only rollout preserved configuration, secrets and infrastructure; no playback-default overrides were present.
- Authenticated deployed MCP lists six tools. Foundry versions: **en-gb 11**, **es-es 4**, **ta-in 3**, with `gpt-5.6-terra`, reasoning `none`, six tools and the existing guardrail. A direct English agent request confirmed `get_match_recap({"scope":"match"})` succeeded.
- Fresh full-solution Release suite: **128 passed, zero failed or skipped**. The initial interrupted run was discarded; the complete rerun passed. These results precede this documentation-only commit.
- Deployed `run_playwright_code`, without local asset overrides, verified Casual/Analyst views, persistence, keyboard evidence and no automatic requests across separate 16-second focused/open-evidence checks, including `visibilitychange`. Injected HTTP 503 produced a retry message; removing the route restored both team rows. All test routes/instrumentation were removed and listening stopped.
- `/healthz` returned 200; all four valid recap scopes returned 200 with `no-store`; invalid scope/language returned 400; both removed chart routes returned 404. At 08:53, recent covered exactly 120,000 ms within half one; halftime/fulltime were correctly unavailable. Local late-match checks confirmed halftime remained first-half-only. Final-whistle boundaries are covered by deterministic tests, not a completed deployed-match observation.
- English AD sample: **101 PCM chunks, six captions, five playback acknowledgements**, with state speed `1`. The typed Voice Live recap said 0-0 at 10:48, Rafael Zandor's sole Blues shot was saved and Reds had no shots; before/after deployed snapshots at 10:40/10:51 confirmed the same facts and source event `m-20261026-e00025`. The answer delivered 25 audio chunks with no captured answer error.
- Actual widths 320/390/1280 had document widths 305/375/1265, no horizontal overflow and nonblank pitch pixels. Screenshot output remained cropped/compressed; full visual acceptance is not claimed.

At the user's request, Accenture author/committer emails on `main` were rewritten to `danielmelchizedekarockiathomas@gmail.com` and pushed with an explicit lease. All 31 commit trees, names, dates and subjects were verified unchanged. Historical hashes below are pre-rewrite identifiers; the old recap checkpoint `145bb5b` is now `db16cb8`. [NEXT_SESSION.md](NEXT_SESSION.md#resume-steps) records remaining acceptance work.

Native-language review (Spanish/Tamil and recap responses), human screen-reader/co-design validation, actual microphone use, subjective audio/caption timing, concurrent multilingual playback, and a completed-match archive write/read round trip remain open. Use [DEMO.md](DEMO.md) for a target 110-second demonstration without promised goals or manufactured match timing.

### Historical Browser-Fix Release: 23 October 2026

This dated release record is superseded by [the recap release verification](#latest-recap-release-verification-26-october-2026). Its hashes are pre-email-rewrite identifiers and its chart checks are historical.

The user subsequently authorized commit, push and deployment of the reviewed fixes. Application commit **`1d5ab9a`** was pushed to `origin/main` and built from a clean Git archive by ACR run **`dtp`**. The image-only update deployed `crvogdevy6po56.azurecr.io/vog-gateway@sha256:2bae9f40390d1da12522b5e37fc2534322667d28af50dbb3d47bb6b691c5950a` as revision **`ca-vog-dev-gateway--browserfix-1d5ab9a`**. Azure confirmed Healthy, latest-ready, one running replica and 100% traffic in Single revision mode. Infrastructure, secrets and Foundry registrations were not changed.

Live app: https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/?release=browserfix-1d5ab9a

Fresh deployed Playwright checks, with no local asset overrides, verified the corrected footer, no horizontal overflow at actual widths 320/390/1280 with settings and charts expanded, two 16-row accessible tables, nonblank pitch/chart canvases, all three entry-tag translations and retained English row language after switching channels. The sample captured 91 PCM chunks, six caption messages and five playback acknowledgements; the typed companion answered Reds 0-0 Blues at 21 seconds, consistent with the clock. No runtime errors were captured; `/healthz` returned 200. Existing-tab JavaScript caching required a cache-disabled reload; hard-refresh old tabs after deployment. Browser routing was removed and listening stopped after verification.

The 15 passing gateway tests immediately before release still apply. Full screenshot acceptance, microphone input, subjective audio quality, concurrent multilingual audio and complete accessibility validation remain unverified. Earlier source-only and Azure records below are historical; this release supersedes their undeployed-fix status and active-revision claims.

### Historical Source Review And Browser Fixes: 23 October 2026

A subsequent code/documentation review and live Playwright session found and fixed four browser issues locally: horizontal overflow at 320px (including hidden chart-table sizing), an English-only stadium-entry sound tag, retained commentary inheriting the newly selected channel's language, and a footer incorrectly claiming all data was synthetic. The entry tag now uses the existing English, Spanish and Tamil phrase-bank wording; each history row keeps its own language. The footer distinguishes fictional live matches from real historical results.

- The unmodified live app delivered English PCM, captions and playback acknowledgements. A typed companion answer matched the scoreboard at 77 minutes, 0-0. All nine language/mode selections settled successfully; this was sequential selection, not concurrent multilingual audio testing. No runtime errors were captured in the instrumented sample.
- Local HTML/CSS/JavaScript were tested through Playwright asset overrides against the live backend, not deployed. Checks passed at actual viewport widths 320, 390 and 1280 with no horizontal overflow, both accessible tables retaining 16 rows, nonblank canvases, all three entry-tag translations, retained English history language after a Spanish switch, preference persistence and keyboard zoom/follow controls.
- Fresh Release gateway tests: **15 passed, zero failed or skipped** on SDK 10.0.400. Editor diagnostics and whitespace checks passed. This was not a full-solution run, microphone test, subjective audio review or accessibility audit. Screenshot capture changed the effective viewport and returned inconsistent crops; full visual screenshot acceptance remains unverified.
- The live history panel now displayed two archived Norton Borough 0-1 Brackworth Town results. This confirms readable records, not an observed completed-match archive write/read round trip.
- No commit, push, Azure deployment, infrastructure change or secret change was performed in this review. Azure revision/traffic metadata below remains the earlier recorded release state. Historical-data eligibility and natural short-demo scoring remain unresolved.

### Historical Chart Azure Release: 23 October 2026

The chart/data endpoints and eligibility actions below describe that older release only; the deployed recap release has removed the charts and public proxy. Its checks do not validate the recap release.

Live app: https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/?release=charts-bottom-20261023

- Source: base `83229d0` includes the 22 October charts/archive/data-prep commit `ab62f5c`; `0fc5655` contains the storage/mobile fixes and release documentation. Commit `8ede73d` contains the chart-placement follow-up and its deployed verification record: the unchanged charts sit below captions and the companion, immediately before latency. The documentation review confirmed `main` and local `origin/main` both point to `8ede73d`; it did not fetch the remote or recheck Azure.
- Recorded final revision: `ca-vog-dev-gateway--charts-bottom-20261023`; image `crvogdevy6po56.azurecr.io/vog-gateway@sha256:4227b9d4ce20e956eb10079ea327ae77694a3d23b832d8ded5d6a7a8451c08d6`. At release verification, Azure confirmed it was the latest ready revision and received 100% of traffic, in single-revision mode with one 0.5-vCPU/1-GiB replica. Foundry registrations and secrets were not changed.
- Playback remains seed `20261026`, speed `1`, and a `120`-second between-match break. No forced goals, join-triggered resets, highlights or new analytics workflow was introduced.
- Storage account `stvogdevy6po56` contains private `vog-data`; shared-key authentication and anonymous blob access are disabled. Gateway and developer access are container-scoped. The gateway now reads the normalized `VogStorage:BlobEndpoint` key, with a literal local-setting fallback.
- Both `/data/goals-per-decade.json` and `/data/home-advantage.json` return 200 with 16 decades. Chart.js canvases and screen-reader tables populate. Desktop chart pixels were verified; the final deployed 390x844 mobile check also verified nonblank charts and no horizontal overflow after fixing min-content sizing.
- Validation: Bicep compilation passed; all **15 gateway tests** passed, including three storage-configuration tests; editor diagnostics and `git diff --check` passed. This is not a fresh full-solution or accessibility-compliance certification.
- Final-release Playwright checks confirmed charts immediately before latency at 1280x900 and 390x844, nonblank canvases, 16 rows per accessible table and no horizontal overflow. English AD delivered 15 PCM chunks, captions and a playback-start acknowledgement at 1x; the typed score answer matched Reds 0-0 Blues, 54 minutes. No runtime errors were captured. This is not subjective audio-quality, concurrent multilingual or completed-match archive validation. The raw CSV route correctly returned 404.
- At release verification, `/history` returned an empty array. This can mean no archived matches, disabled storage or a storage-read failure; it is not evidence of archive health or a tested archive write.
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

In addition to the .NET SDK, install the [Azure CLI](https://aka.ms/installazurecli), PowerShell 7 and Git on PATH. Run provisioning from the repository root with an interactive user signed in through `az login`; that user needs permission to deploy resources and create role assignments.

```powershell
./infra/provision.ps1        # everything in rg-vog-dev (Sweden Central), then builds and deploys the gateway:
                             #   Foundry resource + project (keys disabled), gpt-5.6-terra, agent guardrail;
                             #   Key Vault, container registry, Container Apps, Log Analytics + App Insights;
                             #   Blob Storage with private vog-data and container-scoped gateway/developer roles;
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
dotnet run --project tools/Vog.Replay -- ask --url http://localhost:5080              # through a local gateway, typed and spoken
```

For the deployed gateway, replace the URL with the provisioning output `VOG_GATEWAY_URL`. Provisioning writes it to the local dotenv file but does not export it into your shell; the `ask` command does not load that file when choosing its URL.

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
  Vog.Pipeline       offline and wall-clock live composition, shared match schedule, golden match
  Vog.Voice          Azure neural TTS, captions, WAV, word-boundary cuts
  Vog.Companion      match tools (read-only, grounded in the live match) and the Voice Live agent-mode session
  Vog.Gateway        live broadcast, listener, companion/MCP, scoped insights and best-effort archive/history
foundry/agents/      Foundry agent definitions (instructions, tools, reasoning effort), reviewed like code
tools/Vog.Replay     CLI: commentary playback, coverage report, data card, speech rendering, gateway smoke tests
tools/Vog.FoundrySetup  registers foundry/agents/*.agent.yaml as new Foundry agent versions
tools/Vog.DataPrep   unused legacy utility for historical-chart JSON blobs; not part of the submitted app
infra/               Bicep + Azure CLI provisioning and deployment (provision.ps1, teardown.ps1)
tests/               xUnit v3 test projects
```

## Secrets and personal data

Nothing sensitive belongs in this repository (BUILD_PLAN.md §12):
- The `.gitignore` excludes azd state, env files (including `.env.local`), local settings, keys, workflow checkpoints, generated audio and co-design data.
- The AI resource has key authentication disabled. Gateway, Replay and FoundrySetup use Azure CLI credentials locally and the configured managed identity in Azure. DataPrep uses `DefaultAzureCredential`; its selected identity needs access to `vog-data`.
- The application-managed MCP secret is persisted in Key Vault and the Foundry connection and loaded into gateway configuration at startup. It is excluded from agent definitions and generated local settings. Provisioning temporarily writes it to a parameters file and attempts deletion in `finally`. It reuses the stored key when retrieval succeeds; failed retrieval can currently generate a replacement. Infrastructure separately uses a Log Analytics shared key.
- CI runs a gitleaks secret scan. Locally, install [gitleaks](https://github.com/gitleaks/gitleaks) and run `gitleaks git` before pushing.

## Credits and attributions

| Work | Author / Licence | How it is used |
|---|---|---|
| [International football results from 1872 to 2017](https://www.kaggle.com/datasets/martj42/international-football-results-from-1872-to-2017) | Mart Jürisoo, **CC0 Public Domain** | Previously aggregated into goals-per-decade and home-win JSON for the removed historical panel. Retained private blobs/legacy DataPrep are unused by the submitted app; credit records that real-data usage. |
| [football-results-dashboard](https://github.com/bbenz/football-results-dashboard) | Brian Benz, **MIT** | Historical inspiration for the removed chart experiment; not the current synthetic recap implementation |
| [Chart.js](https://www.chartjs.org) | Chart.js contributors, **MIT** | Previously rendered the removed historical charts; current insights use native meters and tables |
| [Atkinson Hyperlegible](https://brailleinstitute.org/freefont) | Braille Institute, **SIL OFL 1.1** | Optional accessible caption font (self-hosted; licence in `fonts/atkinson-hyperlegible/`) |
| [Microsoft Playwright](https://playwright.dev) | Microsoft, **Apache 2.0** | Browser-based testing via the Playwright MCP server |
| [SignalR](https://github.com/dotnet/aspnetcore) | Microsoft, **MIT** (part of ASP.NET Core) | Real-time browser communication for audio, captions and pitch data |
| Microsoft Foundry, Azure AI Speech, Azure Voice Live, Azure Container Apps, Azure Blob Storage | Microsoft | AI agents, neural TTS, real-time voice Q&A, hosting, and match/data archiving |

> **Synthetic data notice:** The current submitted experience uses fictional, machine-generated matches, leagues, clubs and players, not real historical data. Historical charts and the public `/data` proxy are removed. Prior real CC0 usage is credited above; unused legacy tooling/private blobs are retained. No actual club data, trademarks or footage are used in the simulation. Synthetic-only data does not establish full eligibility.

## Legacy Historical Data And Storage

`infra/modules/storage.bicep` creates a private `vog-data` container with shared-key authentication disabled. The gateway identity and provisioning developer receive container-scoped Storage Blob Data Contributor access. `infra/main.bicep` supplies `VogStorage__BlobEndpoint` to the gateway and outputs it for local configuration.

Legacy DataPrep and existing historical blobs are retained unused. They are not setup prerequisites for the submitted app. The removed panel previously consumed `public/goals-per-decade.json` and `public/home-advantage.json` from private storage through an allowlisted public proxy. The current gateway no longer exposes `/data/{file}` and the listener no longer renders those charts. Historical home-win percentage counted home-team wins across all dataset matches, including neutral fixtures.

DataPrep still accepts `--storage <endpoint>` or the process environment variable `VogStorage__BlobEndpoint`; it does not load the repository dotenv file. This describes retained tooling, not a request to upload real data or re-enable the removed proxy. `/history` remains public even though its backing blobs disallow anonymous access.

The historical 23 October 2026 preparation processed 49,945 matches with no skipped rows and produced 16 decades per chart. All four user-supplied CSV files were SHA-256-identical to the public Kaggle download. Only results fed those charts; scorer, shootout and former-name datasets were unused. Raw CSV data was not checked into Git or served by the gateway. These were real aggregates, not synthetic data or proof of eligibility; they are no longer used by the submitted app.

After a simulated match completes, the gateway attempts a best-effort upload of a lightweight summary under `match-snapshots/`. Uploads run without awaiting completion and failures are logged without retry. `/history` returns up to ten successfully read summaries, newest blob modification first. An empty array can mean no archived matches, disabled storage or a storage-read failure; a completed-match write/read round trip remains unverified.

## Demo configuration

Source defaults remain seed `20261026`, speed `1.0`, and a `120`-second between-match break; the recap rollout preserved these defaults and live state confirmed speed `1`. [DEMO.md](DEMO.md) budgets 110 seconds on an actual device without promising goals during the recording. The submission video must remain under two minutes; changing realism or the recording format requires an explicit decision.

The source code defaults (`appsettings.json`) use `Seed: 20261026, Speed: 1.0` for natural 1× pacing with full narration for all modes.

Override locally:

```powershell
# Natural 1× (full commentary, first goal ~81 real minutes):
dotnet run --project src/Vog.Gateway

# Optional acceleration; not the deployed default or a scoring guarantee:
dotnet run --project src/Vog.Gateway -- --Broadcast:Seed=3 --Broadcast:Speed=6 --Broadcast:BreakSeconds=30
```
