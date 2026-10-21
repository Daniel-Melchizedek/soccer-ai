# Next Session Handoff

Updated: 21 October 2026.

## User Requirement

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