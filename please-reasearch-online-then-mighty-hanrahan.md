# Voice of the Game: next accessibility innovations (research-led)

## Implementation Ledger: 21 October 2026

This research plan is retained as a proposal/history. The ledger below reflects current source, not a claim that every proposed detail or verification step is delivered. [README.md](README.md) separately records the Azure deployment verified on this date: healthy revision `ca-vog-dev-gateway--0000015`, image dated 7 October, one replica, and `gpt-5.6-terra` version `2026-07-09`. Source review does not establish that this image includes every current feature.

| Idea | Current source status | Still outstanding |
|---|---|---|
| 1 Hear the ball | Sonification, independent level and Learn the sounds implemented | Dedicated less-talk/more-sound preset and user evaluation not established |
| 2 Feel the moments | Capability-detected browser vibration implemented | Physical-device and assistive-technology testing |
| 3 Atmosphere | Language sound tags, visual atmosphere meter and synthetic crowd implemented | User validation of comprehension and stimulation levels |
| 4 Mix/captions | Five independent levels; clear/gentle settings; size/colour/opacity/outline/font/position controls, preview and local preferences | Live timing, contrast/reflow and compliance audit |
| 5 No-hold companion | Default tap with local silence auto-stop; optional hold, Alt+Shift+A and typed input | Motor/switch/eye-gaze testing; not hands-free |
| 6 One-tap questions | Deterministic Score/Ball readouts; Who scored via agent | What did I miss absent; half-second response is a target, not a measured guarantee |
| 7 Catch-up | Not implemented | Foundry workflow, fact checking and post-answer catch-up |
| 8 Braille ticker | Partial backend `BrailleBrief`/brief messages and text-group support | No browser output selector or live braille feed; display testing required |
| 9 Pitch map | Canvas map, zoom/follow-ball, shape/number markers and polite zone/possession description | Low-vision and screen-reader validation; no player-follow claim |
| 10 Immersive Reader | Not implemented | Resource, integration and evaluation |
| 11 Hands-free | Not implemented | Voice Live live-reference echo cancellation/barge-in; browser microphone echo cancellation is not this feature |
| 12 Lip-readable avatar | Stretch, not implemented | Feasibility and community evaluation |

No application tests, controlled latency benchmark or disability co-design/device audit was run in this documentation pass. The research claims below are historical references and hypotheses, not legal advice, compliance certification or a current platform-support matrix. Verify primary sources and applicability before using them in a pitch. The Design, build order, file-change suggestions and verification sections describe proposed work; this ledger supersedes their present-tense assumptions and original delivery dates.

## Context
The current browser source (served by the gateway) has:
- **Modes:** audio description, commentary and calm.
- **Languages:** English, plus Spanish and Tamil (both drafts).
- **Sound and text:** earcons panned to the ball, and word-timed captions.
- **Companion:** a Foundry prompt-agent voice companion (tap with silence auto-stop, optional hold, or typed), plus quick score/ball readouts.

The user loves the new look and asked for research-led, innovative improvements for disabled fans. This plan turns three online research passes (blind/low-vision/deafblind; Deaf, cognitive, motor and speech; Microsoft/Azure and web-platform capabilities) into features that fit the existing architecture. They're ranked by impact for disabled fans, demo value for the hackathon (submission 26 Oct 2026) and effort.

**Constraints carried forward:**
- Every agent lives in Microsoft Foundry.
- Use agent workflows where they fit.
- No fallback or degraded modes.
- Synthetic data only, and no third-party marks.
- Never commit secrets.
- A single `main` branch.

## What the research says (evidence behind the ideas)
- **Blind fans don't know where the ball is.**
  - RNIB's 2022 study scored TV football 6.1/15. Its top complaints: not knowing where the ball was, and not knowing how a goal came about.
  - Action Audio (Australian Open) turns ball tracking into sound under the commentary, deliberately curated so it isn't distracting. AKQA's next step is letting users choose which sounds they hear.
- **Touch carries position; audio carries events.**
  - OneCourt's study found audio works best for events and names, and touch for where things are. Fans rated their understanding 74.3% higher with haptics plus AI audio than with radio alone.
  - OneCourt was used with Microsoft Seeing AI at the World Cup in Seattle (June 2026). FIFA used haptic boards in 4 host cities.
- **Deaf and hard-of-hearing fans miss the atmosphere.**
  - 81% want to join chants, and 74% feel cut off from the atmosphere (Liverpool/Carlsberg, 2026).
  - FIFA 2026 interpreters relayed whistles, crowd cheers and emotional shifts.
  - Newcastle's Sound Shirt turns the crowd into vibration.
