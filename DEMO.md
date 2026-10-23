# Voice of the Game: Actual-Device Demo

Target **110 seconds**, strictly **under 120 seconds**. This is a recording plan, not a completed demo or acceptance report. Recap deployment and Playwright checks are **in progress, not yet completed**; use [latest release verification](README.md#latest-recap-release-verification-pending) to identify the actual verified build before recording. Updated Foundry YAML/tool registration is still pending, so do not present a typed recap as deployed until checked.

## Recording Plan

Use a real phone or laptop with audible output and readable captions, not a mocked browser. Show the actual device/browser, match clock and score. Capture one continuous natural-speed session; the time budget below allocates presentation time, not match events. Close private tabs/notifications and rehearse recording levels. Use the current shared match without resets, acceleration, injected goals, fabricated evidence or disguised time jumps. An empty interval is a valid result.

| Elapsed budget | Actual interaction and narration |
|---|---|
| 0-20 s | Select English AudioDescription and **Start listening**. Show captions and the live pitch; let an actual utterance play if one arrives. Introduce accessible commentary over a fictional synthetic match. Silence or dead-ball time is not a reason to fake narration. |
| 20-40 s | Open recent insights in Casual. Show the scope bounds and scope-end score, then an available highlight and its source ID. Explain that recent means up to two match-clock minutes, clamped to the current half. If there are no events, show the empty state. |
| 40-60 s | Switch to Analyst for the same scope. Show the last-40 highlight list, raw event-count table, native shot meters and expandable IDs, versus Casual's up-to-five decisive/latest selection. The panel is English; these are views, not additional listening channels. Evidence/focus pauses automatic refresh. |
| 60-85 s | Type **recap last two minutes** in the companion. Compare any returned scope, score and cited evidence with the panel. A later snapshot may have advanced: compare bounds, not just wording. Do not fake an answer if registration, latency or service availability prevents it; show the real pending/error state and disclose the limitation. |
| 85-95 s | Select halftime. Before half-end, show unavailable; after half-end, show its fixed first-half scope instead. State that fulltime waits for final whistle. Do not wait for or manufacture a period transition. |
| 95-110 s | Close with provenance and architecture: seeded simulator -> deterministic stats/Booth -> SignalR audio/captions/pitch; shared snapshot -> recap HTTP/MCP -> panel or Foundry/Voice Live companion. State the remaining human-validation gaps. Stop recording by 110 seconds to leave margin. |

If live latency consumes the budget, shorten narration, not match time. No goal is promised. The separate three-goals-in-120-seconds goal remains unresolved. Hosted workflows, Fabric and dedicated overlay/Studio are not delivered features.

## Synthetic Data Card

- **Generator:** custom seeded deterministic C# football simulator. Current defaults: seed `20261026`, natural speed `1`, `120`-second between-match break. Determinism describes generated events for the same inputs; it does not make cloud answers, network latency or join time deterministic.
- **Identity and media:** fictional generated league, clubs and player names. No actual club/trademark footage or real match data is used in this experience; pitch imagery is rendered from synthetic tracking and event audio is procedural. Do not introduce real club crests, footage or unlicensed music into the recording.
- **Historical provenance:** removed charts previously used Mart Jurisoo's CC0 international results and Chart.js, with Brian Benz's dashboard as inspiration. See [credits](README.md#credits-and-attributions). Legacy DataPrep and private historical blobs remain unused; no real historical data is used in the submitted app. This does not erase prior usage or guarantee full hackathon eligibility.
- **Meaning and limits:** recaps count raw key events from one immutable snapshot, show source IDs and scope-end scores, and explain recorded goal score effects/dismissals. They infer no window xG/possession, tactics, momentum or probabilities. Elsewhere, xG is a synthetic shot-quality estimate, **not probability of victory**, betting advice or a validated prediction of real football.

## Captions And Privacy

Word-timed captions and caption controls are implemented; actual synchronization, readability and screen-reader compatibility still need human validation. The insights panel is English despite the three supported language parameters; Spanish/Tamil phrase banks remain draft. Do not claim fully localized or universally accessible output.

Use typed input for this short demo; do not imply that it tests the microphone. Optional voice input needs permission and sends captured audio through the gateway to Azure Voice Live; typed questions also leave the device for companion processing. Caption preferences are device-local, not a guarantee that all app data stays local. Do not enter personal/sensitive information or expose credentials, private tabs or notifications. Cloud retention/logging boundaries require review; do not claim zero retention or that `no-store` on `/insights` is a service-wide privacy guarantee.

## Human Validation Checklist

- [ ] Record actual build/revision, device, browser, date and observed results; distinguish deployed checks from local asset overrides.
- [ ] Human screen-reader and keyboard check: control names, focus order, unavailable/empty states, evidence IDs, native meters/table, refresh pause and resumed updates without lost focus.
- [ ] Low-vision/mobile check: text size, zoom/reflow, contrast and visible pitch/captions without overlap; involve intended users rather than treating automation as certification.
- [ ] Native Spanish/Tamil reviewers assess phrase banks and companion recap grounding; disclose the English panel.
- [ ] Test real microphone permission, start/stop/silence, cancellation and typed alternative on target devices; review privacy/logging behavior separately.
- [ ] Listen subjectively for intelligibility, mix, interruption and caption alignment; separately test concurrent multilingual playback.
- [ ] Observe a completed synthetic match archive write/read round trip; readable older records or an empty `/history` response do not prove it.
- [ ] Verify release, registration and deployed scope/evidence behavior, then review all eligibility, attribution, public-repository and video requirements. Synthetic-only data is not full approval.