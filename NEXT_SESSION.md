# Next Session Handoff

Updated: 23 October 2026.

## Latest Review: Local Browser Fixes

The user subsequently requested a codebase/documentation review and live Playwright testing with fixes. This review made **no commit, push or deployment**; earlier release authorization below is historical, not authorization for another rollout.

- Four project documents were corrected against source: README, this handoff, BUILD_PLAN and the accessibility ledger. Official hackathon rules and vendored documentation were preserved.
- Local browser fixes cover 320px overflow (including hidden chart tables), localized stadium-entry tags, per-row commentary language after channel switches, and the misleading synthetic-only footer. Changes are in `wwwroot/index.html`, `css/site.css` and `js/app.js`; server behavior, simulation timing and datasets are unchanged.
- Live backend testing confirmed English PCM/captions/playback acknowledgements, a typed score answer matching 77 minutes and 0-0, and successful sequential selection of all nine channels. Two archived Norton Borough 0-1 Brackworth Town records were readable; no completed-match archive write was observed.
- Playwright local asset overrides verified the fixes without deploying: no overflow at confirmed actual widths 320/390/1280, both tables with 16 rows, nonblank canvases, three localized entry tags, preserved old-row language, persisted preferences and keyboard zoom/follow. Screenshot tooling changed the effective viewport and produced inconsistent crops, so full screenshot acceptance remains open. No microphone, concurrent multilingual audio or complete accessibility audit was performed.
- Fresh gateway Release suite: **15 passed, zero failed or skipped**, SDK 10.0.400. Browser files and Markdown diagnostics plus `git diff --check` passed. No fresh full-solution or Bicep run was needed for these frontend/documentation changes.
- Review the uncommitted worktree before continuing. Obtain current approval before committing, pushing or deploying. Real historical-data eligibility, native-speaker review, accessibility co-design, queue hardening and short-demo natural scoring remain open.

## Current Release Handoff

**Purpose:** accessible, multilingual live commentary, captions, Calm mode and grounded companion Q&A over synthetic, football-realistic events. Do not turn this into a general historical football dashboard or change realism to manufacture a short demo.

