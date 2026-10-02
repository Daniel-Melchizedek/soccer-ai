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
| (c) Describe | `Vog.Describe` | Language-neutral utterance intents, realized through reviewed phrase banks (`en-GB` reviewed, `es-ES` **draft**: needs native-speaker review before use on air) |
| (d) Schedule | `Vog.Booth` | The **Booth Producer**: decides what is spoken when on a serial audio channel, using priorities, preemption, staleness rules, location obligations and dead-ball colour |
| Composition | `Vog.Pipeline` | Runs everything together. Intents are computed once and rendered per channel (language × mode). |

Still to come, per the milestones: speech and captions (Azure Speech), voice Q&A (Voice Live), the Blazor listener app and overlay, the Foundry hosted-agent workflows, and Fabric.

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
| Routine play-by-play event → speech, p95 | 2.5 s | ≤ 3 s |
| Overlapping utterances / urgent items lost | 0 / 0 | 0 / 0 |

The simulator passes its **synthetic data card** over 20 seeds. Means: 2.5 goals (2.6 xG), 25 shots, 81% pass completion, 61 minutes of ball in play, 10 km and 34 sprints per outfield player, plus realistic fouls, cards, corners, throw-ins and goal kicks.

## Run it

Prerequisite: the [.NET 10 SDK](https://dotnet.microsoft.com/download) (pinned in `global.json`).

```bash
dotnet test                                   # 55 tests: determinism, physics, models, phrase banks, booth properties, golden match

dotnet run --project tools/Vog.Replay -- commentary                    # full match, English audio description
dotnet run --project tools/Vog.Replay -- commentary --lang es-ES       # Spanish (draft phrase bank)
dotnet run --project tools/Vog.Replay -- commentary --mode calm --from 70 --to 80
dotnet run --project tools/Vog.Replay -- commentary --quiet --latency  # coverage report + latency by template
dotnet run --project tools/Vog.Replay -- datacard --seeds 20           # simulator realism report
```

Modes: `ad` (audio description), `commentary`, `calm`, `analyst`, `casual`, `kids`.

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
tools/Vog.Replay     CLI: commentary playback, coverage report, data card
tests/               xUnit v3 test projects
```

## Secrets and personal data

Nothing sensitive belongs in this repository (BUILD_PLAN.md §12):
- The `.gitignore` excludes azd state, env files, local settings, keys, workflow checkpoints, generated audio and co-design data.
- Azure access will use managed identity; local development uses `dotnet user-secrets` and `azd env`.
- CI runs a gitleaks secret scan. Locally, install [gitleaks](https://github.com/gitleaks/gitleaks) and run `gitleaks git` before pushing.
