# Voice of the Game: build plan

*Target architecture: .NET 10 · Blazor · Microsoft Foundry (Hosted Agents) · Microsoft Agent Framework · Azure Speech and Voice Live · Microsoft Fabric · Azure Container Apps*
*Plan date: 2 October 2026 · Submission target: **26 October 2026** (deadline 27 October, 11:59 PM PT)*

> This document goes with [IDEAS.md](IDEAS.md), which covers the research, the idea catalogue and why idea #4 was chosen.
> Package versions and API names were checked against Microsoft Learn, NuGet and GitHub on 1–2 October 2026. Anything not yet confirmed is listed in [§20 Verify during build](#20-verify-during-build).

## Implementation Status: 21 October 2026

**This is a design plan, not a delivery or compliance report.** Except where explicitly marked built/current, the architecture diagrams, agent roster, UX, Fabric, security controls, deployment examples and acceptance criteria below describe targets. Historical research, package availability and pricing claims were not revalidated in this documentation pass. The following status supersedes earlier present-tense design claims; [README.md](README.md) records the independently queried Azure deployment and dated runtime snapshot.

| Surface | Current source / deployment status |
|---|---|
| HOT path | Deterministic simulator, stats, intents, phrase-bank realization and serial Booth scheduling; no LLM in this path. English reviewed; Spanish/Tamil draft. |
| Listener | Static JavaScript served by ASP.NET Core; self-hosted SignalR with JSON, AudioWorklet PCM playback. Nine configured live language/mode channels; Analyst/Casual/Kids are offline CLI options. No Blazor, Azure SignalR, overlay/Studio or Media Session integration delivered. |
| Accessibility controls | Ball sonification, supported-browser haptics, sound tags/atmosphere, independent mix, clear/gentle sound settings, caption preferences, tap/hold/typed questions, quick score/ball readouts and zoom/follow-ball canvas map. Device, assistive-technology and compliance validation remains outstanding. |
| Companion | One prompt-agent template registered per language; Voice Live agent-mode relay; five read-only gateway MCP tools. Tap with local silence auto-stop is default. No hosted Insight Desk, recap, transcreation or catch-up workflow. |
| Braille / reading | Backend brief/text support is partial; no browser braille output selector/feed. Immersive Reader and hands-free live-reference echo cancellation are not built. |
| Speech | Live synthesis starts after Booth dispatch; caption cues are sent after synthesis completes. Offline timing/word-boundary-cut utilities do not prove live browser alignment or live word-boundary interruption. |
| Provisioning / telemetry | PowerShell 7 + Azure CLI + Bicep, `.env.local` and startup Key Vault loading. Container Apps, registry, identity, Foundry, Key Vault, Log Analytics and Application Insights are deployed. Fabric/Event Hubs/Cosmos/storage/Azure SignalR are not present in the reviewed resource group. No azd/Aspire delivery path implemented. |
| Azure snapshot | Healthy revision `ca-vog-dev-gateway--0000015`, image tag `0a9f32f-evalfix-20261007`, one 0.5-vCPU/1-GiB replica in Sweden Central. Model `gpt-5.6-terra` version `2026-07-09`, GlobalStandard capacity 800, NoAutoUpgrade. The deployed image is not asserted to match current source. |
| Validation / hardening | Recorded golden-match metrics cover scheduling/coverage, not all accessibility goals. `/healthz` is not a dependency probe. Unbounded queues, per-conversation question limits and provisioning-secret error handling still need code hardening. No code, deployment or application tests were changed/run in this documentation pass. |

The actual CI workflow restores, builds and tests in Release, runs a 20-seed data card and commentary check, and scans history with gitleaks. The additional audit/eval/deploy/phrase-bank workflows below are proposals, not a verified CI inventory. See [the accessibility ledger](please-reasearch-online-then-mighty-hanrahan.md) for idea-by-idea delivery status.

---

## 0. Summary

**What it does:** Voice of the Game turns a synthetic football match into **live, accessible commentary**:
- AI audio description for blind and low-vision fans
- multilingual commentary that adapts football idiom to each language
- hands-free voice Q&A
- live captions with sound labels
- a Calm mode and a low-vision mode
- a sighted HTML5 overlay where every cue carries alt text

**Target design, in five parts (steps 4 and 5 remain planned):**
1. A seeded C# simulator streams match events and 10 Hz tracking data.
2. A deterministic **HOT** path turns them into statistics and language-neutral "utterance intents" in milliseconds.
3. A per-channel **Booth Producer** schedules the single, serial audio stream. AI agents never speak directly; they *bid* for airtime.
4. **WARM** explanations come from an Agent Framework workflow **hosted in Foundry**. **COLD** recaps come from a **declarative YAML workflow, also hosted in Foundry**.
5. **Microsoft Fabric** records everything the system did. It turns that into an "Access Ops" dashboard, alerts and a compliance report for the European Accessibility Act.

---

## 1. Decisions

| Area | Decision | Why |
|---|---|---|
| Concept | Idea #4 Voice of the Game | Real impact for disabled fans; meets all five pipeline stages; strongest multi-agent and demo story ([IDEAS.md §5](IDEAS.md#5-decision-why-idea-4)) |
| Stack | Current: .NET 10 gateway + static JavaScript listener. Target: Blazor Listener/Overlay/Studio | Current implementation supersedes the original Blazor assumption |
| Orchestration | Current: versioned Foundry prompt agents for companion Q&A. Target: Agent Framework hosted workflows | Hosted-workflow APIs and availability still need verification before implementation |
| LLM | **One deployment: `gpt-5.6-terra`** for every LLM agent and the eval judge | Medium cost, a little above gpt-4o (§11) |
| Realtime voice | Voice Live with `gpt-realtime-mini` | The low-cost realtime tier |
| Live speech | Standard neural voices (full SSML) | HD and Omni voices lack `<prosody>` and `<break>` |
| Fabric | Planned: F1–F4; F5–F7 optional | No Fabric integration delivered; capacity/pricing require verification (§10) |
| Deploy | Current: PowerShell 7 + Azure CLI + Bicep | azd hosted-agent tooling and Aspire remain proposals |
| Region | Sweden Central (gpt-5.6-terra, neural voices and Voice Live confirmed there) | EU accessibility story; confirm model, voice and hosted-agent availability first |
| Secrets | Current: Entra credentials/managed identity, Key Vault startup loading, nonsecret `.env.local`, CI gitleaks | Other controls in §12 are targets unless verified separately |

---

## 2. What we're building

### 2.1 Features
1. **AI audio-descriptive commentary (AD).** Ball location and direction of play every few seconds; who has the ball; pass shape; shot speed and angle; kits, celebrations, referee signals, VAR and crowd; score and time reminders; **every on-screen graphic read aloud**. Verbosity and speaking rate are adjustable.
2. **Multilingual transcreation.** Culturally adapted football idiom per language rather than literal translation, with persona voices (calm describer, excitable caller, analyst).
3. **Voice Q&A mid-match** (Azure Voice Live): "score?", "where's the ball?", "who came on?", "why did that matter?" Commentary ducks, the answer plays, then a short catch-up.
4. **Earcons and stereo or binaural ball position** via Web Audio, following the Australian Open "Action Audio" precedent. Haptics on Android only.
5. **Deaf and hard-of-hearing users:** synced live captions (WebVTT, BBC style), sound labels, a visual sound strip, and **text Q&A** as an equal alternative.
6. **Calm mode** for neurodivergent users: low energy, predictable cadence, Easy-read captions. **Low-vision mode:** high contrast, a magnifier that follows the ball or a player, team markers coded by shape.
7. **Sighted overlay:** captions, "why it mattered" cards, speed and pass-difficulty tags. **Every visual cue must carry `altText`**, which is the "accessible-by-design overlay contract."
8. **Personalization:**
   - language (captions may differ from audio)
   - mode
   - favourite club (partisan or neutral tone)
   - player focus
   - single metric
9. **HT/FT audio recap** per language, plus an accessible text version.

### 2.2 Listener modes
| Mode | For | Character |
|---|---|---|
| **AD** (Audio Description) | Blind and low-vision fans | Action, not stats; ball location at most every 4 s; reminders at every restart; graphics read at dead balls |
| **Commentary** | All fans | Classic commentary with brief colour |
| **Calm** | Neurodivergent fans, or anyone wanting low stimulation | Current: quieter commentary cadence and pass filtering; crowd/gentle settings independent. Easy-read captions remain planned. |
| **Analyst** | Data-hungry fans | Pass difficulty, xT, PPDA, pressure regimes; graphics read automatically |
| **Casual** | New or occasional fans | Plain language, rules explained briefly |
| **Kids** | Young fans | Simple language, upbeat; **no free-text Q&A** (suggested questions only) |

### 2.3 Who gets what
| Audience | Main features |
|---|---|
| Blind | AD mode, earcons, stereo ball position, voice Q&A, keyboard and media-key control, screen-reader mode |
| Low vision | Magnifier following the ball or a player, high contrast (7:1 option), 400% reflow, shape-coded markers, AD as needed |
| Deaf and hard of hearing | Live captions with speaker labels and `[crowd roars]`-style sound labels, visual sound strip, text Q&A |
| Neurodivergent | Calm mode, Easy-read captions, a "What's happening?" summary button |
| Everyone, in any market | Language and persona choice, favourite club or player, sighted overlay |

---

## 3. Architecture (Target, Not Deployed Topology)

### 3.1 Diagram
```
 Vog.Simulator (seeded; 10 Hz tracking + MatchEvents + SceneEvents; golden matches; replay 1×/5×/20×)
        │ (a) INGEST ─ Azure Event Hubs (cloud) | in-process Channel<T> (local / demo)
        ▼
 ┌─ HOT (<1 s, no LLM, in-process per match) ───────────────────────────────────────────────────────┐
 │ (b) StatsEngine: speed/sprint tags · pass distance/accuracy/difficulty · ball & shot speed · xG   │
 │     xT · win probability · PPDA · field tilt · Control-vs-Chaos · pressure regimes (change-point)│
 │ BallStateTracker (thirds × channels, direction, possession) · MomentDetector (P0–P4)             │
 │ Code describers: Play-by-Play · Scene · Graphic Reader · Reminders ──► UtteranceIntent           │
 │ InsightCue builder (every cue has altText) ────────────────────────────────► overlay + readout   │
 └──────┬────────────────────────────────────────────────────────────────────────────────────────────┘
        │ intents {priority, value, validUntil, pts}            ▲ verified proposals (bids)
        ▼                                                        │
 ┌─ BOOTH (one actor per ACTIVE channel) ─────────┐    ┌─ WARM (2–8 s) · Foundry Hosted Agent "vog-insight-desk" ─┐
 │ ChannelRegistry (refcount, lazy, idle TTL)     │    │ code-first Agent Framework workflow:                    │
 │ Realizer: reviewed phrase banks [lang][persona]│◄───┤ MomentContext → fan-out [explainer, storyline, scene]   │
 │ BOOTH PRODUCER (airtime arbiter)               │    │ → fan-in → Fact-Checker (code) → repair? → Localizer    │
 │ Voice: TTS cache → Speech SDK (streamed PCM)   │    └──────────────────────────────────────────────────────────┘
 │ Captioner: word boundaries → CaptionCue/WebVTT │    ┌─ COLD · Foundry Hosted Agent "vog-match-recap" ─────────┐
 └──────┬─────────────────────────────────────────┘    │ declarative YAML: moments → arc → write → verify →      │
        │                                              │ Foreach language (transcreate, easy-read) → producer    │
        │                                              │ approval → render audio                                 │
        │                                              └──────────────────────────────────────────────────────────┘
        │ (d) RENDER · Azure SignalR Service (MessagePack): audio chunks · CaptionCues · InsightCues · state (pts)
        ▼
 ┌─ Browser · Blazor ──────────────────────────────────────────────────────────────────────────────────┐
 │ Listener (WASM, screen-reader first) + vog-audio.js (AudioWorklet mixer: channel bus · personal bus │
 │   · earcons · ball tone → stereo/HRTF panner · crowd bed → master with ducking)                     │
 │ Overlay /overlay/{match} (transparent HTML5, machine-readable cues) · Studio (Interactive Server)   │
 │ (e) PERSONALIZE: ListenerProfile (stored client-side)                                              │
 └──────┬───────────────────────────────────────────────────────────────────────────────────────────────┘
        │ push-to-talk mic (PCM16 24 kHz) ↑ / answer audio ↓ (personal bus)
        ▼
 Vog.Companion (Voice Live relay, Azure.AI.VoiceLive 1.2) ──tools──► Vog.MatchQuery (in-process)
                                                       └─ explain_moment ──► vog-insight-desk
 Vog.Mcp (MCP over HTTP) exposes the same MatchQuery tools to Foundry prompt agents and to Copilot

 OPS / DATA: OpenTelemetry → App Insights + Foundry tracing
             Event Hubs → Fabric Eventstream → Eventhouse → Access Ops dashboard + Activator → Teams
```

### 3.2 Key design decisions
- **D1, two audio layers.**
  - The *shared channel bus* is synthesized once per channel and broadcast to every listener on it, so cost scales with channels, not listeners.
  - The *personal bus* carries Q&A answers, on-demand readouts and catch-ups for one listener.
- **D2, audio delivery.**
  - Path: server TTS → SignalR binary PCM (24 kHz, 16-bit mono) → an AudioWorklet in a JS module. Audio chunks never pass through WebAssembly; Blazor C# handles only control and state (≤4 Hz).
  - The *production* story, which we pitch but don't build, is an **HLS alternate audio track + WebVTT** inside a broadcaster's player.
- **D3, Q&A through a server relay.** No keys in the browser, and the grounded tools run server-side. The relay adds about 30–60 ms.
- **D4, echo.** **Push-to-talk by default**, because Chromium does not apply echo cancellation to Web Audio output. Hands-free barge-in via Voice Live Live-Reference AEC is a stretch goal. Headphones are recommended.
- **D5, clocks.** Every message carries `pts` (presentation timestamp on the match clock), and the client keeps a server-clock offset.
  - **Stadium mode** adds no delay; its SLO is event→speech p95 ≤ 1.5 s.
  - **Broadcast mode** runs the presentation clock 2–3 s behind the match clock, so speech, captions and pitch stay in sync and perceived latency is close to zero.
- **D6, latency.**
  - The live path uses only template NLG from reviewed phrase banks, with 3–5 variants per key and a no-repeat window.
  - Player names and fixed calls are pre-synthesized, and the next two queued items are synthesized speculatively.
  - **All shot-outcome variants are pre-rendered** ("Saved!", "Wide!", "GOAL!") before the result is known.
- **D7, voice families.**
  - Live AD uses **standard neural voices**: full SSML with prosody rate, breaks, phoneme/lexicon and word boundaries.
  - HD and Omni voices lack `<prosody>`, `<break>`, `<bookmark>` and `<phoneme>`, so they are used for recaps only.
  - MAI-Voice-2.x (preview) is an A/B option for the "caller" persona.
- **D8, "agents propose, the Booth Producer disposes."** Agents bid {priority, value, duration, validUntil}, and a deterministic arbiter schedules the scarce, serial audio resource. **This is the centrepiece of the multi-agent pitch.**

### 3.3 Five-stage compliance
| Stage | Where it happens |
|---|---|
| (a) Ingest | Simulator → Event Hubs (or an in-process channel) |
| (b) Interpret | StatsEngine: speed/distance thresholds, pass distance/accuracy/difficulty, ball and shot speed, xG, xT, PPDA, regimes |
| (c) Explain | `vog-insight-desk`: control vs chaos, pressure shifts, rhythm, decision quality, all fact-checked |
| (d) Render | Timed audio + synced captions + HTML5 overlay with machine-readable `InsightCue`s (`/cues.json`, `/cues.vtt`) |
| (e) Personalize | ListenerProfile: language, mode, club, player, metric, accessibility settings |
| Plus | HT/FT recaps (`vog-match-recap`), multilingual output, synthetic data card |

---

## 4. Verified facts (1–2 October 2026)

| Item | Status / version | Consequence for us |
|---|---|---|
| `Microsoft.Agents.AI`, `Microsoft.Agents.AI.Workflows` | **1.23.0 stable** (29 Sep 2026). Agent Framework 1.0 went GA in Apr 2026. | Use code-first `WorkflowBuilder` (fan-out/fan-in, conditional edges, `RequestPort`, checkpoints). Pin the whole 1.23 family. |
| `Microsoft.Agents.AI.Workflows.Declarative` | Stable 1.x (C#) | YAML workflows via `DeclarativeWorkflowBuilder` + `AzureAgentProvider`. **No parallel action**, so fan-out stays code-first. |
| Declarative actions (C#) | `InvokeAzureAgent` (Foundry agent **by name**), `InvokeFunctionTool`, `InvokeMcpTool`, `HttpRequestAction`, `If`, `ConditionGroup`, `Foreach`, `Question`, `RequestExternalInput`, `SetVariable`, `SendActivity`, `EndWorkflow`… | In **hosted** scenarios, `InvokeMcpTool` `connection.name` is "not fully supported yet", so recap data access uses `InvokeFunctionTool` |
| Foundry **Hosted Agents** | **GA** service. Protocols: Responses, Invocations, A2A. Portal shows playground, traces, versions and sessions. Idle timeout is configurable. | All three workflows run in Foundry |
| `Microsoft.Agents.AI.Foundry.Hosting` | **Prerelease**. `AgentHost.CreateBuilder` → `AddFoundryResponses(agent)` → `RegisterProtocol("responses", e => e.MapFoundryResponses())`; `azd ai agent init/run/invoke/deploy` | Isolate it in the three `*.Host` projects; the workflows run only as Foundry hosted agents (§8.6) |
| Foundry portal **Workflows** (visual designer) | **Preview; retires 1 Dec 2026**; doesn't support hosted agents | Not used |
| `Azure.AI.Projects` | 2.0.1 stable; certflow-ai uses `AgentAdministrationClient.CreateAgentVersionAsync` + `DeclarativeAgentDefinition` + `GetProjectResponsesClientForAgent` | Proven way to create versioned prompt agents |
| Fabric data agent tool (`MicrosoftFabricPreviewTool`, C#) | Preview; **paid F2+**; **user identity (on-behalf-of) only** | Optional F6 only |
| `ModelContextProtocol` (C#) | **2.2.0** (Aug 2026). MCP sampling is deprecated in spec 2026-07-28. | `Vog.Mcp`; don't use sampling |
| Azure MCP Server | 2.0 **GA** | Used in VS Code with Copilot Agent Mode |
| Aspire | `Aspire.Hosting.AppHost` 13.6.0 | Local orchestration and emulators |
| `Azure.AI.VoiceLive` | **1.2.0 stable**, service 2026-07-15. Multilingual semantic VAD, `interrupt_response`, server echo cancellation, Live-Reference AEC, word timestamps; **WebRTC is preview** | Server-side relay; push-to-talk default |
| `Microsoft.CognitiveServices.Speech` | 1.52.0; text-stream input (`SpeechSynthesisRequest`) is **preview** | Stream the *audio* out; utterances are short anyway |
| HD / Omni / MAI voices | DragonHD has **no en-GB**. Omni lacks prosody/break/bookmark/phoneme. MAI-Voice-2.x is preview. **No "sports commentary" style exists.** | Neural voices for live audio; HD/Omni for recaps |
| `Azure.AI.Translation.Text` | 2.0.0 → API 2026-06-06 **GA** (NMT or LLM per request) | NMT for live text; glossary for names |
| `Microsoft.Extensions.AI.Evaluation.Quality` | 10.10.0 | Groundedness and custom evaluators in CI |
| `Deque.AxeCore.Playwright` | 4.10–4.13 | Automated WCAG checks |
| Cosmos DB vector search | GA. `flat` is exact and limited to 505 dimensions; `quantizedFlat`/`diskANN` go up to 4096 dimensions and need at least 1,000 vectors | "Similar moment" search |
| Browsers | Chromium doesn't echo-cancel Web Audio output; iOS Safari `navigator.vibrate` does nothing | Push-to-talk; haptics on Android only |
| Fabric | Activator latency "can be up to minutes". Trial: 60 days, F64-equivalent, no data agents or Copilot. Fabric CLI GA (Mar 2026). RTI items support Git integration. Cosmos mirroring GA. `fabric-rti-mcp` is public preview. | Fabric stays off the live path (§10) |
| `gpt-5.6-terra` | GA in Foundry (Jul–Aug 2026). Price per third-party trackers: $2.50/$15 per 1M tokens at launch, possibly $2/$12 after 30 Jul. | The single LLM deployment (§11) |

---

## 5. Solution layout

```
soccer-ai/
├─ .gitignore                       # created first: env files, azd state, keys, checkpoints, co-design data …
├─ global.json                      # pins the .NET 10 SDK
├─ Directory.Packages.props         # central package management (pin the Agent Framework 1.23 family)
├─ Directory.Build.props            # nullable, warnings-as-errors, analyzers
├─ VoiceOfTheGame.slnx
├─ azure.yaml                       # azd: Container Apps services + hosted agents
├─ .env.example                     # placeholders only
├─ SECURITY.md
├─ src/
│  ├─ Vog.AppHost/                  # Aspire: local orchestration + emulators (dev only)
│  ├─ Vog.ServiceDefaults/          # OpenTelemetry, health, resilience, options validation
│  ├─ Vog.Contracts/                # records + JSON Schemas (MatchEvent, TrackingFrame, SceneEvent, UtteranceIntent, InsightCue …)
│  ├─ Vog.Domain/                   # pitch geometry (105×68 m), zones, fictional league model
│  ├─ Vog.Simulator/ + Vog.Simulator.Host/   # deterministic engine + control API (start/seek/speed/seed)
│  ├─ Vog.Stats/                    # StatsEngine, BallStateTracker, MomentDetector, model runtime
│  ├─ Vog.Describe/                 # Play-by-Play, Scene, Graphic Reader, Reminders; per-language Realizers
│  ├─ Vog.PhraseBanks/              # /{lang}/{persona}/*.yaml (draft | reviewed)
│  ├─ Vog.Booth/                    # Booth Producer, ChannelRegistry, policies, CatchUp
│  ├─ Vog.Voice/                    # SSML builder, voice capability matrix, synthesizer pool, TTS cache, Captioner, WebVTT, lexicons
│  ├─ Vog.MatchQuery/               # shared read model + tools (bound in-process and over MCP)
│  ├─ Vog.Mcp/                      # MCP server (ModelContextProtocol 2.2, HTTP, API-key middleware)
│  ├─ Vog.Agents/                   # shared agent code: Insight Desk graph, Fact-Checker, Localizer, recap functions
│  ├─ Vog.InsightDesk.Host/         # Foundry hosted agent: code-first workflow  (agent.yaml from azd)
│  ├─ Vog.Recap.Host/               # Foundry hosted agent: declarative YAML workflow
│  ├─ Vog.Transcreation.Host/       # Foundry hosted agent: group chat + human review
│  ├─ Vog.Companion/                # Voice Live relay (voice and text Q&A)
│  ├─ Vog.Gateway/                  # Event Hubs consumer, SignalR CommentaryHub, playout, REST, as-run log
│  ├─ Vog.Web/ + Vog.Web.Client/    # Blazor: Studio (server) · Listener + Overlay (WASM); wwwroot/js/vog-audio.js
│  ├─ Vog.Pipeline/                 # composes the HOT path (simulator → stats → describe → booth per channel); golden match
│  └─ Vog.Evals/                    # MEAI evaluators, Foundry cloud-eval runner, Accessibility Auditor
├─ foundry/
│  ├─ agents/*.agent.yaml           # prompt-agent definitions read by tools/Vog.FoundrySetup
│  ├─ prompts/*.md                  # agent instructions (reviewed in PRs)
│  ├─ schemas/*.json                # response JSON Schemas
│  └─ workflows/match-recap.workflow.yaml
├─ fabric/
│  ├─ items/                        # Git-integration folders: Vog.Eventhouse, Vog.KQLDatabase, Vog.Eventstream, Vog.KQLDashboard, Vog.KQLQueryset
│  ├─ kql/                          # .create-merge tables, update policies, materialized views, functions
│  └─ deploy.yml                    # `fab deploy` configuration
├─ tools/  Vog.Replay (commentary playback, coverage report, data card) · Vog.FoundrySetup · Vog.PhraseBankStudio
│          Vog.Train · Vog.SeasonGen · Vog.SeedMiner
├─ tests/  Vog.Simulator.Tests · Vog.Stats.Tests · Vog.Booth.Tests · Vog.Describe.Tests · Vog.Contracts.Tests · Vog.Agents.Tests
│          Vog.GoldenMatch.Tests · Vog.A11y.Tests · Vog.Load
├─ infra/  main.bicep + modules/    # ported from certflow-ai + new modules
├─ data/   league/ · golden/ · glossaries/      # synthetic only
├─ docs/   architecture.md · adr/ · a11y/ (EAA mapping, screen-reader test script) · rai/
│          codesign/protocol.md · synthetic-data-card.md · metrics.md · demo-script.md
├─ .github/ workflows/ · copilot-instructions.md · prompts/
└─ .vscode/mcp.json                 # Azure MCP, Vog.Mcp (local), fabric-rti-mcp, Learn MCP: no secrets
```

---

## 6. Data: simulator, schemas, metrics

### 6.1 Simulator (this is where the "data synthesis quality" score comes from)
- **Movement:** 10 Hz output with 50 Hz ball physics (drag, bounce). Players steer to role targets from formation templates by phase (in possession, out of possession, transition), with per-player max speed, acceleration and stamina; fatigue lowers top speed over the match.
- **Ball-carrier decisions:** every 0.5–1.5 s the carrier weighs pass, carry and shot options. Each option scores EV = P(success) × xT gain, then a softmax picks one, with noise that rises under pressure.
- **Physical interceptions:** a pass is intercepted when a defender can reach its path in time, which keeps the pass-success model learnable and consistent.
- **Other events:** pressing triggers, duels and tackles, fouls and cards, offside at pass time, a goalkeeper save model, set pieces, substitutions.
- **SceneEvents** (needed for audio description): kits, celebrations, body language, referee signals, VAR checks, replays, crowd intensity, weather, injuries, substitution boards.
- **Storyline steering:** directives (`RedCard(team, 55')`, `PressIntensity(team, +Δ)`, `GoalBias`) nudge parameters rather than script outcomes. `Vog.SeedMiner` runs thousands of seeds headless and keeps the ones with the wanted arc as **golden matches**.
- **Season history:** `Vog.SeasonGen` simulates three prior seasons of box scores (Poisson team strengths), so milestones and streaks work.
- **Synthetic data card** (`docs/synthetic-data-card.md`): a 200-seed realism report checking:
  - pass completion 75–88%
  - 20–30 shots per match
  - about 2.7 goals per match
  - 9–12 km per outfield player
  - 20–40 sprints per player
- **Fictional names:** a syllable generator with a deny-list of real clubs and players, plus pronunciation entries (IPA and per-language alias). Crests are generated SVG geometry.
- **Determinism:** own RNG (PCG/xoshiro), single-threaded; the same seed produces the same SHA-256 of the output.

### 6.2 Schemas (v1, field level)

**MatchEvent**
```
event_id (m{match}-e{seq}) · match_id · seq · schema_version
type: pass|carry|dribble|shot|save|tackle|interception|clearance|pressure|duel|foul|card|offside|ball_out|
      throw_in|corner|free_kick|goal_kick|kick_off|goal|substitution|injury|possession_change|period_start|period_end
period · match_clock_ms · display_clock ("45+2") · wall_clock_utc
team_id · player_id · receiver_id? · opponent_id?
location{x,y} · end_location{x,y}   (metres, origin bottom-left) · attacking_right
outcome · body_part · pass_height · under_pressure · pressure_player_ids[]
possession_id · possession_team_id · phase (build_up|progression|final_third|transition|set_piece) · related_event_ids[]
enriched{pass_distance_m, pass_angle_deg, ball_speed_kmh, p_success, difficulty, xg, xt_delta, shot_speed_kmh, dq_score}
sim{seed, storyline_tag}            (stripped in broadcast mode)
```

**TrackingFrame** (10 Hz on the live path; a 2 Hz copy goes to Fabric)
```
match_id · frame_id · period · match_clock_ms · wall_clock_utc
ball{x,y,z,vx,vy,vz,owner_player_id?} · players[22]{player_id,team_id,jersey,x,y,vx,vy} · quality{occluded_ids[]}
```

**SceneEvent**
```
eventId · matchClockMs · kind KIT|CELEBRATION|BODY_LANGUAGE|REFEREE_SIGNAL|VAR|REPLAY|CROWD|WEATHER|INJURY|SUB_BOARD|COACH
actors[] · attrs{type (knee_slide|hands_on_head|advantage|screen_check…), intensity, location} · causeEventId · cameraVisible
```

**MomentCandidate**
```
moment_id · match_id · trigger · trigger_event_ids[] · significance 0..1 {wp_delta, xt_delta, rarity, story_relevance}
context_ref · deadline_wall_utc
```

**Claim** (typed, so code can check it)
```
claim_id · kind (metric_value|comparison|rank|count|streak|milestone)
subject{player_id|team_id} · metric · window{from_ms,to_ms|match|season} · comparator · value · unit
evidence_event_ids[] · verified · verifier_note
```

**UtteranceIntent** (canonical and language-neutral; produced once per moment)
```
intentId (ULID) · matchId · seq
kind: PBP_PASS|PBP_CARRY|BALL_LOCATION|POSSESSION_CHANGE|SHOT|SHOT_OUTCOME|GOAL|CARD|SUB|FOUL|SET_PIECE|VAR|STOPPAGE|
      SCORE_TIME|DIRECTION_OF_PLAY|SCENE|GRAPHIC_READOUT|EXPLANATION|STORYLINE|CATCH_UP|PRE_MATCH|SYSTEM
templateKey?            "pbp.pass.long" (HOT) | null for LLM-origin
slots{name→Slot}        PlayerRef{id,short,number,gender} · TeamRef · Zone{third,channel,side} · Speed{kmh}
                        Distance{m} · Difficulty{p,band} · Clock{min,sec,added} · Score{h,a}
canonicalText? · claims: Claim[]      (LLM-origin only; must be fact-checked)
priority P0..P4 · value 0..1 · verbosityLevel 1..3 · modes[]
energy 0..1 · styleHint neutral|excited|tense|calm|disappointed|celebratory
partisanship{beneficiaryTeamId?, sentiment -1..1} · focusPlayers[]
momentRef · eventRefs[] · cueRef?
matchClockMs · ptsMs · validUntilMs · supersedes[] · preemptive · interruptible · maxDurationMs?
spatial{x 0..105, y 0..68}? · earconId?
provenance{source TEMPLATE|LLM|PRODUCER, agent, model?, factCheck PASSED|NA|FAILED, traceId}
```

**Utterance** (rendered, per channel)
```
utteranceId · intentId · channelId · lang (BCP-47) · persona · voiceName · voiceFamily NEURAL|DRAGON_HD|OMNI|MAI_FLASH
text · ssml · captionText · easyReadText? · styleTag? · rateStep · estDurationMs · actualDurationMs
audio{format "raw-24khz-16bit-mono-pcm", chunks, cacheKey sha256(voice|style|rate|ssml), cacheHit}
words[{text, audioOffsetMs, durationMs, textOffset}]
schedule{plannedPtsMs, startPtsMs?, endPtsMs?,
         status QUEUED|SYNTHESIZING|PLAYING|DONE|PREEMPTED|DROPPED_STALE|DROPPED_SUPERSEDED|DROPPED_BUDGET, reason?}
latency{eventWallTs, intentTs, enqueueTs, ttsFirstByteTs, firstChunkSentTs, clientPlayTs}
```

**AudioChannel**
```
channelId = hash(matchId, lang, mode, persona, rateStep, partisanship, focusPlayerId?)
mode AD|COMMENTARY|CALM|ANALYST|CASUAL|KIDS · persona DESCRIBER|CALLER|ANALYST|CALM|KIDS
voice{name, family, styleMap(energy→style), lexiconUri}
policy ProducerPolicy{ballLocationMaxGapMs, scoreTimeIntervalMs, maxUtteranceMs[P0..P4], colourInOpenPlay,
                      preemptClasses, energyCap, graphicReadout AUTO_DEAD_BALL|ON_DEMAND, noRepeatWindow}
listeners · state WARMING|ACTIVE|DRAINING · idleTtlMs
metrics{deadAirPct, p50/p95EventToSpeechMs, dropsByReason, preemptions, cacheHitRate}
```

**ListenerProfile** (v1). It is **stored client-side by default**, because accessibility preferences can reveal a disability (a GDPR Art. 9 risk).
```
profileId (random) · uiLocale · preset AD_FULL|AD_BRIEF|COMMENTARY|CALM|ANALYST|CASUAL|KIDS
commentary{lang, mode, persona, verbosity 1..3, rateStep, partisanship, favouriteClubId?, focusPlayerIds[],
           singleMetric? XT|PASS_DIFFICULTY|SPRINT_SPEED|SHOT_SPEED|WIN_PROB|CONTROL_CHAOS, scoreTimeIntervalMin}
audio{commentaryVol, earcons{on,vol}, spatial OFF|STEREO|BINAURAL, ballTone, dangerPulse, crowdBed 0..1,
      duckDb, haptics, monoDownmix}
captions{on, lang (may differ from audio), style STANDARD|EASY_READ, soundLabels, speakerLabels,
         size 100–300%, fg, bg, bgOpacity, position, lines 2|3}
visual{theme DEFAULT|HC_DARK|HC_LIGHT|CUSTOM, textScale, reducedMotion, magnifier OFF|FOLLOW_BALL|FOLLOW_PLAYER,
       teamMarkerShapes, colourOverrides}
interaction{output APP_TTS|SCREEN_READER|CAPTIONS_ONLY, shortcutsEnabled, shortcutMap,
            qaInput PUSH_TO_TALK|HANDS_FREE|TEXT, qaVoice, mediaKeys}
privacy{micConsentAt?, storeTranscripts=false, syncToServer=false}
```

**InsightCue** (visual overlay contract, with accessibility additions)
```
cue_id (deterministic hash of moment, persona, locale, prompt version) · version · supersedes_cue_id? · match_id · moment_id
match_clock{period,ms,display} · wall_clock_utc · air{earliest_utc, latest_utc, scheduled_utc?} · ttl_ms
speed_tier hot|warm|cold · template + template_version · priority 0–100 · category
anchor{kind player|pitch_xy|team|none, player_id?, team_id?, x?, y?, follow}
viz{…}                      locale-free numbers (pass trace, heat grid, values)
payload[persona][locale]{headline≤40, body≤120, short≤24, tts_ssml?}
altText (≤140 chars, REQUIRED, contract-tested) · longDesc?
speech{intentId?, readoutPolicy AUTO|DEAD_BALL|ON_DEMAND|NEVER, priority} · earconId? · captionRef?
aria{politeness off|polite|assertive} · inPtsMs · outPtsMs · minDisplayMs (≥ reading time at 160–180 wpm)
readingLevel · lang · dir · visual{colourTokens (contrast-checked), iconShape, reducedMotionVariant}
evidence{event_ids[], frame_ranges[], claim_ids[]} · confidence
provenance{agents[{name, model, prompt_version, latency_ms, trace_id}], approval{mode, by?}, factCheck}
spoiler_class · targeting{club_ids?, player_ids?} · safety{passed, flags[]}
```

**EarconSpec**
```
earconId · meaning (for the tutorial page) · version
synth{kind TONE_SEQ|NOISE_SWEEP|PULSE_TRAIN, waveform, notes[{hz|midi, startMs, durMs, gain}], adsr, filter{type,hz,q}}
sample?{uri, license (CC0 or own), attribution}
durationMs (≤400; goal ≤1200) · levelDbRelSpeech (−12..−6) · minIntervalMs
spatial{source BALL_X|EVENT_X|CENTER, followY} · teamTimbre HOME|AWAY? · modes[] · suppressInCalm · duckSpeechDb
haptic? number[]            Vibration API pattern; Android only
```

**CaptionCue**
```
cueId · utteranceId? · channelId · lang · kind SPEECH|SOUND_LABEL|SYSTEM · speaker DESCRIBER|CALLER|ANALYST|ASSISTANT?
text (≤2 lines × ≤37 chars) · easyReadText? · startPtsMs · endPtsMs · words[{text, startPtsMs}]
vtt{line, position, align} · cps · driftMs (measured)
```

**StoryThread**: `thread_id · match_id · kind (comeback|duel|milestone_chase|tactical_shift) · title · status · beats[cue_ids] · entities[] · salience`

### 6.3 Metrics and models (deterministic C#, unit-tested)
- **Speed, distance, sprints:** central difference with EMA smoothing. High-speed running is ≥19.8 km/h; a sprint is ≥25.2 km/h held ≥1 s. **Player tags** fire when thresholds are crossed (e.g. top speed, 10 km covered).
- **Pass:** distance, angle and ball speed (distance ÷ flight time). **Difficulty = 1 − σ(β·features)**, with features: distance, angle, nearest defender to the passer, defenders in a 2 m passing-lane corridor, pressure on the receiver, pass height.
  - Training: ML.NET `LbfgsLogisticRegression` in `tools/Vog.Train`, exported as coefficients.
  - Runtime: a plain sigmoid. The per-feature contributions become the "why" (e.g. "lane blocked +0.21").
- **xG:** logistic regression on distance, angle, header and pressure. **Shot speed** is measured from tracking.
- **xT:** a 12×8 grid solved by value iteration over simulated season actions.
- **Win probability:** remaining goals per team are Poisson with λ scaled by minutes left and match state (a red card gives about ×0.75 for the 10-player side). Sum the joint score grid up to 10 goals each.
- **Pressing and territory:** PPDA over a rolling 10 min in the opponent's 60%; field tilt = share of final-third passes; tempo = passes per minute in possession.
- **Control-vs-Chaos (0–100):** combined z-scores of turnovers per minute, inverse mean possession-spell length, zone-transition Shannon entropy and duels per minute, over 5-minute windows.
- **Pressure regime change:** Bayesian online change-point detection (or CUSUM) on pressing intensity and field tilt.
- **Decision quality:** EV per pass option = P(success)·xT(target) − (1−P)·xT_opp(turnover location), using tracking positions at that instant. DQ = EV(chosen) ÷ max EV.

---

## 7. The live path: describers, Booth Producer, voice, captions

### 7.1 Describers (code; no LLM at match time)
| Describer | Input → output | Budget |
|---|---|---|
| Play-by-Play | Tracking + events + BallState → `PBP_*`, `POSSESSION_CHANGE`, `BALL_LOCATION`, `SHOT` intents | ≤20 ms |
| Scene | SceneEvents → `SCENE` intents. **"Scene lock":** it may only describe fields present in the SceneEvent. | ≤20 ms |
| Graphic Reader | `InsightCue.altText` → `GRAPHIC_READOUT` | ≤5 ms |
| Reminder Scheduler | Clock, score, attack direction → `SCORE_TIME`, `DIRECTION_OF_PLAY` (driven by Booth obligations) | — |

Phrase banks live in `src/Vog.PhraseBanks/{lang}/{persona}/*.yaml`, each with draft/reviewed status, 3–5 variants per key, typed slots and a word limit. Per-language **Realizers** handle gender and number agreement, units, clock phrases and prepositions (es/fr/pt/hi). The MVP languages are English and Spanish.

```yaml
# src/Vog.PhraseBanks/es-ES/describer/possession.yaml  (illustrative)
key: pbp.possession.change
status: reviewed          # draft | reviewed
reviewer: reviewer-es-02  # pseudonymous id; never a real name in the repo
maxWords: 6
slots: [team, player, zone]
variants:
  - "{team} recupera: {player}, {zone}."
  - "Balón para {team}. {player}, {zone}."
```

### 7.2 Booth Producer
It runs per channel, on every 50 ms tick and on every new intent:
1. **Drop stale items:** `validUntil < now + est·0.8`, or superseded by a later state of the same moment.
2. **P0 interrupts** (goal, penalty, red card, VAR decision): cut the current non-P0 item at the next word boundary (≤250 ms) or with a 60 ms fade.
3. **Open-play obligations first:**
   - If `now − lastBallLocation > maxGap − est`, generate a `BALL_LOCATION` from the current BallState.
   - Possession changes are spoken within 1.5 s.
4. **Pick the next item:** the highest (priority, value). **P3/P4 only at dead ball**, and only if it fits the expected restart time: throw-in ~8 s, goal kick ~12 s, corner ~20 s, substitution ~30 s, VAR ~60 s.
5. **Score/time reminders** at the mode's cadence, at dead ball or low intensity, and after every restart.
6. **Regime awareness:** in a "chaos" regime, cap P2 utterances at 1.5 s and suppress colour; in "control," allow it.
7. **Q&A pause:**
   - On `speech_started` or push-to-talk, the client ducks or pauses that listener's channel bus.
   - When the answer ends, `CatchUp(from, to)` produces a ≤6 s summary of P0–P1 events plus the score, played on the personal bus.

**Mode policies**
| Policy | AD | Commentary / Casual | Calm | Analyst | Kids |
|---|---|---|---|---|---|
| Ball-location max gap (open play) | 4 s | 8 s | 6 s | 10 s | 6 s |
| Score/time reminder | 3 min + every restart | 5 min | 3 min, fixed | 5 min | 3 min |
| Max P2 utterance | 2.0 s | 3.5 s | 3.0 s | 3.5 s | 2.5 s |
| Colour in open play | No | Brief | No | Brief | No |
| Energy cap | 0.7 | 1.0 | 0.3 | 0.6 | 0.8 |
| Graphic readout | Auto at dead ball | On demand | On demand | Auto | Auto |

### 7.3 Latency budget (Stadium mode, p95)
| Step | Budget |
|---|---|
| Event Hubs hop | 150 ms (in-process: <5 ms) |
| Stats + BallState + MomentDetector + Describer | 20 ms |
| Booth queue wait | P0 ≤100 ms, P1 ≤250 ms |
| TTS first chunk | ≤250 ms (cache hit: 0) |
| SignalR to browser | 80 ms |
| Client jitter buffer | 80 ms |
| **Total** | **~0.8–0.9 s typical; SLO 1.5 s** |

### 7.4 Voice
- **Synthesizer pool:** one pre-connected `SpeechSynthesizer` per active channel; audio streams out through the `Synthesizing` events.
- **TTS cache:** in-memory LRU plus Blob storage, keyed by sha256(voice|style|rate|ssml). At kick-off, player names and fixed calls ("Throw-in, Reds", "Goal kick") are pre-synthesized.
- **Voice capability matrix** per family (prosody, break, style, word boundary, phoneme):
  - AD channels use **standard neural** voices.
  - Recaps use DragonHD/Omni (Omni for en-GB).
  - The "caller" persona is an A/B test of MAI-Voice-2.x (preview).
- **Speaking rate** is a quantised channel dimension (0.9 / 1.0 / 1.2 / 1.4), because pitch-preserving time-stretch isn't available on live Web Audio streams.
- **Pronunciation:** lexicon `alias` (all voices) plus `phoneme` (neural voices) for the fictional names. A **round-trip word-error-rate gate** (synthesize → speech-to-text) runs in CI.

### 7.5 Captions
- Word-boundary events become `CaptionCue`s and a WebVTT track: two lines × 37 characters, left-justified, scrolling (BBC live guidance).
- Speaker labels and sound labels (`[crowd roars]`, `[whistle]`).
- Drift is measured per cue, with an SLO of **p95 ≤ 100 ms** (EN 301 549 §7). Reading rate is ≤180 wpm. Easy-read variant: ≤12-word sentences and no idioms.

### 7.6 Audio graph and earcons
- **`vog-audio.js`:** `AudioContext({latencyHint:"interactive"})`. Buses:
  - channel bus (AudioWorklet ring buffer)
  - personal bus
  - procedural earcons (no copyright issues)
  - ball tone → `StereoPannerNode`, or `PannerNode` with HRTF in binaural mode
  - procedural crowd bed (noise worklet + filter)

  Everything feeds master → `DynamicsCompressor` → output. Ducking uses `gain.setTargetAtTime(−18 dB, t, 0.05)`.
- **Spatial convention:** "you're in the main stand." Pitch length maps to stereo pan, so "attacking the goal on your **left**" matches the pan. The far touchline gets a low-pass filter and −3 dB.

| Earcon | Design | Notes |
|---|---|---|
| `possession.home` / `possession.away` | Two notes rising / falling | Panned to where possession changed |
| `shot` | Short sweep | Panned to the shot location |
| `saved` | Thud | — |
| `goal` | Arpeggio, 1.2 s | Allowed to exceed 400 ms |
| `whistle` | Synthetic two-tone | — |
| `card.yellow` / `card.red` | One high tone / low double tone | — |
| `danger.pulse` | Sonar-style; pulse rate rises closer to goal | **Off in Calm** |
| `var.check` | Three low pulses | — |
| `out_of_play` | Soft click | — |

An **earcon tutorial page** teaches the sounds before kick-off.

---

## 8. Agents and Foundry workflows

### 8.1 Agent roster
| Agent | Kind / model | Role | Pattern | Budget |
|---|---|---|---|---|
| Play-by-Play, Scene, Graphic Reader, Reminders | Code | HOT intents (§7.1) | In-process pipeline | ≤20 ms |
| **Booth Producer** | Deterministic arbiter | Schedules airtime per channel | Per-channel actor | 50 ms tick |
| **Tactical Explainer** (`vog-tactical-explainer`) | gpt-5.6-terra, JSON-schema output, MCP tools | "Why it mattered" in ≤25 words, with typed claims | Insight Desk fan-out; also agent-as-tool for Q&A | 1.5–4 s |
| **Storyline / Colour** (`vog-storyline`) | gpt-5.6-terra | Streaks, lore, head-to-head; dead-ball gap-fill only | Insight Desk fan-out | 2–6 s |
| **Scene Enricher** (`vog-scene-enricher`) | gpt-5.6-terra | Adds vivid but **scene-locked** detail to dead-ball moments | Insight Desk fan-out | 1.5–4 s |
| **Fact-Checker** | Code: typed claims vs MatchState + Eventhouse | Pass, one repair attempt, or drop | Fan-in, then conditional edges | ≤10 ms |
| **Localizer** (live) | Translator 2026-06-06 (NMT; LLM option for colour) + do-not-translate glossary | Fan-out over active languages | Executor | 0.3–2 s |
| **Transcreation Studio** | Group chat: `vog-transcreator` ↔ `vog-idiom-critic` (gpt-5.6-terra) ↔ slot/terminology checker (code), then a human reviewer | Builds reviewed phrase banks | Hosted agent (offline) | minutes |
| **Q&A Companion** | Voice Live `gpt-realtime-mini` (A/B `gpt-realtime-2.1-mini`), function tools, agent-as-tool → Insight Desk | Hands-free and text Q&A; `set_preference` ("slower", "Spanish please") | Realtime front-of-house | first audio ≤2 s p95 |
| **Pitch-Sheet / Pre-match** | Code + gpt-5.6-terra polish | Line-ups, kits, formations, weather, i.e. the pre-match description | COLD sequential | <30 s |
| **Recap Producer** (`vog-recap-arc-planner`, `vog-recap-writer`, `vog-transcreator`, `vog-easyread`) | gpt-5.6-terra | HT/FT recap per language, easy-read version, audio | Declarative YAML hosted agent | 30–120 s |
| **Accessibility Auditor** | Code metrics + LLM judge (MEAI.Evaluation, gpt-5.6-terra) | CI gate on golden matches; PR comment via `gh` | GitHub Actions + Studio panel | minutes |

### 8.2 Layer 1: Foundry prompt agents (versioned, visible in the portal)
`vog-tactical-explainer` · `vog-storyline` · `vog-scene-enricher` · `vog-recap-arc-planner` · `vog-recap-writer` · `vog-transcreator` · `vog-idiom-critic` · `vog-easyread` · *(optional F6: `vog-producer-analyst`)*

They are defined as code. `tools/Vog.FoundrySetup` reads each definition and creates or bumps an agent version on deploy, so prompts are reviewed in pull requests.

```yaml
# foundry/agents/vog-tactical-explainer.agent.yaml   (our own format, read by tools/Vog.FoundrySetup)
name: vog-tactical-explainer
description: Explains why a moment mattered in at most 25 words, with typed claims.
model: ${FOUNDRY_MODEL_DEPLOYMENT}          # gpt-5.6-terra, set in azd env, never hard-coded
instructions: ../prompts/tactical-explainer.md
responseFormat: ../schemas/explanation.schema.json
maxOutputTokens: 200
raiPolicy: ${AGENT_RAI_POLICY_ARM_ID}       # the FULL ARM id; a bare name silently filters nothing
tools:
  - mcp:
      connection: vog-mcp                   # Foundry project connection that holds the API key
      allow: [get_moment, get_window_stats, get_regime, get_player]
```

```csharp
// tools/Vog.FoundrySetup (sketch, based on certflow-ai AgentOrchestrator.RegisterAgentAsync, Azure.AI.Projects 2.0.1)
var definition = new DeclarativeAgentDefinition(spec.Model)
{
    Instructions = File.ReadAllText(spec.InstructionsPath),
    Tools = { BuildMcpTool(spec.McpConnection, spec.AllowedTools) },   // per-agent allow-list, no approval prompts
    // + ContentFilterConfiguration(raiPolicyArmId) and the JSON-schema text format: verify property names (§20)
};
await projectClient.AgentAdministrationClient.CreateAgentVersionAsync(spec.Name, new(definition));
```

### 8.3 Layer 2: three workflows, each deployed as a Foundry Hosted Agent

#### `vog-insight-desk` (WARM, code-first)
- **Input:** `MomentContext` JSON: stats snapshot, last 30 s of events, active story threads, active languages.
- **Output:** `InsightProposal[]`. Each proposal carries verified claims, a payload per language, and a bid {priority, value, duration, validUntil}.

```csharp
// src/Vog.InsightDesk.Host/Program.cs   (sketch: verify Agent Framework 1.23 signatures, §20)
var credential = new DefaultAzureCredential();
var project    = new AIProjectClient(new Uri(options.ProjectEndpoint), credential);

AIAgent explainer = /* bind Foundry prompt agent "vog-tactical-explainer" */ default!;
AIAgent storyline = /* "vog-storyline" */ default!;
AIAgent scene     = /* "vog-scene-enricher" */ default!;

var context   = new MomentContextExecutor(matchQuery);   // code
var fact      = new FactCheckerExecutor(matchQuery);     // code: typed claims vs MatchState / Eventhouse
var repair    = new RepairExecutor(explainer);           // one rewrite with the checker's errors
var drop      = new DropExecutor();                      // logs the rejection; nothing airs
var localizer = new LocalizerExecutor(translator, glossary);

Workflow desk = new WorkflowBuilder(context)
    .AddFanOutEdge(context, targets: [explainer, storyline, scene])
    .AddFanInBarrierEdge(sources: [explainer, storyline, scene], target: fact)
    .AddEdge<CheckResult>(fact, localizer, condition: r => r!.Passed)
    .AddEdge<CheckResult>(fact, repair,    condition: r => !r!.Passed && r.Attempt == 0)
    .AddEdge<CheckResult>(fact, drop,      condition: r => !r!.Passed && r.Attempt > 0)
    .AddEdge(repair, fact)
    .WithOutputFrom(localizer)
    .Build();

AIAgent deskAgent = desk.AsAgent(name: "vog-insight-desk");   // workflow-as-agent: verify method name

var builder = AgentHost.CreateBuilder(args);
builder.Services.AddFoundryResponses(deskAgent);
builder.RegisterProtocol("responses", endpoints => endpoints.MapFoundryResponses());
builder.Build().Run();
```

#### `vog-match-recap` (COLD, declarative YAML)
Data access uses **`InvokeFunctionTool`**, with C# functions registered by the host that call `Vog.MatchQuery` under managed identity. This keeps **URLs and credentials out of the YAML**, and avoids `InvokeMcpTool`'s hosted `connection.name`, which is "not fully supported yet."

```yaml
#
# foundry/workflows/match-recap.workflow.yaml
# Half-time / full-time recap. SKETCH: action kinds come from the Agent Framework declarative
# reference (C#). Verify property names and Power Fx expressions during the build.
# Input (first message): JSON {"matchId": "...", "period": "HT|FT", "languages": ["en-GB", "es-ES"]}
#
kind: Workflow
trigger:
  kind: OnConversationStart
  id: vog_match_recap
  actions:
    - kind: SetVariable
      id: read_request
      variable: Local.Request
      value: =ParseJSON(System.LastMessageText)

    - kind: InvokeFunctionTool
      id: get_key_moments
      functionName: GetKeyMoments          # host function → Vog.MatchQuery (+ Eventhouse season context)
      arguments:
        matchId: =Text(Local.Request.matchId)
        period: =Text(Local.Request.period)
      output:
        result: Local.KeyMoments

    - kind: InvokeAzureAgent
      id: plan_arc
      agent:
        name: vog-recap-arc-planner
      input:
        arguments:
          moments: =Local.KeyMoments
      output:
        responseObject: Local.Arc

    - kind: InvokeAzureAgent
      id: write_recap
      agent:
        name: vog-recap-writer
      input:
        arguments:
          arc: =Local.Arc
          moments: =Local.KeyMoments
      output:
        responseObject: Local.Recap         # canonical text + typed claims

    - kind: InvokeFunctionTool
      id: verify_claims
      functionName: VerifyClaims            # the same code Fact-Checker as the Insight Desk
      arguments:
        claims: =Local.Recap.claims
      output:
        result: Local.Check

    - kind: If
      id: repair_once
      condition: =Not(Local.Check.passed)
      then:
        - kind: InvokeAzureAgent
          id: rewrite_recap
          agent:
            name: vog-recap-writer
          input:
            arguments:
              arc: =Local.Arc
              errors: =Local.Check.errors
          output:
            responseObject: Local.Recap
        - kind: InvokeFunctionTool
          id: verify_again
          functionName: VerifyClaims
          arguments:
            claims: =Local.Recap.claims
          output:
            result: Local.Check
        - kind: If
          id: still_failing
          condition: =Not(Local.Check.passed)
          then:
            - kind: SendActivity
              activity:
                text: "recap_rejected: unverifiable claims"
            - kind: EndWorkflow

    - kind: Foreach
      id: per_language
      source: =Local.Request.languages
      itemName: lang
      actions:
        - kind: InvokeAzureAgent
          id: transcreate
          agent:
            name: vog-transcreator
          input:
            arguments:
              text: =Local.Recap.text
              locale: =lang
          output:
            responseObject: Local.Localized
        - kind: InvokeAzureAgent
          id: easy_read
          agent:
            name: vog-easyread
          input:
            arguments:
              text: =Local.Localized.text
              locale: =lang
          output:
            responseObject: Local.EasyRead
        - kind: InvokeFunctionTool
          id: stage_variant
          functionName: StageRecapVariant
          arguments:
            locale: =lang
            text: =Local.Localized.text
            easyRead: =Local.EasyRead.text

    - kind: RequestExternalInput            # the producer approves in the Studio console
      id: producer_approval
      prompt:
        text: "Approve this recap for broadcast?"
      variable: Local.Approval
      default: "pending"

    - kind: If
      id: approved
      condition: =Local.Approval = "approved"
      then:
        - kind: InvokeFunctionTool
          id: render_audio
          functionName: RenderRecapAudio      # host function → Vog.Voice (TTS + WebVTT), managed identity
          arguments:
            matchId: =Text(Local.Request.matchId)
        - kind: SendActivity
          activity:
            text: =Concat("recap_ready:", Text(Local.Request.matchId))
      else:
        - kind: SendActivity
          activity:
            text: "recap_not_approved"
```

```csharp
// src/Vog.Recap.Host/Program.cs   (sketch)
var agentProvider = new AzureAgentProvider(new Uri(options.ProjectEndpoint), new DefaultAzureCredential());
var workflowOptions = new DeclarativeWorkflowOptions(agentProvider) { Configuration = configuration };
// Register GetKeyMoments, VerifyClaims, StageRecapVariant, RenderRecapAudio as function tools
// (docs show WorkflowRunner / WorkflowFactory: verify the hosted wiring, §20).
Workflow recap = DeclarativeWorkflowBuilder.Build<string>("match-recap.workflow.yaml", workflowOptions);
// Durable checkpoints: CheckpointManager.CreateJson(store, DeclarativeWorkflowJsonOptions.Default)
AIAgent recapAgent = recap.AsAgent(name: "vog-match-recap");      // verify
// then AgentHost.CreateBuilder + AddFoundryResponses(recapAgent) + MapFoundryResponses, as above
```

#### `vog-transcreation-studio` (offline, code-first group chat)
- **Participants:** `vog-transcreator` ↔ `vog-idiom-critic` ↔ slot/terminology checker (code), using `CreateGroupChatBuilderWith(RoundRobinGroupChatManager { MaximumIterationCount = 3 })`.
- **Human gate:** a `RequestPort` for native-speaker review.
- **Output:** phrase-bank YAML with `status: reviewed`.
- **Trigger:** the `tools/Vog.PhraseBankStudio` CLI or the `phrasebank.yml` GitHub Action, which opens a PR via `gh`.

### 8.4 Q&A Companion (Voice Live agent mode → Foundry agent)
- **Built (Oct 3):** the companion is a Foundry prompt agent per language, `vog-match-companion-<lang>` (gpt-5.6-terra, reasoning `none`, `VogAgentGuardrail`), defined in `foundry/agents/vog-match-companion.agent.yaml` and registered by `tools/Vog.FoundrySetup`. The gateway relays each fan's question to Voice Live in **agent mode** (`AgentSessionConfig`), so Voice Live hears and speaks while the Foundry agent answers.
- **Tools run in Foundry, not the app:** in agent mode Foundry Agent Service runs the agent's tools, so the match tools are an **MCP server on the gateway** (`/mcp/<lang>`, read-only, API key). Each language has its own project connection (`RemoteTool`, key in Key Vault and the connection): Agent Service sends MCP requests to the connection's target, not the tool's own URL.
- **Push-to-talk works in agent mode** with `NoTurnDetection` and `mai-transcribe` transcription (`azure-speech` requires semantic VAD; the SDK's `mai-transcribe-1` is retired).
- **Historical measurement:** question → first answer audio p50 6.1 s, p95 8.7 s on Azure, above the ≤2 s target. The 21 October retrieved snapshot had 5 answers, p50 3.562 s / p95 7.335 s; neither snapshot is a controlled latency benchmark.
- **Sessions:** one Voice Live conversation per connection/language. Tap-to-ask with local silence auto-stop is default; hold-to-talk and typed input are optional. The 50-question limit resets with the conversation and is not durable rate/cost protection.
- **Tools** (planned in full; the first five are built as `MatchTools`): `score_time`, `where_is_ball`, `who_has_ball`, `recent_events`, `lineup_subs`, `player_info`, `stat`, `repeat_last`, `set_preference`, and `explain_moment`, which calls `vog-insight-desk`.
- **Current audio behavior:** commentary is muted during recording and ducked during answers. **Planned:** a Booth catch-up utterance after the answer; none is emitted today.
- **Text-chat twin:** the same tools for Deaf and speech-impaired users.
- **Safety:**
  - Fan input is wrapped as untrusted (certflow's `<untrusted_user_input>` pattern).
  - Prompt Shields are on.
  - Kids mode offers suggested questions only.

### 8.5 What stays outside Foundry, on purpose
- **The HOT describers and the Booth Producer:** deterministic and sub-second.
- **The Voice Live relay:** real-time audio. (The companion's answers themselves come from a Foundry agent: see §8.4.)

The README explains *why not everything is an agent*. Judges reward knowing when not to use an LLM.

### 8.6 Calling hosted agents: latency and resilience (Planned)
- **Calls:** the Gateway calls `vog-insight-desk` through the Responses API. The client builds on certflow's `GetProjectResponsesClientForAgent`; verify the hosted-agent reference shape.
- **Warm and dedupe:**
  - Warm-up ping at kick-off; hosted idle timeout set to 60 min.
  - One session per match.
  - Dedupe per `momentId`, following certflow's claim-before-work pattern.
- **Timeout:** 6 s client-side. The Booth never waits for agents: a late explanation is stale and is dropped, and play-by-play (the primary content) carries on as normal.
- **No in-process mode:** the workflow runs only as a Foundry hosted agent. If it is unavailable, the Studio shows the Insight Desk as down; nothing substitutes for it.
- **Demo moment:** the Foundry portal lists the 3 hosted agents and 8 versioned prompt agents, then shows **a trace of one Insight Desk run**: three parallel agent spans → fact-check → localize.

---

## 9. UX (Planned Blazor/Overlay/Studio)

### 9.1 Listener app (WASM PWA, screen-reader first)
- **Start:** the first focus is a large **"Start listening"** button, which unlocks audio playback (`audioCtx.resume()`). Pages have landmarks and a single H1, and onboarding is audio-guided.
- **Output modes:**
  - **App TTS** (default).
  - **Screen reader:** commentary goes to the user's own screen reader or braille display, through one *polite* live region throttled to one update per 2 s or slower, P0–P1 plus reminders; P0 may be assertive.
  - **Captions only.**

  In App TTS mode, live regions carry **UI status only** (WCAG 4.1.3), so they don't talk over our audio.
- **History:** a browsable `role="log"` of recent utterances (`aria-live="off"` in TTS mode).
- **Keyboard:** remappable Alt+Shift shortcuts (WCAG 2.1.4), tested against NVDA/JAWS browse-mode clashes:
  - **S** score and time · **B** where's the ball · **P** who has it · **L** last 3 events · **R** repeat last · **G** read the on-screen graphic
  - **V / Shift+V** verbosity down/up · **[ ]** speaking rate · **E** earcons · **C** captions · **Space** push-to-talk
- **Media Session:** headset and lock-screen keys (play/pause; seek back = repeat last). Verify (§20).
- **Low vision:** HC dark/light themes (with a 7:1 option), reflow at 400%, magnifier that follows the ball or a player, shape-coded team markers, reduced motion.
- **Deaf / hard of hearing:** captions (a different language from the audio is allowed), sound labels, a visual sound strip (crowd-intensity meter plus icons), text Q&A.
- **Calm:** no shouting styles or crowd bed, softened earcons, a "What's happening?" summary button.
- **Tracking:** throttled to 5 Hz for fans; overlay and Studio get 10 Hz.

### 9.2 Overlay (`/overlay/{match}/{layout}?channel=`)
- **Page:** WebAssembly with prerender off, a transparent body and a fixed 1920×1080 canvas. Animations use CSS transforms only; each cue goes queued → in → hold → out.
- **Machine-readable output:**
  - `data-cue-id`, `data-template` and `data-evidence` attributes on the DOM
  - the hub JSON, `GET /cues.json` and `GET /cues.vtt` (WebVTT metadata)
  - the published JSON Schema
- **`caspar-bridge.js`** exposes `play/stop/next/update(json)`.
- **Tested** as a browser source in **OBS, CasparCG and vMix**. Vizrt / AE Live would consume the JSON feed.
- **2D pitch:** SVG `<g>` elements with CSS `transform` transitions interpolating between 10 Hz frames. A small JS interop applies positions directly, so Blazor doesn't diff the DOM every frame.

### 9.3 Studio console (Interactive Server, Entra sign-in, `RequireAuthorization`)
- **Per-channel monitor:** listeners, current text, queue depth, event→speech p50/p95, drops by reason, preemptions, cache hits, dead air %. Includes **listen-in**.
- **Kill switches** per channel or global: the producer takes agent content off air (for example, a bad explanation) while the play-by-play carries on. There's also a "facts-only" switch.
- **Recap approvals** (the `RequestExternalInput` round-trip), a Fact-Checker rejection log, links to App Insights / Foundry traces, a cost meter, and an **event injector** for demos.
- **"Assist a human describer" prompter view:** a pitch sheet plus live stat prompts. This is the enterprise answer to the shortage of describers.

### 9.4 Settings
- Presets plus fine-tuning, stored as versioned JSON in `localStorage`, with export/import. **Server sync is opt-in.**
- The captions and audio-description toggles sit **next to play/pause** (EN 301 549 §7.3).

---

## 10. Microsoft Fabric: the data and operations layer, never on the live audio path

**Verdict:** Fabric applies, with real, non-decorative uses. It stays off the HOT and WARM paths, because Activator latency can run to minutes, Eventhouse ingestion takes seconds, and the audio SLO is 1.5 s.

### 10.1 Evidence from sample repositories (surveyed 2 Oct 2026)
| Repo | What it proves | How we apply it |
|---|---|---|
| [**microsoft/fabric-racing-sim**](https://github.com/microsoft/fabric-racing-sim) (official, MIT) | **Live sports telemetry into Fabric RTI is a Microsoft-showcased pattern.** A .NET telemetry client (game UDP feed) computes derived metrics → Eventstream → Eventhouse → KQL Dashboard + Power BI. Its `src/` holds the Fabric items as **Git-integration folders**: `RacingSim.Eventhouse`, `.Eventstream`, `.KQLDatabase`, `.KQLDashboard`, `.KQLQueryset`, `.SemanticModel`, `.Report`. | It has the same shape as our C# simulator + StatsEngine. We copy the **item-folder layout** under `fabric/items/`. **We don't copy its secret handling:** its setup notebook generates an `appsettings.json`, and its [client](https://github.com/toolboc/forza-telemetry-client) holds an `eventHubConnectionString`. We publish to *our* Event Hubs with managed identity, and Eventstream reads from there. Its setup is a manual notebook; **ours is scripted**. |
| [**microsoft/fabric-rti-mcp**](https://github.com/microsoft/fabric-rti-mcp) (official, public preview, MIT, `uvx microsoft-fabric-rti-mcp`) | An MCP server with Eventhouse `kusto_query` / `kusto_list_entities` / `kusto_describe_database` / `kusto_show_queryplan`, ~17 Eventstream builder tools, Activator `activator_create_trigger`, and Map. Auth via `DefaultAzureCredential`; tool allow-list `FABRIC_RTI_ALLOWED_TOOLS`; works with **GitHub Copilot Agent Mode**. | **Dev-time only**, in `.vscode/mcp.json`: Copilot authors KQL, Eventstream topology and Activator triggers. **The runtime stays C#:** `Vog.Mcp` queries Eventhouse through the Kusto .NET SDK. |
| [usk15/contoso-motors-rti-fabric](https://github.com/usk15/contoso-motors-rti-fabric) (community) | **Activator** gives automatic responses; the **Operations Agent** gives governed, human-approved actions with an audit record. Idempotent deploy script; a simulator with injectable faults; a `kql/` folder. | The "inject a fault → alert in Teams" demo beat; optional F7; the `fabric/kql/` layout |
| [microsoft/fabric-cicd](https://microsoft.github.io/fabric-cicd/latest/) + Fabric CLI (`fab deploy` runs fabric-cicd; CLI GA Mar 2026) | Code-first deployment of Git-format items that have public APIs | `fabric/deploy.yml` + `fab deploy` in GitHub Actions (OIDC) |
| [microsoft/fabric-samples](https://github.com/microsoft/fabric-samples) | `features-samples`: data-warehouse, fabric-apis, git-integration, semantic-link. No RTI or sports samples. | Git-integration samples only |
| [FabricRTI_Accelerator](https://awesome.ecosyste.ms/projects/github.com%2FSuryaTejJosyula%2FFabricRTI_Accelerator) (community) | A script that provisions an Eventhouse, KQL DB, tables, functions and materialized views | A reference for the `fabric/kql/` setup order |

### 10.2 Applicability by need
| Need | Applicable? | Why |
|---|---|---|
| Live AD audio path (≤1.5 s) | ❌ No | Activator latency runs to minutes and ingestion takes seconds; no sample targets sub-second loops |
| Match and accessibility telemetry, dashboards | ✅ Yes, proven | racing-sim (sports, .NET client), contoso-motors |
| SLO alerting to Teams | ✅ Yes, on the trial | Activator |
| Agent grounding on season history via KQL | ✅ Yes | Kusto .NET SDK; fabric-rti-mcp shows the agent → KQL pattern |
| Fabric items in Git + CI deploy | ✅ Yes | Item folders + `fab deploy`; only a paid capacity would be Bicep |
| Copilot Agent Mode authoring Fabric | ✅ Yes (preview tool, dev-time) | fabric-rti-mcp |
| Data agent (F6) / Operations Agent (F7) | ⚠️ Paid F2+ only | Neither works on trial capacity; both are preview. The Operations Agent needs an admin toggle and isn't available in East US / South Central US. |

### 10.3 What we use
| # | Use | Fabric items | Value | Priority | Capacity |
|---|---|---|---|---|---|
| **F1** | Telemetry store | **Eventstream** (Event Hubs source, consumer group `fabric`, Entra auth) → **Eventhouse** KQL DB. Tables: `match_events`, `tracking_2hz`, `moments`, `cues`, `utterances`, `caption_cues`, `latency_spans`. | One analytics source of truth | **MVP** | Trial |
| **F2** | **Access Ops dashboard + Activator** | Tiles: dead air %, ball-location interval p95, event→speech p50/p95, drops by reason, active channels and listeners, tokens and cost, groundedness, rejections. **Activator → Teams/email** when: p95 > 1.5 s for 1 min; dead air > 5%; simulator stalled; spike in rejections. | Live SLO monitoring; the enterprise story | **MVP** | Trial |
| **F3** | Season-history grounding | Three synthetic seasons in Eventhouse; materialized views; KQL functions `PlayerMilestones()`, `Streaks()` | `Vog.Mcp` `get_season_context` / `get_milestones` via the Kusto .NET SDK (managed identity, 60 s cache). The **Fact-Checker verifies against the same KQL**. | Should | Trial |
| **F4** | **Accessibility compliance report** | KQL queryset + a dashboard page (Power BI optional). Per match and language: key-event coverage, ball-location interval, caption drift, reading rate, rejections, SLO attainment. | EAA / EN 301 549 evidence: **Best Enterprise** | Should | Trial |
| F5 | Cosmos DB mirroring → OneLake | Mirror `cues` (as-run log) and `moments` only | Joins without pipelines | Could | Verify on trial |
| F6 | Producer "Ask the data" | Fabric **data agent** over Eventhouse, attached to `vog-producer-analyst` via `MicrosoftFabricPreviewTool` (C#) | On-behalf-of the signed-in producer (certflow's delegated-token pattern) | Stretch | **Paid F2+**; preview; user identity only |
| F7 | Operations Agent | Playbook over the Access Ops tables | Human-approved actions with an audit record (e.g. "take agent content off air on the ES channel") | Stretch | **Paid**; admin toggle; region limits |

### 10.4 Capacity, privacy and the repo
- **Trial:** start the Fabric trial **now**. It lasts 60 days at F64-equivalent with 1 TB of OneLake, so it runs to about 1 Dec, covering the build, judging and winners.
- **Paid capacity:** F6/F7 need an **F2 pay-as-you-go** capacity, about **$0.36/hour** (US West 2), roughly $263/month if left running. **Pause it when idle**, and create it only if F6 or F7 is attempted.
- **Fabric is part of the deployment:** no feature flag and no substitute dashboard.
- **Data minimization:** match and system telemetry only. No listener profiles, no microphone audio, no Q&A text. Tracking is down-sampled to 2 Hz.
- **Repo:**
  - `fabric/kql/*.kql` (`.create-merge`) and `fabric/items/` (Git-integration folders), deployed by `fab deploy` from GitHub Actions with OIDC.
  - An optional F2 capacity is Bicep `Microsoft.Fabric/capacities`.
  - Connection strings, if any, stay in Fabric or Key Vault and are never committed. Generated config (`*.generated.json`) is git-ignored.
- **Demo moment (~10 s):** the Access Ops dashboard updates live. Inject TTS latency from the Studio, and an **Activator alert lands in Teams**: *"the system tells the producer before a blind fan notices."*

---

## 11. Model and cost policy ("medium cost, a little above gpt-4o")

**One deployment for everything: `gpt-5.6-terra`** (Global Standard). It serves all 8 prompt agents, the transcreation group chat and the eval judge. The model name appears **only** in `foundry/agents/*.agent.yaml` (via `${FOUNDRY_MODEL_DEPLOYMENT}`) and in that one setting, so swapping models is a config change.

| Model | Input / output per 1M tokens | Role |
|---|---|---|
| gpt-4o (2024-11-20) | $2.50 / $10 | certflow-ai's model; reference point |
| **gpt-5.6-terra** | **$2.50 / $15** at launch (26 Jun 2026); one tracker reports **$2 / $12** after 30 Jul 2026 | **Default.** Same-or-lower input and 1.2–1.5× output vs gpt-4o: "a little above" |
| gpt-5.4 | $2.50 / $15 | Not used: terra is deployed in Sweden Central (quota 1,000K TPM) |
| gpt-5.6-sol, gpt-5.5, GPT-6 | $5 / $30 and up | **Not used** |

> Confirm prices in the Azure pricing calculator on deployment day. The official Azure OpenAI pricing page showed placeholders when checked.

- **No LLM tokens are spent on:** the HOT describers (templates), the Booth Producer and Fact-Checker (code), live localization (Translator NMT, per character), or live TTS (neural voices, per character).
- **Voice Q&A** uses Voice Live with `gpt-realtime-mini`, the low-cost realtime tier. Push-to-talk keeps audio tokens low. Verify the per-token price.
- **Cost controls:**
  - The Insight Desk runs only for P0–P2 moments.
  - Output is capped per agent (≤200 tokens for the explainer).
  - Static instructions come first, for prompt caching.
  - Translations run only for active languages.
  - A per-match token budget: once it's spent, agents stop bidding and the Studio raises an alert, so a runaway can't run up cost.
  - Response caching makes golden-match replays cost about $0.
  - The Batch API (−50%) is used for offline transcreation and nightly evals.
- **Ballpark (estimate):** 40 explained moments × 3 agents × (~2.5k input + 150 output tokens) ≈ 0.30M input + 0.02M output, about **$1 per match** at launch prices. Each recap language adds roughly $0.10–0.20.

---

## 12. Repo hygiene and secrets ("use .gitignore; never commit sensitive info")

- **`.gitignore` was created first.** It is the `dotnet new gitignore` template plus project rules:
  - azd and environment files: `.azure/`, `.env.*` (except `!.env.example`)
  - local settings: `appsettings.*.local.json`, `appsettings.Local.json`, `**/appsettings.generated.json`, `local.settings.json`, `secrets.json`
  - keys and certificates: `*.pem`, `*.key`, `*.p12` (`*.pfx`, `*.publishsettings` and `.env` come from the template)
  - local parameter overrides: `infra/*.parameters.local.json`
  - generated Fabric config: `fabric/**/*.generated.json`
  - workflow checkpoints: `**/checkpoints/` (they can contain conversation content)
  - generated audio: `out/audio/`
  - co-design data: `data/codesign/`, `**/recordings/`
  - gitleaks reports
  - `!.vscode/mcp.json` is allowed back in, and must stay secret-free
- **No secrets in code, YAML, `agent.yaml`, docs or Fabric definitions:**
  - **Managed identity + `DefaultAzureCredential`** for Foundry, Event Hubs, Cosmos, SignalR, Speech, Storage, Key Vault and Eventhouse.
  - **Current:** the gateway MCP API key is stored in Key Vault and a Foundry project connection. The gateway reads Key Vault at startup; it is not delivered through a Container Apps Key Vault secret reference. Provisioning lookup/error handling needs future hardening.
  - **Current local dev:** nonsecret settings in git-ignored `.env.local`, with Azure CLI credentials. `dotnet user-secrets` / `azd env` are original design options, not the implemented setup.
  - **No fallback secret defaults** in code. Options classes use `ValidateOnStart`, so the app fails fast when a setting is missing.
  - **GitHub Actions** uses OIDC federated credentials, with no client secrets.
  - `.vscode/mcp.json` uses `${input:…}` prompts.
- **Automated protection:** GitHub **secret scanning + push protection** on the public repo; **gitleaks** as a pre-commit hook and a failing CI step; Dependabot; `SECURITY.md`.
- **Sensitive data beyond keys:**
  - GenAI message-content capture is **off** in deployed environments.
  - Microphone audio is never stored.
  - Co-design notes and recordings stay outside the repo; only anonymized aggregates are committed.
  - Docs use repo-relative paths, not local machine paths.
- **Before submitting:** run gitleaks over the full history and review `git status`. Commit or push only when the team decides to.

---

## 13. Infrastructure and delivery

- **Current (verified 21 Oct 2026): Azure CLI + Bicep via PowerShell 7**, using `infra/provision.ps1` and `infra/teardown.ps1`. Built: Foundry account/project/model/guardrail, gateway managed identity and roles, Key Vault, registry, Container Apps environment/gateway, Log Analytics and App Insights. `-SkipBuild` skips image build only; it still deploys infrastructure, writes settings and registers agents. The azd notes below are future hosted-agent options.
- **azd** (`azure.yaml`):
  - Container Apps services: `gateway`, `web`, `mcp`, `simulator`, `companion`.
  - Three hosted agents via the `azure.ai.agents` azd extension: `azd ai agent init` creates each host's `agent.yaml` manifest. Verify how `azure.yaml` references them.
  - Local loop: `azd ai agent run` / `invoke --local`. Deploy: `azd provision` + `azd deploy`.
- **Bicep modules:**
  - **Ported from certflow-ai:** identity, monitoring, key-vault, container-registry, ai-foundry (AIServices account + project + `gpt-5.6-terra` deployment + RAI policy + roles), container-apps (including the `imagesPublished` two-pass trick).
  - **Planned, not present:** event-hubs (4 hubs: `match-events`, `tracking-lite`, `moments`, `cues`), signalr (S1), cosmos (with vector policy), storage and optional fabric-capacity.
  - **Current:** a Foundry MCP connection module. Fabric connections remain planned.
- **Identity and RBAC:** a user-assigned managed identity per app, least privilege:
  - Foundry User (renamed from Azure AI User)
  - Event Hubs Data Sender/Receiver
  - Cosmos DB data-plane role
  - SignalR App Server
  - Speech/Cognitive Services User
  - Storage Blob Data Contributor
  - Key Vault Secrets User
  - Eventhouse database viewer for `Vog.Mcp`

  Hosted-agent identities get only what their tools need.
- **GitHub Actions:**
  - **Current `ci.yml`:** restore, Release build/tests, 20-seed simulator data card, commentary check and full-history gitleaks scan. The following additional workflows are planned:
  - `a11y-audit.yml`: the Accessibility Auditor on golden matches, with a PR comment via `gh`.
  - `evals-nightly.yml`: uncached; pushes datasets to Foundry cloud eval.
  - `deploy.yml`: OIDC, then `azd provision/deploy` and `fab deploy`.
  - `phrasebank.yml`: the transcreation studio, opening a PR.
- **GitHub Copilot:** `.github/copilot-instructions.md`, prompt files in `.github/prompts/`, and `.vscode/mcp.json` (Azure MCP Server, Vog.Mcp, fabric-rti-mcp, Microsoft Learn MCP). How Copilot Agent Mode was used is documented in the README.
- **Local dev:**
  - Aspire AppHost with emulators: Event Hubs, Cosmos vNext with vector search, and Azure SignalR (serverless only, so verify the hub works or use in-process SignalR locally).
  - In-process channels as an Event Hubs alternative.
- **Current scaling:** one gateway Container App, min/max replicas 1/1; listener and relay run in it. Separate Web/Companion apps and hosted-agent warm-up settings remain planned.
- **Region:** Sweden Central. Confirm hosted agents, `gpt-5.6-terra` quota, neural/HD voices and Voice Live there on day 1.

---

## 14. Reuse from certflow-ai

Source: [github.com/daniel-melchizedek/certflow-ai](https://github.com/daniel-melchizedek/certflow-ai). Paths below are relative to that repo.

### 14.1 Port and adapt
| certflow-ai source | Becomes in Voice of the Game |
|---|---|
| `src/CertFlow.Agent/AgentOrchestrator.cs` `RegisterAgentAsync` (L353–371): `DeclarativeAgentDefinition` + `AgentAdministrationClient.CreateAgentVersionAsync` + `ContentFilterConfiguration(RaiPolicyArmId)`, warning if missing | `tools/Vog.FoundrySetup`. The RAI policy needs the **full ARM id**; a bare name silently filters nothing. |
| `AgentOrchestrator.BuildMcpTool` (L260–279): Foundry-hosted MCP tool, per-agent allow-list, `NeverRequireApproval`, key held in a project connection | Least-privilege MCP access for each `vog-*` agent (read-only tools; only the Fact-Checker path can call `verify_claims`) |
| `AgentOrchestrator.RunAgentAsync` (L516–567): `ProjectOpenAIClient.GetProjectResponsesClientForAgent(name)` | The base of the Responses client used to call Foundry agents and hosted agents, with function tools run server-side |
| `src/CertFlow.McpServer/Program.cs` (L50–89), `McpTelemetry.cs`, `Tools/*.cs` (self-correcting errors, `AppointmentTools.cs` L42–80) | `Vog.Mcp`: HTTP transport, API-key middleware with a fixed-time comparison that refuses to start without a key, a span per tool, errors the model can act on |
| `src/CertFlow.Application/Services/PolicyEngine.cs` + `PolicyRules/` | The Fact-Checker rule chain and the Booth mode policies |
| `src/CertFlow.Worker/Consumers/EmailReplyConsumer.cs` (L262–348): rebuild the user-facing result from data, not from the agent's account | The Cue Publisher numbers check: every spoken or displayed figure is re-derived from MatchState |
| `src/CertFlow.Worker/Consumers/InboundEmailConsumer.cs` (L36–73): claim-before-work idempotency | Dedupe per `momentId` in the Booth and Insight Desk |
| `<untrusted_user_input>` wrapping; downstream agents get JSON only | Q&A input wrapped as untrusted; the Insight Desk receives only typed `MomentContext` |
| `src/CertFlow.Worker/Program.cs` (L80–87): OTel + `UseAzureMonitor()` + `AddSource("OpenAI", "Azure.AI.*")` + GenAI tracing flags | `Vog.ServiceDefaults`, **also setting `OPENAI_EXPERIMENTAL_ENABLE_OPEN_TELEMETRY`** (certflow never set it) |
| `evals/CertFlow.EvalRunner/Program.cs` (Foundry evals against an agent target, `report_url`) | `Vog.Evals` cloud-eval runner, **wired into CI** |
| `infra/modules/ai-foundry.bicep`, `container-apps.bicep`, identity, key-vault, monitoring | The starting point for `infra/` (§13) |
| `src/CertFlow.Portal.Blazor/Program.cs` (L14–30): forwarded headers for Container Apps + Entra sign-in | Studio sign-in; the Listener stays anonymous by default |
| Delegated-token `StaticTokenCredential` (AgentOrchestrator L307–316) | Optional F6: Fabric data agent on behalf of the signed-in producer |
| `docs/presentation.md` (timing budget, "pre-stage everything"), `docs/problem-statement.md` ("claims deliberately NOT made") | The same structure for `docs/demo-script.md` and the impact claims |

### 14.2 Fix certflow-ai's gaps (scores on "quality software development practices")
| # | Gap in certflow-ai | Fix in Voice of the Game |
|---|---|---|
| 1 | No workflow; hand-written sequencing duplicated in two places | Three Agent Framework workflows as Foundry Hosted Agents |
| 2 | JSON parsed from prompts plus regex patches | JSON-schema response formats + typed `RunAsync<T>` + contract tests |
| 3 | Hardcoded model name and inline prompts; flat environment variables | `foundry/agents/*.agent.yaml` + prompt files; options classes with `ValidateOnStart` |
| 4 | Placeholder tests only | Booth property tests, golden-match snapshots, fake chat client for workflow topology, accessibility and E2E tests in CI |
| 5 | Public admin and debug endpoints with no authorization | `RequireAuthorization` everywhere in the Studio and admin; Key Vault actually used; no fallback secrets |
| 6 | No resilience handlers | `AddStandardResilienceHandler` + timeouts and retries; no fallback paths |
| 7 | Template leftovers, version drift | Clean solution; `Directory.Packages.props` + `global.json` |
| 8 | 526-line imperative `deploy.ps1`; Foundry connections outside IaC | azd + Bicep + `azd ai agent` + `fab deploy`, from GitHub Actions with OIDC |
| 9 | Docs that didn't match the code | README claims checked against the code before submission |
| 10 | Non-streaming chat; missing ownership checks | Streaming Q&A; authorization checks inside every tool |

---

## 15. Hero technology → rubric and prizes

Rubric abbreviations: TI = Technological implementation, AG = Agentic design and innovation, RW = Real-world impact and production readiness, UX = UX and presentation, CA = Category adherence.

| Technology | Concrete use | Rubric | Prize |
|---|---|---|---|
| **Microsoft Foundry** | 8 versioned prompt agents; **3 workflows as Hosted Agents**; one gpt-5.6-terra deployment; Voice Live, Speech, Translator; guardrails; tracing; cloud evals; portal playground | TI, AG, RW | **Best Foundry Use** |
| **Agent Framework (.NET)** | Code-first workflow (fan-out/fan-in, conditional repair loop); **declarative YAML** workflow (Foreach, human approval, function tools, checkpointing); **group chat** with human review; agent-as-tool; deterministic arbiter | AG | **Best Multi-Agent** |
| **MCP** | Our C# `Vog.Mcp` (one MatchQuery service, two transports); Azure MCP Server 2.0 and fabric-rti-mcp in Copilot Agent Mode | AG, TI | Multi-Agent |
| **Azure services** | Container Apps (azd + Bicep), Event Hubs, Cosmos (vectors), Azure SignalR, Blob, managed identity, Key Vault, App Insights | RW | **Best Azure Native** |
| **Microsoft Fabric** | F1 telemetry, F2 Access Ops + Activator → Teams, F3 KQL grounding for agents and the Fact-Checker, F4 EAA compliance report | RW, CA | **Best Enterprise**, Azure Native |
| **Azure AI Services** | Neural, HD and Omni voices with word boundaries, Translator 2026-06-06, Content Safety / Prompt Shields | TI, UX | Foundry |
| **GitHub Copilot Agent Mode + GitHub CLI/Actions** | Instructions file, prompt files, `mcp.json`, Auditor PR comments via `gh`, OIDC deploys, phrase-bank PRs | TI | Grand |
| Accessibility engineering | EAA / EN 301 549 mapping, SLOs, kill switch, cost model, privacy by default, co-design evidence | RW, UX | **Best Enterprise**, Grand |
| Five-stage pipeline | §3.3 | CA | All |

---

## 16. Milestones

Roles (adapt to the team size):
- **R1 Engine:** simulator, stats, describers.
- **R2 Booth & Audio:** Booth, Voice, `vog-audio.js`.
- **R3 Agents & Foundry:** hosted workflows, Q&A, MCP, evals.
- **R4 UX, Accessibility, Fabric & Story:** Blazor, overlay, accessibility tests, co-design, Fabric, video.

| Dates | Deliverables |
|---|---|
| **Oct 1–2** | **Confirm in the Official Rules whether building may start before Oct 6** (work must be new after registration opened on Sep 29). Port certflow's Bicep into an azd layout. Create `Vog.FoundrySetup`. **Spike:** one prompt agent + a "hello" code-first workflow as a hosted agent (`azd ai agent init/run/deploy`), visible in the portal with traces. Choose Sweden Central and request quotas. **Start the Fabric trial.** **Contact co-design partners** (local blind football club, RNIB Connect, CAFE, Level Playing Field, an internal disability network). Check your employer's policy on external hackathons, IP and branding. |
| **Oct 2–4** (go/no-go spikes) | S1: neural TTS streaming + word boundaries (measure time to first byte). S2: SignalR PCM → AudioWorklet + panner + earcon, end to end. S3: Voice Live .NET relay with push-to-talk and one tool. S4: SceneEvents in the simulator. |
| **Oct 5** | End-to-end "hello commentary": simulator → template → Booth → TTS → browser |
| **Oct 6–10** (window opens; livestream Oct 7) | Full English live path; Booth v1 (priority, preemption, stale-drop, reminders, gap-fill); captions; earcons + stereo; overlay with `altText`; `vog-insight-desk` v1 hosted; **F1** Eventstream → Eventhouse |
| **Oct 14: MVP freeze** (livestream) | Golden match with English AD + TTS + captions; earcons + stereo; caption overlay; **Spanish** (reviewed phrase bank); Voice Q&A (push-to-talk, 6 tools); Studio latency panel; Container Apps deploy; **co-design session #1**; **backup video recorded** |
| **Oct 15–21** (livestream Oct 21) | Feedback fixes; Calm, Analyst and Casual modes; player focus; partisan tone; `vog-match-recap` HT/FT in EN + ES; Auditor in CI; evals; load test; **F2** Access Ops + Activator → Teams; **F3** KQL grounding; **F4** compliance report; third language (FR or PT-BR); **co-design session #2** |
| **Oct 22–26** | **Feature freeze Oct 22.** Manual screen-reader pass. README, architecture, synthetic data card, metrics, responsible AI, EAA mapping. Shoot (Oct 23–24) and edit (Oct 25) the video with captions, plus **an audio-described cut**. gitleaks over history; tag v1.0. **Submit Oct 26.** |

- **Stretch:** hands-free AEC barge-in; Voice Live WebRTC; two-voice booth (commentator + describer); multi-voice podcast recap; Hindi; arrow-key "explore the pitch"; Media Session keys; haptics; Kids mode; A2A on the Tactical Explainer; F5; F6/F7 (only if a paid F2 is approved).
- **Cut order** (first cut first):
  1. avatar (never build)
  2. WebRTC
  3. F7 → F6 → F5
  4. haptics
  5. Kids mode
  6. two-voice booth
  7. third language
  8. hands-free AEC
  9. multi-voice recap (cut to a single voice)
  10. F4 → F3
  11. player focus
  12. partisan tone

  Keep **F1 + F2** if at all possible, because Fabric is a hero technology.
- **Never cut:** English AD, captions, earcons + stereo, overlay with `altText`, Spanish, basic Q&A, the Fact-Checker, latency metrics, the kill switch, at least one Foundry-hosted workflow.

---

## 17. Quality targets and testing

### 17.1 Targets
| Metric | Target | How it's measured |
|---|---|---|
| Ball-location mention interval (AD, open play) | p50 ≤3 s, p95 ≤5 s | Utterance log vs ball-in-play windows |
| Possession-change coverage | ≥95% within 1.5 s | Event ↔ utterance join |
| Key-event coverage (goal, card, sub, shot, VAR) | 100% | Same |
| Dead air in open play (>4 s, no speech or earcon) | ≤5% in AD | Timeline analysis |
| Event→speech latency (Stadium mode) | p50 ≤800 ms, p95 ≤1.5 s | OTel spans + client `clientPlayTs` ack |
| Broadcast-mode alignment | p95 ≤300 ms | Same |
| Stale / superseded drops | P2 <5%; P0/P1 = 0 | Booth metrics |
| Overlap on a channel | 0 (by construction) | Property tests |
| Q&A duck reaction | ≤200 ms after `speech_started` | Client timestamps |
| Caption sync drift | p95 ≤100 ms (EN 301 549) | Cue vs word-boundary offsets |
| Caption reading rate | ≤160–180 wpm | Per cue |
| TTS round-trip word error rate | ≤5%; fictional-name accuracy ≥95% | Golden phrase set per language |
| Groundedness | 0 unverifiable claims published; MEAI Groundedness ≥4/5 | Fact-Checker log + evaluators |
| Transcreation quality | ≥4/5 accuracy, fluency and idiom | 50 utterances per language: native-speaker spot check + LLM judge + back-translation slot check |
| Persona distinctiveness | ≥80% correct identification | Blind ABX test with co-design participants |
| Easy-read level | EN grade ≤6; ≤12 words per sentence | Readability scorer |
| WCAG 2.2 AA | 0 serious/critical axe issues; keyboard-only E2E; screen-reader script passes | Covers 1.2.4, 1.4.2, 2.1.4, 2.4.11, 2.5.8, 4.1.3 |
| Load | 500 listeners × 8 channels with the p95 SLO held | NBomber SignalR clients |
| Cost | Recorded per channel-match | Token and character meters |

### 17.2 Test suites
- **Unit and property** (xUnit + FsCheck):
  - known-answer tests for every metric
  - win probability at 0 minutes left and with red cards
  - xT convergence
  - PPDA with zero denominators
  - bounds: probabilities in [0,1], speed ≤10.5 m/s, distance never decreasing
- **Simulator:** the same seed gives the same SHA-256; the 200-seed realism report.
- **Booth** (virtual clock): never overlaps, never drops a P0, gap obligations met, catch-up length ≤6 s.
- **Golden-match snapshots** (Verify): StatsEngine output, intent stream, Booth timeline.
- **Contracts:** every cue has `altText`; every intent has `validUntil`; JSON Schema validation; character limits; locale coverage.
- **Workflows:** a fake chat client covers the Insight Desk topology (fan-in never stalls; repair loop at most once), recap approval pause/resume, and checkpoint restore.
- **Evals** (`Microsoft.Extensions.AI.Evaluation`, cached in CI):
  - Groundedness, Relevance, Fluency
  - custom: ClaimAccuracy = 100% for cues that air, Spoiler, Kids reading level, Glossary adherence, Persona distinctiveness
  - safety evaluators on Q&A

  A nightly uncached run pushes datasets to Foundry cloud eval.
- **Accessibility:** Playwright + `Deque.AxeCore.Playwright`; keyboard-only flows; RTL layout; overlay transparency (screenshot alpha check). A **manual screen-reader script** covers NVDA + Chrome/Firefox, JAWS + Chrome, VoiceOver macOS/iOS and TalkBack: start, change language, ask a question, change verbosity, read history, open the recap.
- **Q&A red team:** off-topic questions, prompt injection, unsafe requests.
- **Load and chaos:** NBomber SignalR clients; fan-out of 8 personas × 6 locales × 4 moments per minute; kill a replica mid-run and confirm resume.

### 17.3 Co-design protocol (`docs/codesign/protocol.md`)
- **Participants:** 5–8 people (blind, including a braille user; low vision; Deaf / hard of hearing; neurodivergent), **with consent and compensation**.
- **Tasks:** three per person on 10-minute clips, thinking aloud.
- **Measures:** SUS plus Likert items ("I could picture where the ball was"); A/B tests (earcons on/off, stereo vs mono, verbosity levels); a comprehension quiz (score, scorer, which side the ball is on).
- **Reporting:** **aggregate, anonymized results only.** Raw notes and recordings never enter the repo.

---

## 18. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Preview features (MAI voices, WebRTC, TTS text streaming, the Foundry .NET hosting package, realtime 2.1) | The default path is GA wherever possible; previews are A/B options. The hosting package is isolated in three `*.Host` projects. |
| LLM latency spikes | HOT cue first, WARM upgrade later; the Booth never waits on agents and drops stale items |
| Hallucinated descriptions | The live path is template-only; scene lock; typed claims + code Fact-Checker; numbers check; groundedness gate in CI |
| Fan-in barrier stalls / step blocking | One workflow run per moment; every fan-out target always returns (possibly `Skipped`); unit-tested |
| Echo during Q&A | Push-to-talk + headphones; Live-Reference AEC as a stretch goal |
| Mispronounced fictional names | Pronounceable name generator, lexicon alias/phoneme, word-error-rate gate |
| Robotic templates | Variants, no-repeat window, persona banks, style tags |
| Our TTS collides with the user's screen reader | Separate output modes; minimal live regions; screen-reader tests |
| Listener overload | Verbosity control, Calm mode, earcon tutorial, co-design |
| Template morphology in es/fr/pt/hi | Typed slot features, per-language Realizers; MVP limited to EN + ES |
| Quota or region gaps | Request on day 1; deployment name in config; gpt-5.6-terra deployed with 50K TPM (1,000K available); standard neural voices confirmed |
| Fabric trial limits | Keep Fabric off the live path; 2 Hz tracking; screenshot dashboards early |
| Demo-day failure | Deterministic golden match; the backup video is recorded by Oct 14 |
| IP and trademarks | Fictional names with a deny-list, generated crests, procedural audio, no cloned voices, AI-voice disclosure |
| Employer policy | Confirm your employer's policy on external hackathons, IP and branding before submitting |
| Scope creep | MVP freeze on Oct 14; cut order in §16 |

---

## 19. Two-minute demo storyboard

| Time | Shot | Rubric |
|---|---|---|
| 0:00–0:15 | **Black screen: "Headphones on."** Procedural crowd ambience. AD voice: "Reds, in red, attacking the goal on your left… Okafor, own half, left touchline…" A possession earcon pans left to right. "Silva shoots, twenty yards, 104 kilometres an hour, saved!" Caption: *"For 2.2 billion people with a vision impairment, this is how a match looks."* | UX |
| 0:15–0:30 | Fade up the 2D pitch, overlay captions and a why-card. The AD voice **reads the graphic aloud**: "Graphic: pressure shift. Blues have won the ball back four times in two minutes." | Tech, Category |
| 0:30–0:50 | Headset button: "Who just came on?" Commentary ducks; the assistant answers from tools. "Why did that matter?" gets a grounded explanation. Resume: "While we spoke: Blues corner, still 1–0." | Agentic, UX |
| 0:50–1:05 | "Switch to Spanish": transcreated delivery with Spanish captions. Then Calm mode replays the same moment calmly with easy-read captions. | Personalization |
| 1:05–1:15 | Sound labels and the visual sound strip; the low-vision magnifier following the ball | Impact |
| 1:15–1:38 | The **Foundry portal trace** of a `vog-insight-desk` run: parallel agents → **the Fact-Checker rejecting an ungrounded claim**. Then the **Fabric Access Ops dashboard**: inject latency and an **Activator alert lands in Teams**. Cost scales with channels, not listeners. | Foundry, Multi-Agent, Enterprise |
| 1:38–1:52 | Impact: the EAA has been in force since June 2025; FIFA needed human describers in 3 languages for 104 matches, and this scales to every match. Include a **real, consented** tester quote only if we have one. | Impact |
| 1:52–2:00 | HT recap in a third language. *"Voice of the Game: every fan, every language, every moment."* Link to the audio-described cut. | Close |

**Production tips** (from certflow-ai's `docs/presentation.md`):
- Pre-stage everything: golden seed, warmed hosted agents, cue cache.
- Record the backup video by Oct 14.
- Caption the video itself, and publish an audio-described cut.
- No real club names, logos or music.

---

## 20. Verify during build

- **Agent Framework 1.23:**
  - exact signatures: `AddFanOutEdge`, `AddFanInBarrierEdge`, `AddEdge<T>(…, condition:)`, `RequestPort.Create`, `CreateGroupChatBuilderWith`
  - agent-as-tool (`AsAIFunction`)
  - the durable checkpoint-store package
  - **the workflow-as-agent method name** (`AsAgent` vs `AsAIAgent`) for `AddFoundryResponses`
- **Foundry:**
  - that `AzureAgentProvider` resolves versioned prompt agents created with `CreateAgentVersionAsync` by name, and how to pin versions
  - `DeclarativeAgentDefinition` JSON-schema text format and the `ContentFilterConfiguration` property names
  - calling a hosted agent from .NET (Responses agent reference)
  - hosted-agent cold start and minimum warm settings
  - the `RequestExternalInput` round-trip over the Responses protocol
  - registering function tools for a *hosted* declarative workflow
  - Agent Inspector graph view in the Foundry Toolkit for VS Code
- **Voice Live .NET:** Foundry-agent session mode (documented for Python only); Live-Reference AEC option names; word timestamps; the push-to-talk commit method; `gpt-realtime-mini` vs `2.1-mini` regions and limits; pricing.
- **Voices:** MAI-Voice naming and SSML support; DragonHD `express-as`; exact en-GB/ES/FR/PT-BR neural voice names and styles; whether `Synthesizing` chunks stream fast enough; TTS concurrency limits and pricing.
- **Translator 2.0.0:** LLM mode, glossary support, latency.
- **Platform:**
  - `gpt-5.6-terra` / `gpt-5.4` quota in Sweden Central
  - Azure SignalR MessagePack streaming in both directions on Container Apps
  - how `azure.yaml` declares hosted agents
  - the Bicep API version for Foundry project connections
  - whether one AIServices account covers Speech and Translator
- **Fabric:**
  - Eventstream managed-identity/Entra auth to Event Hubs
  - Cosmos mirroring on trial capacity
  - the Activator → Teams action on trial
  - Real-Time Dashboard minimum refresh interval
  - Power BI licences for report viewers
  - Git-integration coverage per RTI item
  - `fab` support for Eventhouse/Eventstream creation
  - the C# `MicrosoftFabricPreviewTool` name in the current `Azure.AI.Projects`
- **Browser:** Media Session with Web Audio routed through `<audio>`; iOS audio session and silent switch; Blazor live regions under re-render and prerender.
- **Integration:** the HLS audio-description `CHARACTERISTICS` string; Vizrt / AE Live HTML5 data-feed compatibility.
- **Rules:** whether building may start before Oct 6; whether preview services are allowed; licence and branding rules.
- **Pitch claims:** primary sources for the WHO hearing figure, the Microsoft "game changer" quote and the RNIB on-screen-graphics finding.

---

## 21. Sources

**Hackathon:** [Official repo and rules](https://github.com/microsoft/insidethegamehackathon) · [Reactor series](https://developer.microsoft.com/en-us/reactor/series/S-1709/) · [PL × Microsoft partnership](https://news.microsoft.com/source/2025/07/01/premier-league-and-microsoft-announce-five-year-strategic-partnership-to-personalize-the-fan-experience-with-ai-for-1-8-billion-people/)

**Agent Framework and Foundry**
- Framework and workflows: [Agent Framework 1.0](https://devblogs.microsoft.com/agent-framework/microsoft-agent-framework-version-1-0/) · [Agent Framework at BUILD 2026](https://devblogs.microsoft.com/agent-framework/microsoft-agent-framework-at-build-2026-announce/) · [Workflows NuGet](https://www.nuget.org/packages/Microsoft.Agents.AI.Workflows/) · [Workflow edges](https://learn.microsoft.com/en-gb/agent-framework/user-guide/workflows/core-concepts/edges) · [Human-in-the-loop](https://learn.microsoft.com/en-us/agent-framework/workflows/human-in-the-loop) · [Checkpoints](https://learn.microsoft.com/en-us/agent-framework/workflows/checkpoints)
- Declarative workflows: [Declarative workflows (C#)](https://learn.microsoft.com/en-us/agent-framework/workflows/declarative?pivots=programming-language-csharp) · [Declarative workflows 1.0](https://devblogs.microsoft.com/agent-framework/move-agent-orchestration-workflows-out-of-code-with-agent-framework-declarative-workflows-1-0/)
- Hosting: [Foundry hosted agents (Agent Framework)](https://learn.microsoft.com/en-us/agent-framework/hosting/foundry-hosted-agent) · [Hosted agents (concepts)](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/hosted-agents) · [dotnet run → Foundry hosted agent](https://devblogs.microsoft.com/dotnet/from-dotnet-run-to-foundry-hosted-agent-in-3-lines-of-csharp/)
- Foundry portal workflows: [Foundry workflows (preview, retiring) + migration guide](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/workflow) · [Migrate declarative workflows (Foundry Toolkit)](https://learn.microsoft.com/en-us/azure/foundry/how-to/develop/vs-code-agents-workflow-low-code) · [What's new in Foundry, Jul–Aug 2026](https://devblogs.microsoft.com/foundry/whats-new-in-microsoft-foundry-july-august-2026/)
- Fabric tool: [Fabric data agent tool in Foundry](https://learn.microsoft.com/en-us/azure/foundry/agents/how-to/tools/fabric)

**Speech, Voice Live, Translator:** [Voice Live overview](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live) · [Voice Live how-to](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live-how-to) · [Azure.AI.VoiceLive](https://www.nuget.org/packages/Azure.AI.VoiceLive) · [HD voices](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/high-definition-voices) · [MAI voices](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/mai-voices) · [Voice styles](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/language-support?tabs=tts#voice-styles-and-roles) · [Speech SDK](https://www.nuget.org/packages/Microsoft.CognitiveServices.Speech) · [Translator 2026-06-06](https://devblogs.microsoft.com/foundry/azure-translator-api-version-2026-06-06-ga-supporting-multilingual-applications-with-flexible-translation-options/)

**Fabric**
- Docs: [Real-Time Intelligence](https://www.microsoft.com/en/microsoft-fabric/features/real-time-intelligence) · [Activator latency](https://learn.microsoft.com/en-us/fabric/real-time-intelligence/data-activator/activator-latency) · [Eventstream custom endpoint](https://learn.microsoft.com/en-us/fabric/real-time-intelligence/event-streams/add-source-custom-app) · [Fabric trial](https://learn.microsoft.com/en-us/fabric/fundamentals/fabric-trial) · [RTI Git & deployment pipelines](https://learn.microsoft.com/en-my/fabric/real-time-intelligence/git-deployment-pipelines) · [Fabric CLI GA](https://blog.fabric.microsoft.com/en-US/blog/fabric-cli-is-now-generally-available-explore-and-automate-microsoft-fabric-from-your-terminal/) · [Cosmos DB mirroring](https://learn.microsoft.com/fabric/mirroring/azure-cosmos-db) · [Operations agents](https://blog.fabric.microsoft.com/en-GB/blog/improving-operational-efficiency-with-operations-agents-in-real-time-intelligence/) · [RTI MCP announcement](https://blog.fabric.microsoft.com/en-us/blog/introducing-mcp-support-for-real-time-intelligence-rti/) · [F2 pricing](https://blog.fabric.microsoft.com/blog/announcing-microsoft-fabric-capacities-are-available-for-purchase)
- Sample repos: [microsoft/fabric-racing-sim](https://github.com/microsoft/fabric-racing-sim) · [toolboc/forza-telemetry-client](https://github.com/toolboc/forza-telemetry-client) · [microsoft/fabric-rti-mcp](https://github.com/microsoft/fabric-rti-mcp) · [usk15/contoso-motors-rti-fabric](https://github.com/usk15/contoso-motors-rti-fabric) · [microsoft/fabric-samples](https://github.com/microsoft/fabric-samples) · [fabric-cicd](https://microsoft.github.io/fabric-cicd/latest/) · [FabricRTI_Accelerator](https://awesome.ecosyste.ms/projects/github.com%2FSuryaTejJosyula%2FFabricRTI_Accelerator)

**Model pricing** (third-party; confirm in Azure): [GPT-5.6 tiers (Finout)](https://www.finout.io/blog/gpt-5.6-pricing-2026-sol-terra-and-luna-tiers-explained) · [GPT-5.6 (Falconer)](https://falconer.com/guides/gpt-5-6-pricing/) · [Luna/Terra/Sol update (OrcaRouter)](https://www.orcarouter.ai/blog/gpt-5-6-luna-terra-sol-pricing) · [Azure OpenAI pricing (official)](https://azure.microsoft.com/en-us/pricing/details/azure-openai/) · [Voice Live calculator](https://modelavailability.com/tools/azure-voice-live-calculator)

**Tooling:** [MCP C# SDK](https://www.nuget.org/packages/ModelContextProtocol) · [Azure MCP Server](https://learn.microsoft.com/azure/developer/azure-mcp-server) · [Aspire AppHost](https://www.nuget.org/packages/Aspire.Hosting.AppHost) · [MEAI Evaluation](https://www.nuget.org/packages/Microsoft.Extensions.AI.Evaluation.Quality) · [Deque.AxeCore.Playwright](https://packages.nuget.org/packages/Deque.AxeCore.Playwright/) · [Cosmos vector search](https://learn.microsoft.com/azure/cosmos-db/vector-search) · [Blazor Server hosting](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/server) · [CasparCG HTML templates](https://github.com/CasparCG/help/wiki/Media:-HTML-Templates)

**Accessibility:** [WHO World report on vision](https://www.who.int/publications/i/item/9789241516570) · [EAA and audiovisual services (Taylor Wessing)](https://www.taylorwessing.com/en/interface/2025/media-hot-topics-2025/access-to-audiovisual-media-services-under-the-european-accessibility-act) · [EN 301 549 §7](https://accessible.canada.ca/node/382) · [FIFA World Cup 2026 ADC](https://inside.fifa.com/organisation/news/audio-desctiptive-commentary-haptic-boards-jillian-sloane-canada) · [Red Bee AFCON](https://redbeemedia.com/news-events/audio-describing-live-football-behind-the-mic-for-afcon) · [Action Audio](https://www.monash.edu/news/articles/monash-university-and-tennis-australia-serve-up-world-first-accessible-audio-stream-for-fans-with-blindness-or-low-vision) · [BBC subtitle guidelines](https://clevercast.com/bbc-subtitling-guidelines) · [ARIA live regions](https://www.uxpin.com/studio/blog/aria-live-regions-for-dynamic-content/)

**Browser:** [Web Audio echo-cancellation limitation](https://docs.atmoky.com/known-issues) · [iOS Safari vibrate](https://bugnet.io/blog/fix-web-game-vibrate-api-not-available-ios-safari)
