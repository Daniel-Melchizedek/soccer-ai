# Voice of the Game

**Accessibility-first, multilingual AI match commentary**: an entry for the Microsoft × Premier League *Inside the Game* hackathon.

Voice of the Game turns a synthetic football match into live commentary that works for every fan: audio description for blind and low-vision fans, multilingual commentary, captions for Deaf and hard-of-hearing fans, a Calm mode, and a sighted overlay where every graphic is also read aloud.

- Research and idea selection: [IDEAS.md](IDEAS.md)
- Full build plan: [BUILD_PLAN.md](BUILD_PLAN.md)

> All data is synthetic. The league, clubs and players are fictional and generated, with a deny-list of real club names. No real Premier League data, trademarks or footage are used.

## What works today

The offline **HOT path** (BUILD_PLAN.md §3) runs end to end on virtual time, with no cloud services needed:

| Stage | Project | What it does |
|---|---|---|
| (a) Ingest | `Vog.Simulator` | A seeded, deterministic 4-3-3 match simulator: 10 Hz tracking, match events, and the scene details an audio describer needs (kits, weather, celebrations, crowd, VAR) |
| (b) Interpret | `Vog.Stats` | Speed, distance and sprint tags from tracking; pass difficulty with its causes; xG; ball and shot speed; live ball state with restarts inferred from the laws of the game |
| (c) Describe | `Vog.Describe` | Language-neutral utterance intents, realized through reviewed phrase banks (`en-GB` reviewed; `es-ES` and `ta-IN` (Tamil) **draft**: they need native-speaker review before use on air) |
| (d) Schedule | `Vog.Booth` | The **Booth Producer**: decides what is spoken when on a serial audio channel, using priorities, preemption, staleness rules, location obligations and dead-ball colour |
| Composition | `Vog.Pipeline` | Runs everything together. Intents are computed once and rendered per channel (language × mode). |
| Speech and captions | `Vog.Voice` | Azure neural voices signed in with Microsoft Entra ID (the resource has no keys), 24 kHz PCM streamed as it arrives, captions timed to the spoken words (WebVTT, at most 2 lines of 37 characters), and cuts at word boundaries when the booth interrupts |
| (e) Render, live | `Vog.Gateway` | Plays the golden match in real time, on a wall-clock schedule every gateway shares, and broadcasts every channel that has listeners over SignalR: each line as the booth starts it, its audio streamed as it is synthesized, then word-timed captions. The listener page plays the audio through an AudioWorklet, with procedural earcons panned to where the action is. |
| Ask about the match | Foundry agent `vog-match-companion-<lang>` + `Vog.Companion` | Hold-to-ask or typed questions through Azure Voice Live in agent mode. The answering agent is a versioned Foundry prompt agent (gpt-5.6-terra, agent guardrail) whose MCP tools read the live match from the gateway, so every fact comes from match data. Answers come back in their own voice, with the commentary turned down. |

Still to come, per the milestones: the Foundry agent workflows (Insight Desk, match recap, transcreation), the Blazor listener app and overlay, and Fabric.

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

### Quality, measured on every run

The golden match meets all the accessibility targets from BUILD_PLAN.md §17, on English and Spanish audio description and on the Commentary and Calm modes:

| Check (audio description, en-GB) | Result | Target |
|---|---|---|
| Ball-location mention gap in open play, p50 / p95 | 2.8 s / 4.0 s | ≤ 3 s / ≤ 5 s |
| Dead air in open play | 0 % | ≤ 5 % |
| Urgent (goal, shot, tackle…) event → speech, p95 | 1.0 s | ≤ 1.5 s |
| Open-play pass calls → speech, p95 | 2.4 s | ≤ 3 s |
| Dead-ball calls (restarts, substitutions) → speech, p95 | 4.9 s | ≤ 8 s |
| Overlapping utterances / urgent items lost | 0 / 0 | 0 / 0 |

Speaking-time estimates are calibrated against the real Azure voices with `vog-replay speak`: spoken ÷ estimated duration is 1.01 at the median for English audio description, and 1.05 for Tamil, whose longer words get their own rate factor. Synthesizing each line when it is *queued* rather than when it airs hides the service's first-byte latency: a median of 36 ms added, against about 600 ms on this development network.

