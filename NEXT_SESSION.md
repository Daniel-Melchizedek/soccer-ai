# Next Session Handoff

Updated: 21 October 2026.

## User Requirement

The app should show at least three goals within two minutes of viewing. The user explicitly rejected a separate demo mode: this must be the app's default behavior. The video submission must be less than two minutes; three goals is the user's requirement, not an official hackathon rule. Keep the data synthetic and the clubs and players fictional.

The latest instruction was to commit and push the current implementation and document enough context to resume. No Azure deployment was requested or performed.

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

- Golden-match project: all 16 tests passed, including after the final runner formatting changes.
- Gateway project: five tests passed before adding the long timing test (two new options/policy tests and three existing pitch-message tests).
- Immediately before the handoff, the two options/policy tests passed again. The test project compiled with the new long timing test included, but that test was not executed to completion.
- The fixture test verifies four natural goals and at least three goal events in every two-minute window of its repeated, zero-break 60x schedule, including windows crossing a match boundary. This is a schedule/event check, not proof of live Azure speech delivery.
- The two-second runner test verifies actual high-speed progress, but uses no commentary channels.
- app.js passed Node's syntax check. The touched runner, gateway service and browser module had no editor diagnostics at the time checked.
- git diff --check passed before the handoff document was added; repeat before committing.
- The shared browser preview loaded, and Playwright confirmed the match-speed element exists. No live state, live goal timing, Azure speech or final responsive speed-label screenshots were verified.

## Pending Checks And Blockers

1. The full nine-channel, actual-60x timing test DefaultRateDeliversThreeGoalCallsWithinTwoMinutesWithAllChannelsRunning was added, but its tool execution was cancelled by the user. Do not report it as passed. It normally takes about 82 seconds and times out after 115 seconds. It verifies pipeline goal calls and scoreboard totals, not Azure audio.
2. Starting the real gateway on port 5090 failed because VOG_KEY_VAULT_URI was not set. No .env.local file was found by workspace search. LocalSettings searches upward from the content root for that file. Restore existing local configuration safely; do not commit credentials or provision paid resources merely to bypass this blocker.
3. The shared page at http://127.0.0.1:5088/ is a Python static preview, not the gateway. Its SignalR negotiation returns HTTP 501 on POST. It cannot run the match or prove the new goal cadence.
4. Prior terminal context showed an unsuccessful az login. Check current authentication before Azure-dependent work; do not assume the session is signed in.
5. The Azure deployment has not been updated. Local and deployed gateways must use identical seed, speed and break settings because the companion's MCP tools can read the deployed gateway rather than the local instance.
6. At the default rate, detailed spoken audio description and mode-specific routine commentary are filtered out. Ball sonification, pitch data and quick readouts remain available. Validate this usability tradeoff with the user and actual listeners; do not describe it as equivalent to full real-time audio description. Speech queues remain unbounded, and end-to-end goal/audio backlog needs a live check.
7. The schedule guarantee assumes the configured fixture/rate and an operating gateway. Startup catch-up, CPU pressure, SignalR latency, Azure warm-up and audio playback are not covered by the synthetic viewing-window test. Arbitrary seed/rate/break overrides invalidate the guarantee.

## Resume Commands

Run from the repository root in PowerShell. The repository pins .NET 10 and Microsoft.Testing.Platform; xUnit v3 uses --filter-method, not VSTest filter expressions.

```powershell
git status --short --branch
dotnet test --project tests/Vog.GoldenMatch.Tests
dotnet test --project tests/Vog.Gateway.Tests --filter-method '*DefaultRateDeliversThreeGoalCallsWithinTwoMinutesWithAllChannelsRunning' --output Detailed
dotnet test --project tests/Vog.Gateway.Tests
```

After restoring existing Azure configuration and authentication:

```powershell
dotnet run --project src/Vog.Gateway
```

The launch profile uses http://localhost:5080. If occupied, pass --no-launch-profile --urls http://127.0.0.1:5090. Start listening on the real gateway, check the 60x label, record score increases for less than two minutes, confirm spoken goal calls do not trail the display, and verify mobile/desktop layout. Do not use the static preview for live verification.

Git was missing from one terminal's PATH after a session change. Installed executables were confirmed at C:\Program Files\Git\cmd\git.exe and C:\Program Files\dotnet\dotnet.exe; invoke them explicitly if needed. PowerShell 5.1 does not support &&.

## Online Reference

Microsoft Learn's Task.Delay documentation states that short delays depend on system-clock resolution, approximately 15 milliseconds on Windows. Stopwatch provides elapsed-time measurement using a high-resolution performance counter when available. These references support the pacing change, not the football scoring guarantee:

- https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.delay?view=net-10.0
- https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.stopwatch?view=net-10.0