- **Caption settings have regulatory relevance; applicability needs review.**
  - EN 301 549 §7.1.4 (EAA): users must be able to change caption colour, font, size, background and outline.
  - FCC: the settings must be easy to find, previewable and remembered.
- **Hearing-aid users need the mix under their control.** The balance between speech and background was hearing-impaired viewers' top complaint (47%). Wimbledon let viewers set commentary and crowd levels separately.
- **Hold-to-talk can be a barrier.** Sustained activation can exclude users with tremor, switches or eye-gaze dwell. Providing tap and typed alternatives addresses that interaction risk; holding a key is not by itself a blanket WCAG 2.1.1 violation.
- **Typing is slow for some users.** AAC users manage 8–10 words a minute, and switch users about 1.7, which is a strong case for one-tap questions.
- **Deafblind access needs more investigation.** The original research did not identify a live-sport braille feed; that is not proof none exists. A key-first abbreviated feed is a design hypothesis that needs testing with screen readers and braille displays.
- **Platform limits.**
  - Vibration works only on Chrome/Samsung on Android, not on iOS or Firefox.
  - The browser's built-in binaural panner uses one generic head model and confuses front and back, so use stereo panning plus pitch rather than 3D.
  - No Azure service does sign language. The Deaf community warns against AI signing avatars for live content (WFD/WASLI; BDA 2025).
  - Voice Live's live-reference echo cancellation enables hands-free barge-in.
  - Azure Immersive Reader is still available and embeddable.