- The user authorized the 23 October deployment and storage/chart completion, then explicitly requested commit, push and a fresh deployed Playwright test. Base `83229d0` contains feature commit `ab62f5c`, which added charts, archive and DataPrep on 22 October but no storage infrastructure. Storage/mobile fixes and documentation are in `0fc5655`; chart placement and the final deployed verification record are in `8ede73d`. The subsequent documentation review started with a clean worktree and `main` and local `origin/main` at `8ede73d`; no remote fetch was performed. No new branch is required.
- The user requested charts at the page end immediately before latency; `8ede73d` moves the unchanged panel below captions/companion. At the recorded release check, latest ready revision `ca-vog-dev-gateway--charts-bottom-20261023` received 100% traffic; immutable image/public URL are recorded in [README.md](README.md#current-azure-release-23-october-2026). The documentation review did not recheck Azure. Natural defaults remain seed 20261026, speed 1, break 120. Existing Foundry registrations, secrets and non-storage settings were preserved during that release.
- Added `infra/modules/storage.bicep` and wired `infra/main.bicep`. Account `stvogdevy6po56` has private `vog-data`; shared-key and anonymous blob access are disabled. Gateway/developer contributor roles are scoped to the container. `StorageConfiguration` handles normalized environment keys and literal local settings.
- Uploaded the credited Kaggle results dataset privately and ran DataPrep: 49,945 rows, zero skipped, 16 decades per aggregate. All four supplied CSVs hash-match the download; only results feeds the charts. Both chart endpoints return 200; desktop and final deployed mobile pixel/table checks pass without horizontal overflow.
- All 15 gateway tests passed, including three endpoint-configuration tests; Bicep compilation, touched-file diagnostics and whitespace validation passed. These are not a fresh full-solution or complete accessibility audit. Initial release verified English PCM/captions and a scoreboard-grounded typed companion answer; final frontend release reconnected live state/pitch.
- At the release check, the archive response was empty. Archiving is best-effort after a full synthetic match completes; missing storage or read failures also produce an empty history response. Do not infer storage health from an empty array, seed fake recent results or claim a verified archive write/read round trip.
- **Submission blocker:** the historical panel uses real international results, whereas the official rules require synthetic data. Licensing does not resolve eligibility. Resolve removal from the submission or replacement with honestly labeled simulator-derived data before claiming compliance. The live engine/companion remains synthetic and does not ingest Kaggle results.
- Three natural goals in 120 seconds remains unresolved. Keep original timing/realism boundaries and the rejected 60x history below. Complete co-design/screen-reader, concurrent multilingual playback, queue hardening and runtime security review separately.

### Current Resume Commands

Use the confirmed SDK 10.0.400 at `C:\Program Files\dotnet\dotnet.exe`, not the old LOCALAPPDATA path. MTP/xUnit v3 uses no extra test-argument separator. Never print credentials or run the full provisioning script merely to update the image.

```powershell
git status --short
& 'C:\Program Files\dotnet\dotnet.exe' test --project tests/Vog.Gateway.Tests --configuration Release
az bicep build --file infra/main.bicep --stdout
git diff --check
```

Check Azure readiness/traffic and hard-refresh existing browser tabs after changes; unversioned scripts may be cached. Review worktree and branch tracking on resume. All seven project Markdown files were updated; official rule text is preserved beneath repository notes and vendored SignalR documentation is unchanged. The initial commit needed no rollout; the subsequent chart-placement change was rebuilt and deployed. Fresh Playwright checks on that final revision passed desktop/mobile order, nonblank canvases, 16-row tables and no overflow, plus English PCM/captions/playback and a scoreboard-grounded typed answer. The original test tab stalled on persistent request routing; a fresh tab worked. No application routing workaround was added.

## Historical Corrective Handoff: 21 October 2026

The following records the state and authorization boundaries on 21 October only. It is superseded by the current release handoff above; old process paths, local ports and deployment limitations are not current instructions.

**This section supersedes all accelerated-default claims, resume instructions and release authorization in the historical record below.** The user rejected global 60x playback. The user authorized committing and pushing the correction, then updating and pushing this handoff. Neither request authorizes deployment, infrastructure, secret or Foundry registration changes. The correction is verified locally only; obtain current approval before any deployment. Preserve the SignalR.Client 10.0.13 security fix and useful Stopwatch pacing/catch-up.

### Git Status And Next Steps

- Corrective application commit **95aeb90**, `Restore natural match playback and improve pitch visibility`, was committed and successfully pushed to `origin/main` at https://github.com/Daniel-Melchizedek/soccer-ai on 21 October 2026. It includes the implementation, tests, goal-timing command, standalone unintegrated speech queue and corrective documentation.
- After that push, `git status --short --branch` confirmed a clean worktree and `main` synchronized with `origin/main`. It was checked again before this documentation-only handoff update. The handoff update is a separate follow-up commit; use `git log -2 --oneline` and `git status --short --branch` to confirm both on resume.
- No deployment was performed. Azure's last recorded release is still the historical 60x image below; its current live revision has not been rechecked. Do not confuse a Git push with a Container App rollout.
- First remaining acceptance checks: obtain trustworthy pitch screenshots and verify the companion's MCP match context. Concurrent multilingual playback, rapid-switch race resilience and runtime security review also remain open. The 119-test and Playwright results below belong to the corrective application verification; they were not rerun for this documentation-only update.
- Three natural goals within 120 real seconds remains an unresolved requirement, not a completed feature. Obtain approval before changing scoring realism, restart timing or presentation format. Speech queue integration is also unfinished; preserve the standalone queue without claiming resilient live delivery.

### Implemented Locally

- Restored code and JSON defaults: seed 20261026, Speed 1.0, BreakSeconds 120. At 1x all narration intents and normal preemption remain enabled. Explicit accelerated configurations and their honestly named tests remain supported.
- Pitch geometry uses one uniform metres-to-screen scale and a centered plot with edge allowance and deliberate zoom clipping. Ball drawn last: solid yellow core, dark borders and separated white ring, 28 CSS pixels overall at every zoom/DPR. Nearby carrier numbers move away from the ball. Shapes, player numbers, hatching, keeper squares, coordinates, zoom 1-3, follow, descriptions and reduced-motion throttling remain.
- Added bounded offline `goaltiming` CLI output: raw goal PTS and score changes, first kickoff, two opening-window definitions and minimum later triple span. It does not modify simulator outcomes or production playback.
- PendingSpeechQueue and its six tests were started before this corrective pivot. They are present and tested, but the queue is NOT integrated into SpeechWorker. Existing unbounded-queue and synthesis-failure risks remain; do not describe queue hardening as complete.

### Verified Results

- Current worktree was checked; existing edits were preserved. Simulator realism/determinism, shared-schedule implementation, state/pitch contracts, companion implementation, infrastructure and security dependency remain unchanged.
- Full Release solution build passed using `--artifacts-path TestResults/corrective-build` because the pre-existing 5090 Release process locks standard build outputs. Final full Release suite: **119 passed, zero failed or skipped**. GoldenMatch focused suite: 19 passed, including new natural pacing, reread catch-up/state reconstruction with earlier-callback suppression, and exact two-minute break boundaries. Explicit 60x nine-channel timing coverage still passes; that is pipeline coverage, not nine concurrent Azure speech streams.
- Corrected Release gateway started via VS Code task **Run corrective gateway** at **http://127.0.0.1:5091/**, using source content root and seed 20261026. Startup joined the existing shared schedule at 14.4 minutes, not a per-listener kickoff. Existing http://127.0.0.1:5090/ process was left running and still uses old 60x binaries. Its source-served static assets are not evidence of corrected server defaults.
- Actual gateway `run_playwright_code`: 11 state messages over 10 wall seconds, DOM clock 14:58 to 15:08; pitch PTS 898,100 to 908,300 ms (52 updates). All states speed 1, score 0-0, no captured browser errors. Initial sample was dead-ball and had no narration. A later 15-second English audio-description sample delivered tackle and foul utterances, 55 PCM chunks, two audioEnd messages, two caption sets, one cut and **two outgoing played acknowledgements**. These are browser worklet-start acknowledgements, not subjective audio/caption quality validation.
- Reload clock 16:15 before and 16:17 after: shared match did not restart. Local Score readout returned text and 28 answerAudio chunks for "16 minutes gone. Reds 0, Blues 0", consistent with local state. Foundry companion grounding was NOT verified against the corrected local scoreboard; its registered MCP endpoint may still read deployed 60x state. Do not claim synchronization or alter registrations without approval.
- All nine language/mode combinations confirmed selected channel status when each join was awaited. Rapid-switch sampling showed status lag; stable joins passed. Concurrent multilingual playback and rapid-switch race resilience remain unverified.
- Isolated same-origin renderer fixture, imported without app.js/SignalR: **72 pixel/interaction cases passed**, using widths 1440, 1280 and 390, DPR 1/2, center/corners/goals/touchlines, ball over player/white markings, yellow/white kits, reversed attack/away possession, zoom 1/3 and follow off/on. Yellow core and dark/white ring pixels verified; no fixture overflow. Keyboard Enter zoom and Space follow worked. With browser time advanced, reduced-motion redraw throttle held below 500 ms, redraw resumed beyond 500 ms, and descriptions refreshed beyond five seconds, including null possession.
- Non-text contrast calculations using renderer colors: yellow core/dark border 13.29:1, white ring/dark border 19.03:1, white ring/pitch 16.22:1, field lines/pitch 13.37:1. This is not complete accessibility compliance or screen-reader/co-design validation.
- Actual page layout: no horizontal document overflow at 1440x900, 1280x900 or 390x844. Canvas sizes respectively 1186x612, 1155x612 and 283.5x183.59 CSS pixels. A preliminary inherited viewport overflow observation did not reproduce at these explicit sizes.
- Screenshot captures were attempted, but integrated screenshot output showed compressed/unrelated page fragments inconsistent with measured canvas dimensions. Canvas-download export also timed out. **Visual screenshot acceptance remains unverified**; pixel and interaction evidence should not be presented as a complete visual review. Generated screenshot/report artifacts in TestResults are ignored, not submission assets.
- Editor diagnostics for touched gateway, tests and replay code were clear. Repeat `git diff --check` before committing.

### Natural Scoring Gate

- Default seed 20261026: first kickoff PTS 12,000 ms; one goal at 4,857,200 ms, score 0-1. Seed 3 raw goal PTS: 2,539,100 (1-0), 3,605,400 (1-1), 4,879,100 (1-2), 5,442,000 (2-2); shortest triple span 1,836,600 ms.
- Seeds 1-100: zero three-goal opening candidates from either simulation start or first kickoff; zero later-window candidates. Best seed 36 triple span **246,800 ms**, not within 120 seconds. Raw generated report: `TestResults/goal-timing-seeds-1-100.jsonl` (ignored).
- Under current rules, three goals in 120 seconds from simulation start cannot fit: opening delay 12 seconds plus two minimum 55-second post-goal waits = 122 seconds before actual play. First-kickoff-relative and later windows are not proven universally impossible, but this bounded search found none. Every arbitrary sliding 120-second window is incompatible with current waits and 90-minute realism. No xG changes, shorter restarts, forced scores, join-triggered resets, disguised time jumps or highlights were introduced.
- **Normal speed does not solve the three-goal requirement.** Approval is required for a different format/realism tradeoff. Do not release this as a supposedly complete three-goal correction. Video must remain strictly under two minutes, with fictional teams/players and synthetic football-realistic outcomes.

### Historical Resume Commands

The following commands record the 21 October environment. Its LOCALAPPDATA SDK path is obsolete; use the current commands above instead.

```powershell
git status --short
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build VoiceOfTheGame.slnx --configuration Release --artifacts-path TestResults/corrective-build
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" test --solution VoiceOfTheGame.slnx --configuration Release --artifacts-path TestResults/corrective-build
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" test --project tests/Vog.GoldenMatch.Tests --configuration Debug
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" run --project tools/Vog.Replay -- goaltiming --seeds 100 --seed 1
git diff --check
```

Check whether task Run corrective gateway is still running before starting another gateway. Use 5091 for corrected live testing, not 5088 static preview or the old 5090 binary. Await channel status when changing modes. Resolve screenshot capture and verify companion MCP source before claiming full acceptance. Installed/deployed ASP.NET Core runtime security review remains outstanding; the SignalR package patch alone does not patch shared runtimes.

## Historical Record: Rejected 60x Release

Everything below documents earlier work only. Its defaults, timing claims, resume commands and deployment approval are not the current requirements.

### Historical User Requirement

The app should show at least three goals within two minutes of viewing. The user explicitly rejected a separate demo mode: this must be the app's default behavior. The video submission must be less than two minutes; three goals is the user's requirement, not an official hackathon rule. Keep the data synthetic and the clubs and players fictional.

The prior session committed and pushed the implementation as b60e1ce. This verification session must also preserve software best practices and hackathon rules. After verification, the user explicitly authorized committing, pushing and deploying the verified changes to the existing Azure Container App. Deployment results must be checked before treating the release as complete.

## Azure Release: 21 October 2026

- Application commit 759aa26043c24958c92961a74c269218d4b77c38 was pushed to origin/main.
- ACR build dtg successfully published vog-gateway:759aa26-20261021061006 from the clean worktree. The build pulled fresh .NET 10 SDK/runtime base images; their exact security coverage has not been audited.
- Deployed immutable image crvogdevy6po56.azurecr.io/vog-gateway@sha256:cea54fae683a527e6bdfbae07302b91e5799fa5c7087e3d3f69a7eed549854c7 to the existing app in rg-vog-dev, without provisioning infrastructure or changing secrets/Foundry registration.
- Revision ca-vog-dev-gateway--r759aa26-061158 is Healthy and latestReadyRevisionName, with 100% traffic in Single revision mode. The previous revision was deactivated. Both the revision-specific and public /healthz endpoints returned status ok.
- Public URL: https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/ . A fresh browser connection receives speed 60 and shows the 60x label. Previously open pages retained old JavaScript; hard-refresh after deployment.
- A fresh English audio-description listener on Azure received three natural goals with PCM worklet playback acknowledgements at 30.7, 48.5 and 69.7 seconds after Start listening. The scoreboard advanced to 1-2, and no browser errors were captured. This is one successful end-to-end sample, not subjective audio validation or an outage SLA. Recording observed real traffic without modifying simulation outcomes.

## Implemented

- Gateway defaults now use simulator seed 3, match-clock speed 60x, and zero between-match break. This is continuous accelerated match playback, not a highlights mode and not injected scoring.
- Seed 3 naturally produces four goals at presentation times 2,539,100; 3,605,400; 4,879,100; and 5,442,000 milliseconds. At 60x these occur approximately 42, 60, 81, and 91 seconds after kick-off.
- LiveMatchRunner uses Stopwatch-based elapsed-time pacing instead of a separate PeriodicTimer wait for every tick. This avoids accumulating timer-resolution delays at high speeds. Catch-up still steps earlier ticks without broadcasting their commentary.
- BroadcastOptions.BreakSeconds replaces the hard-coded two-minute gateway break. The app defaults to zero; MatchSchedule itself is otherwise unchanged.
- At speeds above 4x, audio-channel utterances are limited to Goal and PeriodChange intents. High-speed simulation preemption no longer cuts those spoken calls. Text-only listeners still receive all booth-started commentary. Speech synthesis itself is not accelerated.
- StateMessage carries the configured speed, and the browser displays it beside the synthetic-match label after receiving state data.
- README documents the defaults, configuration overrides, and reduced spoken commentary at accelerated speeds.

## Changed Files

- README.md
- src/Vog.Gateway/BroadcastOptions.cs
- src/Vog.Gateway/LiveBroadcastService.cs
- src/Vog.Gateway/Messages.cs
- src/Vog.Gateway/appsettings.json
- src/Vog.Gateway/wwwroot/index.html
- src/Vog.Gateway/wwwroot/js/app.js
- src/Vog.Pipeline/LiveMatchRunner.cs
- tests/Vog.Gateway.Tests/BroadcastOptionsTests.cs
- tests/Vog.GoldenMatch.Tests/DemoMatchTests.cs

Despite the name DemoMatchTests, there is no separate production demo mode.

## Verification Completed

- This session installed the pinned SDK 10.0.400 at C:\Users\Admin\AppData\Local\Microsoft\dotnet. Invoke that executable explicitly because the system installation still contains older SDKs. Included runtimes are 10.0.11; their security patch level still needs review before release.
- Release solution restore/build initially failed with NU1903 for SignalR.Common 10.0.12 (GHSA-334f-vvp4-7g6p). Updated the centrally managed SignalR.Client dependency to 10.0.13, without suppressing auditing. Release solution build then passed. This package update does not patch the installed or deployed ASP.NET Core shared runtime.
- Full Release solution suite passed: 110 tests, zero failures or skips. The previously cancelled long gateway timing test passed individually and in the full suite.
- Strengthened DefaultRateDeliversThreeGoalCallsWithinTwoMinutesWithAllChannelsRunning to assert three calls on EACH of the nine language/mode channels, rather than counting only English audio description. The strengthened test compiled and passed in approximately 82 seconds. It verifies pipeline callbacks and scoreboard totals, not nine concurrent Azure speech streams.
- Azure CLI authentication was confirmed using C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd. Restored git-ignored .env.local with existing non-secret Azure endpoints only; Key Vault supplies the MCP secret. The current gateway started successfully at http://127.0.0.1:5090/.
- A real local English audio-description listener received four natural goals within its first 120 seconds. Browser PCM worklet playback acknowledgements occurred at 28.6, 46.4, 67.6 and 77.0 seconds after Start listening. First audio and playback followed goal messages by approximately 0.35-0.38 seconds. No browser errors were captured. The listener joined an already-running shared match, not a fresh per-listener fixture.
- Playwright found no document horizontal overflow at 1280px desktop and 390px mobile widths, and verified the live 60x label. The mobile label wraps into a narrow column; complete responsive usability and subjective audio/accessibility testing remain pending.
- Editor diagnostics found no errors in the dependency file or app.js. This session could not rerun Node's syntax check because Node was unavailable on PATH and at its standard installation location. The earlier session's successful check is historical evidence only.

Historical checks from the implementation session:

- Golden-match project: all 16 tests passed, including after the final runner formatting changes.
- Gateway project: five tests passed before adding the long timing test (two new options/policy tests and three existing pitch-message tests).
- Immediately before the handoff, the two options/policy tests passed again. The test project compiled with the new long timing test included, but that test was not executed to completion.
- The fixture test verifies four natural goals and at least three goal events in every two-minute window of its repeated, zero-break 60x schedule, including windows crossing a match boundary. This is a schedule/event check, not proof of live Azure speech delivery.
- The two-second runner test verifies actual high-speed progress, but uses no commentary channels.
- app.js passed Node's syntax check. The touched runner, gateway service and browser module had no editor diagnostics at the time checked.
- git diff --check passed before the handoff document was added; repeat before committing.
- The shared browser preview loaded, and Playwright confirmed the match-speed element exists. No live state, live goal timing, Azure speech or final responsive speed-label screenshots were verified.

## Pending Checks And Blockers

1. The former SDK, local-endpoint and CLI-authentication blockers were resolved this session. Keep .env.local ignored and never expose secrets. Renew authentication as needed; do not provision resources merely to restore local configuration.
2. The nine-channel pipeline timing check now passes. Actual concurrent multilingual Azure speech, transient outages, slow clients and resource-pressure behavior remain unverified.
3. The page at http://127.0.0.1:5088/ is a static preview, not the gateway. Use http://127.0.0.1:5090/ for local live verification.
4. Review and patch installed/deployed ASP.NET Core shared runtimes as appropriate. The SignalR client package fix alone is not a complete runtime security update.
5. Azure now runs the release recorded above, with the existing one 0.5-vCPU/1GiB replica and no Broadcast overrides. Local and deployed gateways must use identical seed, speed and break settings because the companion's MCP tools can read the deployed gateway rather than the local instance; even identical settings do not prove synchronized clocks/state.
6. At the default rate, detailed spoken audio description and mode-specific routine commentary are filtered out. Ball sonification, pitch data and quick readouts remain available. Validate this usability tradeoff with the user and actual listeners; do not describe it as equivalent to full real-time audio description. Speech queues remain unbounded; P0 retention can build backlog during outages, and synthesis failures currently lose a call without retry. The successful local sample is not proof of resilient delivery.
7. The schedule guarantee assumes the configured fixture/rate and an operating gateway. Startup catch-up, CPU pressure, SignalR latency, Azure warm-up and audio playback are not covered by the synthetic viewing-window test. Arbitrary seed/rate/break overrides invalidate the guarantee.

## Submission Guardrails

- Follow hackathon-rules/about-challenge.md and hackathon-rules/submission-guideline.md throughout. Use synthetic football-realistic data, fictional teams/players and natural simulator outcomes, never injected scores or unauthorized real match data.
- The video must be strictly less than two minutes and show the functioning project. The three-goal cadence is a user requirement, not a judging rule. Keep the GitHub repository public for submission and exclude unauthorized trademarks, copyrighted footage/music and confidential information.
- Show useful explanation beyond scores and genuine personalization/AI behavior where implemented. Do not claim full audio-description equivalence at 60x, accessibility compliance, outage guarantees, multi-agent collaboration or hosted recap features that have not been demonstrated.
- Do not demonstrate local companion answers as matching the local scoreboard until its MCP data source is confirmed consistent. The user authorized this release; update only the existing gateway image, preserve secrets and infrastructure, and verify the live revision before claiming deployment success.

## Resume Commands

Run from the repository root in PowerShell. The repository pins .NET 10 and Microsoft.Testing.Platform; xUnit v3 uses --filter-method, not VSTest filter expressions.

```powershell
git status --short --branch
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" test --project tests/Vog.GoldenMatch.Tests
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" test --project tests/Vog.Gateway.Tests --filter-method '*DefaultRateDeliversThreeGoalCallsWithinTwoMinutesWithAllChannelsRunning' --output Detailed
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" test --solution VoiceOfTheGame.slnx --configuration Release
```

After restoring existing Azure configuration and authentication:

```powershell
$env:PATH = "C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin;$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" run --project src/Vog.Gateway --configuration Release --no-launch-profile --urls http://127.0.0.1:5090
```

The launch profile uses http://localhost:5080. If occupied, pass --no-launch-profile --urls http://127.0.0.1:5090. Start listening on the real gateway, check the 60x label, record score increases for less than two minutes, confirm spoken goal calls do not trail the display, and verify mobile/desktop layout. Do not use the static preview for live verification.

Git was missing from one terminal's PATH after a session change. Installed executables were confirmed at C:\Program Files\Git\cmd\git.exe and C:\Program Files\dotnet\dotnet.exe; invoke them explicitly if needed. PowerShell 5.1 does not support &&.

## Online Reference

Microsoft Learn's Task.Delay documentation states that short delays depend on system-clock resolution, approximately 15 milliseconds on Windows. Stopwatch provides elapsed-time measurement using a high-resolution performance counter when available. These references support the pacing change, not the football scoring guarantee:

- https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.delay?view=net-10.0
- https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch?view=net-10.0