The simulator passes its **synthetic data card** over 20 seeds. Means: 2.5 goals (2.6 xG), 25 shots, 81% pass completion, 61 minutes of ball in play, 10 km and 34 sprints per outfield player, plus realistic fouls, cards, corners, throw-ins and goal kicks.

## Run it

Prerequisite: the [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned in `global.json`).

```bash
dotnet test                                   # 92 tests: determinism, physics, models, phrase banks, booth, golden match, schedule, captions, match tools

dotnet run --project tools/Vog.Replay -- commentary                    # full match, English audio description
dotnet run --project tools/Vog.Replay -- commentary --lang es-ES       # Spanish (draft phrase bank)
dotnet run --project tools/Vog.Replay -- commentary --mode calm --from 70 --to 80
dotnet run --project tools/Vog.Replay -- commentary --quiet --latency  # coverage report + latency by template
dotnet run --project tools/Vog.Replay -- datacard --seeds 20           # simulator realism report
```

Modes: `ad` (audio description), `commentary`, `calm`, `analyst`, `casual`, `kids`.

### With Azure: real voices and captions

Prerequisites: the [Azure CLI](https://aka.ms/installazurecli) and PowerShell 7, signed in with `az login`.

```powershell
./infra/provision.ps1        # everything in rg-vog-dev (Sweden Central), then builds and deploys the gateway:
                             #   Foundry resource + project (keys disabled), gpt-5.6-terra, agent guardrail;
                             #   Key Vault, container registry, Container Apps, Log Analytics + App Insights;
                             #   registers the Foundry agents; writes .env.local (git-ignored, no secrets)
./infra/provision.ps1 -SkipBuild                                       # infrastructure only

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

Open http://localhost:5080, pick a language and mode, and select **Start listening**. Headphones help: earcons are panned to where the action is, as seen from the main stand. The gateway needs the settings in `.env.local` (or the same names in its environment) and won't start without them. Speech is synthesized only for channels that have listeners, so cost scales with channels in use, not with listeners.

Measured on a developer network, from a cold start, English audio description:

| Measure | p50 | p95 |
|---|---|---|
| Booth start → first audio chunk sent (server) | 0.59 s | 0.72 s |
| Line received → playback started (browser) | 0.81 s | 1.39 s |

A line that could only start well behind the booth's schedule is skipped, so a hold-up doesn't leave the channel permanently behind: after 1.5 s for most lines, after 4 s for urgent ones, and never for goals and red cards. Skips are counted at `/stats`. The next step is to synthesize lines while they wait in the booth's queue rather than when they start, which S1 measured at a median of 36 ms added.

### Ask about the match (Foundry agents)

On the listener page, hold **Hold to ask** (or hold Space or Enter on it) and ask; or type the question. The gateway relays it to Azure Voice Live in agent mode, connected to the Foundry agent for the channel's language (`vog-match-companion-en-gb`, `-es-es`, `-ta-in`): English, Spanish and Tamil, spoken or typed. The agent's MCP tools read the gateway's live match, so answers come from match data, and it declines anything that isn't about the match.

```powershell
dotnet run --project tools/Vog.Replay -- companion --say "Who has the ball?"          # straight to the agent over Voice Live
dotnet run --project tools/Vog.Replay -- ask --url $env:VOG_GATEWAY_URL               # through a gateway, typed and spoken
```

Measured on the Azure deployment (Sweden Central):

| Measure | p50 | p95 |
|---|---|---|
| Booth start → first speech chunk sent (commentary) | 0.09 s | 0.79 s |
| Question finished → first answer audio (agent and tool call) | 6.1 s | 8.7 s |

The answer latency is the agent's: the model calls an MCP tool and then answers, and Voice Live speaks the result. Bringing it down is the next piece of work for the companion.

Matches run on a fixed wall-clock schedule from 1 October 2026, so every gateway, local or in Azure, is at the same moment of the same match. That is also why a local gateway's questions, answered from the deployed gateway's match, stay consistent with what you hear.
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