## Idea catalogue (who it serves · effort)
| # | Idea | Serves | Effort |
|---|---|---|---|
| 1 | **Hear the ball**: a soft continuous tone under the commentary. Left/right follows the ball along the pitch (as heard from the main stand), the pitch of the tone rises as the attacking team nears goal, each team has its own timbre, and passes tick. A "less talk, more sound" option. | Blind, low vision | M |
| 2 | **Feel the moments**: vibration patterns on Android for goal, shot, card and possession change, plus crowd pulses. Works with screen readers and braille. | Deafblind, Deaf, blind | S |
| 3 | **See and feel the atmosphere**: tags such as [WHISTLE], [CROWD ROARS] and [CROWD GASPS] in captions, a crowd-intensity meter, and a synthetic crowd sound. | Deaf/HoH, blind | M |
| 4 | **Your mix and your captions**: separate commentary, ball-sound and crowd volumes; a "clear speech" setting; caption size, colour, background, font and position, with a preview and remembered on the device (EAA §7.1.4). | HoH, hearing aids, Deaf, low vision | S |
| 5 | **No-hold companion**: tap to start and tap to stop (or auto-stop on silence), keeping hold as an option; a keyboard shortcut; large targets. | Motor, eye-gaze, tremor | S |
| 6 | **One-tap questions**: "Score", "Where's the ball?", "What did I miss?", "Who scored?". Score and ball are answered within about half a second in the channel's language (reusing `ReminderFactory` and the phrase banks); the others go to the agent. | Speech, motor, cognitive, blind | M |
| 7 | **What did I miss?**: a 15-second spoken catch-up of the last minutes, as the first **Foundry agent workflow** (fetch events through MCP → write the recap → check the facts in code → speak it in the channel language). | ADHD, cognitive, screen-reader users, anyone who stepped away | L |
| 8 | **Braille ticker**: a "screen reader & braille" output mode with throttled, key-first short lines (`79' GOAL BLU Zandor 0-1`) and the app's speech turned off. | Deafblind, screen-reader users | S–M |
| 9 | **Live pitch map**: a high-contrast mini-map with the ball and players, using shape and letter as well as colour (● R / ▲ B), zoomable. | Low vision, colour-blind, Deaf, judges | M |
| 10 | **Immersive Reader** for the commentary log and answers: picture dictionary, syllables, translation, read aloud. | Dyslexia, cognitive, language learners | S–M (new resource) |
| 11 | **Hands-free talk**: just speak, using Voice Live live-reference echo cancellation and barge-in (not for Tamil: semantic VAD doesn't support it). | Blind, motor | L |
| 12 | **Lip-readable companion**: a Voice Live photo avatar that lip-syncs answers. | HoH lip-readers | L |
| — | AI sign-language avatar: **deliberately not built**. Azure has no sign-language service, and Deaf organisations warn against AI avatars for live content. Ideas 3 and 4 serve Deaf fans instead. | — | — |

**The user chose to build all of ideas 1–11.** Idea 12 (lip-readable avatar) is a later stretch.

## Design (Proposed Details; Ledger Above Controls Status)

### Foundation (everything else builds on it)
- **Live pitch stream:** `LiveBroadcastService` gets a second loop next to `BroadcastStateAsync`. Every 200 ms it sends a compact `pitch` message, read from `_pipeline.Snapshot` (`MatchSnapshot.Frame` gives `BallFrame` and `PlayerFrame` list):
  - `{ t, p, b:[x,y,z], o:'h'|'a'|null, hr }`, where `hr` is whether the home team attacks right
  - `h:[[x,y,n]…]` and `a:[…]` for the players, rounded to 0.1 m (about 0.5 KB)

  The client interpolates between messages.
- **One audio mix in the browser:** a new `js/mix.js` gives each sound its own gain in the existing 24 kHz `AudioContext`, all feeding a master output:
  - commentary (the `pcm-player` node and its duck gain)
  - answers
  - earcons
  - ball sound
  - crowd sound

  "Clear speech" adds a `DynamicsCompressorNode` on speech and lowers crowd and ball.
- **Settings panel:** a "Sound, captions and controls" section with a live preview, saved in `localStorage` (EAA §7.1.4, FCC "readily accessible"). It's built with native controls (sliders, radios, selects), each with a label.

### 1 · Hear the ball (`js/sonify.js`)
- **Sound path:** `OscillatorNode` → `BiquadFilter` (lowpass) → `StereoPannerNode` → ball bus.
  - **Left/right:** pan = x/105·2−1, the same convention as the earcons.
  - **Distance:** volume follows the ball's distance from the main-stand touchline.
  - **Pitch:** from distance to the goal the possessing team attacks, worked out from `hr` and the period: 1 octave (220 to 440 Hz) from own goal to opponent's goal.
  - **Teams:** home sounds round, away sounds brighter; a loose ball is quieter and neutral; a ball in the air gets a slight glide.
- **Mixing:** all parameters move with `setTargetAtTime`, so there's no zipper noise. The sound is gently ducked (to 0.6) while commentary speaks, as in Action Audio. It's off until the fan turns it on, with its own volume.
- **Less talk, more sound:** a one-click preset that switches to the existing Calm channel (no pass-by-pass calls) with the ball sound on. No new listener mode is needed.
- **Teaching the sounds:** a short "Learn the sounds" demo that plays the ball moving from left to right and towards goal, before match use.

### 2 · Feel the moments (`js/haptics.js`)
- **Patterns:** `navigator.vibrate` patterns keyed by the earcon id that already comes with each line:
  - goal: long and repeating
  - shot: double
  - card: triple
  - whistle: two short
  - possession: one tick
  - crowd roar: a swell
- **Where it appears:** the toggle only appears when the device can vibrate. That's feature detection, not a degraded mode; iOS and Firefox don't support vibration. Patterns respect "Gentle sounds" (shorter).

### 3 · See and feel the atmosphere
- **Sound tags in the fan's language:** a new `sounds:` table in every phrase bank (`whistle`, `crowd_roar`, `crowd_gasp`, `crowd_rises`), e.g. `[CROWD ROARS]` / `[RUGIDO DEL PÚBLICO]` / `[ரசிகர்கள் ஆர்ப்பரிப்பு]`.
  - The `PhraseBank` loader (`src/Vog.Describe/PhraseBank.cs`) reads the table.
  - `UtteranceMessage` gains a `Sound` field, mapped from the earcon or intent kind (goal → roar, saved or missed shot → gasp, whistle earcon → whistle).
  - The browser shows the tag before the caption and in the history log.
  - The phrase-bank tests require the table in every language.
- **Crowd meter:** an "atmosphere" bar beside the scoreboard. Its energy comes from `ThreatModel.Value` (in `Vog.Domain`) for the team in possession, plus jumps after shots and goals. It's computed on the server and sent in the `pitch` message as `e` (0–1), and shown with words ("Quiet / Building / Roaring") as well as the bar. It's static when reduced motion is on.
- **Crowd sound (`js/crowd.js`):** filtered noise in an AudioWorklet, with loudness following `e` and swells on goals. It's synthetic, so nothing needs licensing. It has its own slider and is ducked under speech.

### 4 · Your mix and your captions
- **Sound:** sliders for commentary, ball, crowd, sounds (earcons) and answers; a "Clear speech" preset; a "Gentle sounds" preset (softer goal fanfare, shorter vibration).
- **Captions:**
  - size (S/M/L/XL)
  - colours (white on black, yellow on black, black on white, cyan on black)
  - background opacity
  - font (system, or Atkinson Hyperlegible, SIL OFL, self-hosted woff2)
  - position (above or below the log)
  - speaker colour: commentator white, companion cyan (BBC order)

  These are CSS custom properties on `.caption`.
- **Behaviour:** every setting previews live and is remembered on the device.

### 5 · No-hold companion (`js/ask.js`)
- **Talk mode setting:** "Tap to start, tap to stop" (the new default) or "Hold to talk".
  - In tap mode, the button toggles `aria-pressed` and its label changes ("Stop asking").
  - It stops by itself after 1.2 s of silence once speech has been heard, using an RMS threshold in `pcm-capture.worklet.js`, or after 20 s.
- **Shortcut:** Alt+Shift+A toggles talking from anywhere. All targets are at least 44×44 px.

### 6 · One-tap questions
- **The buttons:** Score · Where's the ball? · Who scored? · What did I miss?
- **Score and ball readouts:** a new hub method `Readout(kind)` goes to a new `ReadoutService` in the gateway.
  - It builds the intent with `ReminderFactory.ScoreTime` / `BallLocation` (`src/Vog.Describe/ReminderFactory.cs`) from `MatchSnapshot.Ball`.
  - It realizes the intent with a `Realizer` for the channel language.
  - It speaks it with the channel's commentator voice (`VoiceCatalog.For`) on a per-language warm `NeuralSynthesizer`, so it never queues behind commentary.
  - It sends the result only to the asker, reusing `answerStart/answerAudio/answerText/answerEnd`. Target: under 600 ms.
- **The agent buttons:** "Who scored?" sends a localized question to the companion agent. "What did I miss?" calls idea 7.

### 8 · Braille ticker (deafblind and screen-reader users)
- **Text-only listening:** an output-mode setting, "Spoken by the app" or "Screen reader & braille".
  - Joining text-only puts the connection in a `{channel}:text` group.
  - `ChannelDirectory` doesn't count it for speech, so no TTS cost is added.
  - `OnBoothEventAsync` sends `utterance` to both groups.
- **Short lines:** a new `BrailleBrief` formatter in `Vog.Describe` builds key-first lines from the intent and snapshot, e.g. `79' GOAL BLU Zandor 0-1`.
  - Abbreviations come from a new `brief:` table in each phrase bank (event words, and team codes such as RED/BLU, ROJ/AZU, சிவ/நீல).
  - `UtteranceMessage` gains `Brief`.
- **What reaches the braille display:** only P0/P1 events and a score/time line every 5 minutes. The browser throttles to one line per 3 s, in an `aria-live="polite"` `aria-atomic` region. The full log stays browsable.

### 9 · Live pitch map (`js/pitch.js`)
- **Drawing:** a `<canvas>` drawn from the `pitch` stream.
  - Dark pitch with bright lines.
  - Home players as ● and away players as ▲, with shirt numbers, in kit colours (shape and number carry the meaning).
  - The ball white with a black ring.
- **Low-vision controls:** zoom (+/−) and "Follow the ball" (a magnifier window).
- **Screen readers:** the canvas is hidden from them; beside it, a text line "Ball: {zone}, {team} in possession" updates at most every 5 s and isn't live.
- **Motion:** with reduced motion it redraws at 2 fps.

### 10 · Immersive Reader
- **Azure:** Bicep adds a `Microsoft.CognitiveServices/accounts` resource of kind `ImmersiveReader`, with a custom subdomain and key auth disabled. The gateway identity gets Cognitive Services User on it.
- **Token:** a gateway endpoint `GET /reader/token` returns a short-lived Entra token and the subdomain, rate-limited per connection.
- **Browser:** the vendored Immersive Reader JS SDK (check its licence) opens the recent commentary and the last answer in the channel's language. Check Tamil read-aloud support during the build.

### 7 · What did I miss? (the first Foundry agent workflow, as a hosted agent)
Hosted agents are GA in Sweden Central and can be versioned over REST (`az rest`), so no `azd` is needed.

- **Container (new `src/Vog.CatchUp.Host`):**
  - `AgentHost.CreateBuilder` → `AddFoundryResponses(workflow.AsAIAgent("vog-catch-up"))` → `MapFoundryResponses`, on port 8088 (Responses protocol 2.0.0).
  - Packages: Workflows 1.23.0, Foundry.Hosting 1.23.0-preview, Azure.AI.Projects 3.0.0-beta.3 via `VersionOverride` in this project only, plus `NoWarn` for preview diagnostics. Modelled on foundry-samples `csharp/hosted-agents/agent-framework/workflows`.
- **Graph (code-first `WorkflowBuilder`):**
  1. **RequestParser** (validates `{lang, minutes}`: the 3 languages, 1–15 minutes)
  2. **EventsFetcher** (MCP client → new `get_events_since` tool on `/mcp/{lang}`, which also needs adding to `MatchTools.Specs`)
  3. **RecapWriter** (Foundry prompt agent `vog-catchup-writer-<lang>`, no tools; writes directly in the target language and returns `{sentences:[{text, eventIds}]}`)
  4. **FactChecker** (pure code in a new testable `src/Vog.CatchUp` library: each sentence cites events in the window; every number, name and team is present in those events; fits a 15 s budget per language)
  5. Then: pass → **Publisher**; first fail → back to RecapWriter with the errors; second fail → **Rejecter**, which speaks nothing.
- **Secrets:** a Bicep `CustomKeys` connection `vog-catchup-secrets` supplies the MCP key as `${{connections.vog-catchup-secrets.credentials.mcp_api_key}}`. It never goes into the image.
- **Gateway:**
  - `CatchUpClient` (plain `HttpClient`, Entra scope `https://ai.azure.com/.default`) POSTs to `{project}/agents/vog-catch-up/endpoint/protocols/openai/responses`.
  - `CatchUpService` caches by (language, minutes, last event `Seq`) and speaks with `NeuralSynthesizer` + `VoiceCatalog.Companion(lang)` over the existing answer messages.
  - Hub method `CatchUp(minutes)`.
  - A warm-up call at kick-off, plus `idle_timeout_seconds: 3600`.
- **`provision.ps1`:**
  1. The project identity gets AcrPull, and `az acr config authentication-as-arm` is enabled.
  2. `az acr build` the catch-up image.
  3. FoundrySetup registers `vog-catchup-writer.agent.yaml`.
  4. `az rest POST …/agents/vog-catch-up/versions` with `kind: hosted`, the image, 0.5 CPU/1 Gi, environment variables, session config and the guardrail ARM id; poll until `active`.
  5. Grant the agent's instance identity Foundry User on the project.
- **Spike first (half a day):** a minimal hosted echo workflow deployed this way. It must confirm invocation, cold start, and that the instance identity can call prompt agents.

### 11 · Hands-free talk (English and Spanish; Tamil uses tap-to-talk)
- **Session config:** `AudioEchoCancellation { ReferenceSource = Client, Channels = 2 }` plus `AzureSemanticVadTurnDetectionMultilingual { Languages = [en, es], InterruptResponse = true, AutoTruncate = true }`, `InputAudioSamplingRate = 24000`. Audio is interleaved stereo PCM16: channel 0 the mic, channel 1 what was actually played (Microsoft sample `voicelive-samples/javascript/live-reference-aec`).
- **Browser:**
  - Everything that plays (commentary, answers, earcons, ball, crowd) goes through the master `out` gain from the Foundation section.
  - `earcons.js` takes a bus argument (today it plays straight to the destination, so the earcons would be missing from the echo reference).
  - The mic opens inside the same 24 kHz context, with browser processing off.
  - A new `pcm-aec-capture.worklet.js` mixes the mic and the `out` tap into 100 ms stereo chunks.
  - SignalR moves to MessagePack (96 kB/s of binary instead of base64).
  - On `speechStarted`: flush the answer and duck the commentary.
  - It switches off after **2 silent minutes**, with a spoken notice.
  - Where the browser can't run the mic in a 24 kHz context, hands-free isn't offered.
- **Gateway:**
  - `CompanionOptions.HandsFree`; sessions keyed by (language, hands-free).
  - `SpeechStarted`/`SpeechStopped` events forwarded to the browser.
  - Hub methods `TalkStart/TalkAudio/TalkStop`; a question is counted on each `SpeechStopped`.
  - Reconnect at Voice Live's session time limit.
- **Spike first:** confirm live-reference echo cancellation plus semantic VAD work in agent mode with `mai-transcribe`, and check tolerance for Bluetooth delay.

### Plus: "thinking" sound
While the companion or catch-up works, a soft looping tick plays on the answers bus (and is announced "Thinking…" in the status line), so blind fans aren't left in silence for 6–9 s.

## Build order (submission 26 Oct; one commit, deploy and verification per step)
1. **Oct 5–7:**
   - Foundation (pitch stream, mix bus, settings panel)
   - idea 4 (your mix and captions)
   - idea 5 (no-hold talk)
   - "thinking" sound
2. **Oct 7–10:**
   - idea 1 (hear the ball, plus "Learn the sounds")
   - idea 2 (haptics)
   - idea 6 (one-tap questions and instant readouts)
3. **Oct 10–13:**
   - idea 3 (sound tags in the phrase banks, crowd meter, crowd sound)
   - idea 8 (braille ticker and text-only listening)
4. **Oct 13–17:** idea 7, starting with the hosted-agent spike.
5. **Oct 16–18:**
   - idea 9 (pitch map)
   - idea 10 (Immersive Reader)
6. **Oct 18–21:** idea 11, starting with the echo-cancellation spike.
7. **Oct 22–25:** freeze, accessibility pass, README/BUILD_PLAN updates, demo video. Earlier planned work (Fabric, Insight Desk, recap, companion speed-up) only if time remains.

## Files touched (representative)
- **Gateway:** `src/Vog.Gateway/LiveBroadcastService.cs` (pitch stream, text-only groups), `CommentaryHub.cs` (Readout, CatchUp, Talk*, text-only join), `Messages.cs` (`Sound`, `Brief`), new `ReadoutService.cs`, `CatchUpService.cs`/`CatchUpClient.cs`, `ChannelDirectory.cs`.
- **Companion and describing:** `src/Vog.Companion/CompanionSession.cs` (hands-free config and events), `MatchTools.cs` (`get_events_since`); `src/Vog.Describe/PhraseBank.cs` + all three `describer.yaml` (`sounds:`, `brief:`), new `BrailleBrief.cs`.
- **New projects:** `src/Vog.CatchUp` (fact checker), `src/Vog.CatchUp.Host` (hosted workflow + Dockerfile); `foundry/agents/vog-catchup-writer.agent.yaml`.
- **Browser:** `wwwroot/js/` new `mix.js`, `sonify.js`, `haptics.js`, `crowd.js` (+ worklet), `pitch.js`, `reader.js`, `pcm-aec-capture.worklet.js`; changes to `app.js`, `ask.js`, `earcons.js`, `pcm-capture.worklet.js`; `index.html`, `css/site.css`.
- **Infrastructure:** `infra/main.bicep` and modules (Immersive Reader account, catch-up connection, ACR pull for the project identity); `infra/provision.ps1` (catch-up image and hosted agent version).

## Verification
- **Unit tests (xUnit):**
  - every phrase bank has `sounds:` and `brief:` for all keys
  - `BrailleBrief` lines are key-first and ≤ 40 characters
  - FactChecker rejects invented names, numbers and events and accepts grounded recaps (golden match at 80')
  - `get_events_since` windows
  - readout intents realize in all 3 languages
  - golden coverage still passes
- **Smoke tools against the deployed URL:**
  - `vog-replay listen` (now also checks `pitch` messages at about 5 Hz and `Sound`/`Brief` fields)
  - `vog-replay ask` (adds `Readout` latency under 600 ms and `CatchUp` grounded text plus audio)
  - a new `vog-replay handsfree` that streams synthesized stereo (speech on channel 0, played audio on channel 1) and checks `speechStarted` → answer → barge-in flush
- **Browser (headless Chrome over CDP, as before):**
  - settings persist across reloads
  - captions restyle
  - the braille mode live region updates no more than once per 3 s
  - the pitch map draws
  - no console errors
  - `@axe-core` run on the page with no serious issues
- **Manual, on a phone:**
  - Android vibration patterns and tap-to-talk
  - a quick NVDA pass of braille mode and keyboard shortcuts
  - listen to ball sonification with headphones
- **Each step:** build, all tests, secret scan of staged files, commit to `main` (no attribution trailer), `./infra/provision.ps1`, then the smoke checks.

## Risks
- **Previews:** the hosted-agent hosting package and the 3.0.0-beta SDK are previews. Isolated in `Vog.CatchUp.Host`; the spike happens before the build.
- **Hands-free in agent mode is unverified.** Spike first; if it fails, report back rather than ship a degraded mode.
- **Fan-reported limits:** vibration isn't on iOS, and the browser's generic HRTF confuses front and back, so the ball sound uses stereo pan and pitch only.
- **Immersive Reader:** a token endpoint for anonymous users (rate-limited); Tamil read-aloud to be confirmed.
- **Scope:** Fabric, Insight Desk and recap move after these features.
