# Next Session Handoff

Updated: 26 October 2026 (session date; latest rollout telemetry reports 27 October UTC).

## Latest Release: Kickoff And Team-Coloured Goalkeepers

The user authorized Azure deployment, then requested keeper colours matching their teams and ordinary commit/push once fixes were done. The tested fixes are deployed and supersede the fixed global-epoch behavior described in older records below. Use `git log -3 --oneline`, `git status --short --branch` and the remote branch to confirm the release commit; no branch/history rewrite is needed.

- `LiveBroadcastService` waits for the first valid listener (including text-only) before starting at zero. `CommentaryHub.Join` waits for match initialization. Later/concurrent joins, channel switches and Stop/Start join the same match without resetting it. Match cycles continue with natural defaults: seed `20261026`, speed `1`, break `120`, nine channels. A gateway restart creates a new first-listener kickoff; separate gateways are not automatically synchronized.
- `MatchSchedule.StartsAt` is configurable and defaults to the original epoch for existing consumers. Four added tests cover explicit kickoff, the natural-rate break, first-listener startup, later/concurrent joins without reset, and shutdown before any listener.
- Goalkeepers use team-coloured square markers, `H1`/`A1` (home/away shirt number) and opposite diagonal hatching. The legend explains H/A. Two actual-module tests cover red/blue and swapped blue/red fills, keeper labels and unchanged outfield numbers. Ball colour remains yellow.
- Fresh verification: **136 .NET Release tests** and **26 Node browser tests** passed, zero failures/skips. Commands: `dotnet test --solution VoiceOfTheGame.slnx --configuration Release --artifacts-path TestResults/listener-kickoff`; `node --experimental-vm-modules --test tests/browser/insights.test.mjs tests/browser/catchup.test.mjs tests/browser/pitch.test.mjs`. No C# behavior changed after the full suite; the latest image also published successfully in ACR.
- Final ACR run **`dts`**, completed at telemetry time `2026-10-27T05:49:00Z`, deployed immutable image `crvogdevy6po56.azurecr.io/vog-gateway@sha256:f03affe3356749d7f9ed2accaed4567f2a3eebbf9688d37e5675f47ecf601828` as **`ca-vog-dev-gateway--teamkeepers-797caac-d53dc5d6a5ea`**. Azure confirmed Healthy, latest-ready, one replica and 100% traffic. Source was base `797caac` plus scoped working-tree fixes, built before the release commit; 117 application/build/test files matched the clean snapshot. Snapshot SHA256: `d53dc5d6a5ea6a205470eca778d532364fa5fe4b2d00a91f0a79687bd34dea2c`, preserved under `C:\Users\Admin\AppData\Local\Temp\vog-deploy-teamkeepers-797caac-6beed821\source-snapshot.tar.gz`.
- Image-only rollout preserved the configuration fingerprint `1f8584f54faedb57c0be7e35934d887d35b4a9ffc362d843efa84cc0034f3a8f`; no provisioning, secret, storage or Foundry-registration changes. Existing agents already read the deployed MCP. Local companion facts still must not be assumed to match the local simulation.
- Clean deployed check: health 200, score `Not started`, recap unavailable before join, then **00:01** on the first displayed state. Actual canvas recorded red `#e53935` and blue `#1e63ff` keeper fills with `H1`/`A1`. Stop/Start retained progress, **01:12 to 01:13**. A bounded English AD observation received **306 PCM / 20 captions / 28 answer-audio messages**; the score shortcut's "Inside the first minute. Reds 0, Blues 0" matched the scoreboard at question time. Text catch-up reported four seconds with no received commentary. These are delivery/grounding checks, not subjective audio, microphone, native-language or screen-reader certification.
- Actual widths 320/390/1280 had no horizontal overflow and nonblank pitch pixels. Screenshot output was incorrectly cropped; full visual acceptance remains open. No routes/assets were overridden; temporary canvas/WebSocket hooks were removed and test listening stopped. The deployed shared match continues, so the next listener joins in progress. Do not restart solely to manufacture another 00:00 demonstration.
- Intermediate rollout retained for provenance/rollback: ACR **`dtr`**, revision `ca-vog-dev-gateway--kickoff-797caac-98a60128d7f3`, digest `sha256:f08d73badda635c805a50ac056ce01c811606f12464e1b02fbc8d72772bac4f7`, snapshot `98a60128d7f3413cbd215046b42c832541fea5883738115c490c5285dc6497de`. It fixed kickoff/H-A labels but still used gold keepers; the final `dts` release supersedes it. Previous recap rollback: `ca-vog-dev-gateway--recaps-8cf6074`, digest `sha256:b90a7eeb4363d94d1f493de2f2102e8175880038ffc2176eb8c4ae2f99c1ab43`.

Live URL: https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/?release=teamkeepers-797caac-d53dc5d6a5ea

The local gateway remains available through the ignored task at `http://127.0.0.1:5093/`, with local archiving disabled; check its task/port before starting another process. Remaining acceptance is human audio/microphone/native-language/accessibility review, reliable full visual captures and a naturally completed archive round trip. New infrastructure or unrelated roadmap implementation needs separate authorization. The following sections are historical implementation and acceptance records, not current deployment instructions.

## Current Worktree: Pending-Feature Implementation

The user subsequently authorized implementing pending features. This section supersedes the acceptance-only and local-process instructions below. At the latest resume, the worktree was clean and `main` and local `origin/main` pointed to **`797caac`**, containing the pending-feature implementation. No remote fetch was performed. These application changes are committed but **undeployed**. This resume performed no commit, push, deployment, provisioning, secret change or Foundry registration.

- Implemented: 64-entry priority-aware speech queue integration and browser skip notification; fresh-synthesizer retry limited to one attempt before any audio; deterministic worker tests via `IStreamingSynthesizer`.
- Implemented: bounded post-answer **text-only** catch-up over actually received commentary, through playback completion; error/timeout and cancellation handling; fresh transport on channel switch and stale-callback protection. Six retained moments, 600 characters each, 120-second watchdog. No extra model calls or spoken catch-up. Sent-question errors/timeouts require Stop/Start before another question; server question-correlation tokens remain future work.
- Implemented: localized recap controls/generated labels in English, Spanish and Tamil; Spanish/Tamil marked draft, server-authored facts remain English. Language changes preserve focus and open evidence.
- Fresh validation: **132 .NET Release tests passed**, **24 Node browser tests passed**, no failures/skips. Commands: `dotnet test --solution VoiceOfTheGame.slnx --configuration Release --artifacts-path TestResults/pending-features`; `node --experimental-vm-modules --test tests/browser/insights.test.mjs tests/browser/catchup.test.mjs`. Local browser fixtures at 1280/390/320 confirmed language changes, evidence retention, source tags and no horizontal overflow/page errors. All fixture routes/content were removed. Mobile screenshots remain compressed; do not claim full visual acceptance.
- Local startup restored after the user ran `az login`: Azure CLI account state was Enabled, port 5093 was free, and no prior local task existed. The ignored VS Code task **Run gateway locally** now runs the Release gateway at `http://127.0.0.1:5093/`, with the four known nonsecret endpoints and the CLI directory on process PATH. Key Vault loaded successfully; no secrets were printed or written. Local archiving is disabled. Check the task/port before starting another gateway; this is no longer a static preview.
- Resume HTTP checks at observed environment time **2026-10-27T05:05:04.302Z**: health 200; match recap available at 77:40, 0-0; half-time available and first-half-only; full-time unavailable; recap responses `no-store`; invalid scope 400; removed goals-chart route 404. Local history was `[]` with archiving explicitly disabled, not an archive round-trip check. No new full test suite was run; the 132/24 results above belong to the implementation session.
- Read-only Azure inspection confirmed latest-ready **`ca-vog-dev-gateway--recaps-8cf6074`**, latest revision receiving 100% traffic. Integrated browser modules loaded with HTTP 200, but the page reported hidden and automation clicks did not start listening, including after `bringToFront`. No new PCM, Voice Live, microphone or visual acceptance is claimed. Temporary WebSocket instrumentation was removed; no routes or fixtures were installed. Local page ID `3f150b58-99b3-41f5-bc03-f5caa99e184c` may not survive another session.
- Next engineering work: browser braille mode/feed, Easy-read captions, spoken catch-up, server question correlation/recovery, PCM chunk bounds/backpressure, archive reliability/tests and runtime security review. Hosted Insight Desk/recap/transcreation, Fabric and Immersive Reader need API/resource/licensing decisions; new paid resources or deployment require separate approval. Dedicated overlay/Studio is not implemented. No claim that the full roadmap is finished.
- Next acceptance: open the working local gateway in a visible browser, start listening and exercise catch-up through actual answer playback, Stop/Start recovery and channel switching against speech/Voice Live, then seek deployment approval. Foundry agents still read deployed MCP; compare their answers against deployed evidence rather than assuming local state synchronization. Existing user-reported microphone checks below cover the old release, not this changed lifecycle. Preserve natural defaults: seed `20261026`, speed `1`, break `120`, nine channels. Native review, screen-reader/co-design and a natural completed archive round trip remain open.

## Resume Acceptance Check: 26 October 2026

This section records the earlier acceptance phase, before the implementation batch above. At resume, the worktree was clean and `main` and local `origin/main` both pointed to documentation commit **`95bb525`**, above deployed application **`8cf6074`**. No remote fetch or new Azure revision inspection was performed. That initial phase changed documentation only; later application edits and test runs are recorded above.

- **Concurrent live delivery:** three isolated browser contexts listened to English, Spanish and Tamil AudioDescription over the same **30,031 ms** interval starting **2026-10-26T10:32:47.736Z**. English received **25 PCM chunks / 2 caption messages / 1 playback-start acknowledgement**; Spanish **22 / 2 / 2**; Tamil **12 / 1 / 1**. Each received 30-31 state messages with speed `1`, rendered commentary rows, and had no captured page errors. This is a bounded concurrent delivery sample, not native-language, subjective listening, sustained-load or nine-channel acceptance. An earlier deferred run lost its result and is not counted.
- **Settled switching:** a separate isolated listener selected Spanish Commentary, Tamil Calm, then English AudioDescription, awaiting each expected language/mode status. All settled successfully. This establishes selection status, not post-switch audio correctness or rapid-switch race resilience. Every automation-created test context was stopped/closed; no routes or asset overrides were installed. The user's shared browser page was left available.
- **Human report:** the user confirmed **Windows 11, VS Code's built-in browser** on the shared live app and said, "when I click on start listening, I can hear the sound". After being asked explicitly about all three microphone checks, the user confirmed that **recording stopped automatically after a pause, commentary muted while speaking, and the spoken answer matched the displayed score**. In the subsequent check, the user also confirmed that **tapping again stopped recording immediately and captions matched the audible speech without noticeable delay**. These are user-reported passes in that environment, not independently captured microphone telemetry, measured caption latency or cross-device acceptance. Hold-to-talk, denied-permission fallback, answer ducking and screen-reader results remain unconfirmed.
- **Visual review remains partial:** a desktop pitch element capture showed the complete field, ball, markers and controls without obvious clipping. Analyst/evidence and mobile captures were still cropped/compressed. Actual widths 1280/390/320 had document widths 1265/375/305; at 320, the Analyst table was 251 CSS pixels wide and Casual hid it. No horizontal overflow was observed, but full visual acceptance still needs reliable captures or actual-device review.
- **Archive baseline:** at **2026-10-26T10:34:07.720Z**, health was 200 and match `m-20261026` was **33:39, first half, 0-0**. Halftime/fulltime were unavailable and recap responses were `no-store`. `/history` returned ten records; newest summary timestamp was **2026-10-26T09:58:30.6046178+00:00**, seed `20261026`, Norton Borough 0-1 Brackworth Town, match length `5689500` ms. No new completed-match write was observed. Correlate a future natural completion, its archive-success log/blob and the corresponding history read; do not infer a round trip from these older records.
- **Archive coverage limitation:** source inspection found configuration tests, not direct archive write/read tests. `LiveBroadcastService` uses a fresh wall-clock summary timestamp after runner completion and fires the archive task without awaiting it; blob names use seed plus completion time to the second. Paired history timestamps are an observation, not a diagnosed cause. Repeated seed-based match IDs do not uniquely identify a scheduled round. Any archive reliability/identity change remains a separate engineering slice requiring scope confirmation.
- **Local environment:** no listener was found on port **5093** at resume. Do not assume the prior local task is still running; check again before starting a gateway. The workspace for this session is **`C:\Users\Admin\reop\soccer-ai`**, not the historical `repo` spelling below.
- **Documentation:** corrected the stale deployment/registration preflight and release anchor in [DEMO.md](DEMO.md). The recording plan remains a target 110 seconds, strictly under 120, with no promised goals or manufactured timing.

Remaining human checks at the end of the acceptance phase: hold-to-talk, denied-permission fallback, answer ducking, native review, screen-reader/co-design and full visual review. Longer concurrent/post-switch listening and a naturally completed archive round trip also remain open. The user then authorized pending-feature implementation, as recorded above; the changed companion lifecycle needs regression acceptance.

## Historical Handoff: Recaps Deployed

The user authorized completion, commit, push and deployment, then explicitly requested a session stop and an updated handoff. Application source **`8cf6074`** is pushed and deployed, following recap checkpoint **`db16cb8`**. The final application fix makes tab-return refresh obey the same focus/open-evidence guard as the 15-second timer. Full Release solution: **128 passed, zero failed or skipped** on SDK 10.0.400; an interrupted earlier run was discarded. This handoff and README are a documentation-only follow-up, requiring no image rebuild.

**Pause checkpoint:** the user subsequently requested committing and pushing README.md and NEXT_SESSION.md together as a documentation-only follow-up to application commit `8cf6074`. This handoff is included in that follow-up; use `git log -3 --oneline` and `git status --short --branch` to identify its hash and confirm remote synchronization. The deployed image remains built from `8cf6074`; a newer documentation commit is not an undeployed application change. No build, test, deployment or Foundry registration is still running. The local gateway task remains running as described below.

- ACR **`dtq`** succeeded from a clean Git archive of `8cf6074`. Image: `crvogdevy6po56.azurecr.io/vog-gateway@sha256:b90a7eeb4363d94d1f493de2f2102e8175880038ffc2176eb8c4ae2f99c1ab43`, created `2026-10-26T06:51:43Z`.
- Azure revision **`ca-vog-dev-gateway--recaps-8cf6074`**: Healthy, latest-ready, one replica, 100% traffic, Single mode. Image-only update; no provisioning, secret rotation, blob deletion or configuration change. Seed `20261026`, speed `1`, break `120` and nine channels remain unchanged; no deployed playback overrides, live state speed `1`.
- Live URL: https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/?release=recaps-8cf6074
- Authenticated deployed MCP listed all six tools. Foundry companions registered: **en-gb v11**, **es-es v4**, **ta-in v3**, model `gpt-5.6-terra`, reasoning `none`, six-tool allowlist and existing guardrail. Direct English Foundry request logged successful `get_match_recap({"scope":"match"})`.
- Deployed Playwright: both views, saved preference, keyboard source evidence, separate 16-second focus/open-evidence pauses including tab return, synthetic 503 retry recovery, `no-store`, invalid scope/language 400, health 200 and both removed chart routes 404. Recent was exactly two minutes within half one; halftime/fulltime unavailable before their end. Local late-match check confirmed halftime isolation; final-whistle behavior is test-covered, not observed in a completed deployed match.
- Live English AD: 101 PCM chunks, six caption messages, five playback acknowledgements at 1x. Typed Voice Live recap at 10:48 accurately reported 0-0, Blues' one saved shot by Rafael Zandor and no Reds shots, matching deployed snapshots at 10:40 and 10:51 with evidence `m-20261026-e00025`; 25 answer audio chunks, no captured answer error.
- Reflow at actual 320/390/1280 widths: document 305/375/1265, no horizontal overflow; nonblank pitch pixels at each size. Screenshots remain cropped/compressed, so full visual acceptance remains open. All browser routes/instrumentation removed; listening stopped.
- At the user's explicit request, 15 Accenture author/committer email entries were corrected to `danielmelchizedekarockiathomas@gmail.com`; name remains `Daniel Melchizedek Arockia Thomas Samuel`. All 31 commit trees, names, dates and subjects were verified unchanged before lease-protected push. Other email identities were preserved. Historical commit IDs below predate this rewrite; `145bb5b` is now `db16cb8`. The isolated backup is `C:\Users\Admin\AppData\Local\Temp\vog-email-rewrite-d16707f.git`, with `refs/original/refs/heads/main`; no backup purge was performed.

### Resume Steps

1. Read this current handoff and README's latest release verification, then check `git status --short --branch` and `git log -3 --oneline` for the documentation follow-up. Preserve any subsequent user changes. Do not repeat the history rewrite or rebuild/redeploy solely for Markdown changes. The prior deployment authorization was for the completed recap release, not blanket approval for unrelated future rollouts.
2. Remaining acceptance: native Spanish/Tamil review, human screen-reader/co-design, actual microphone input, subjective audio/caption alignment, concurrent multilingual playback, full visual review and a completed-match archive write/read round trip. Existing readable archive entries are not proof of a newly observed write. Runtime security review and speech queue integration remain separate unfinished work.
3. Use [DEMO.md](DEMO.md) for an actual-device, target 110-second demo, strictly under 120 seconds. Do not promise goals or halftime arrival, accelerate playback, manufacture events or infer full eligibility from synthetic-only data.
4. Local VS Code task **Run gateway locally** remains at `http://localhost:5093/`, no listener connected, archiving disabled. Its ignored `.vscode/tasks.json` is machine-local. Check it before starting another gateway. Use a separate artifacts path if a running gateway locks Release outputs.
5. Executables: `C:\Program Files\Git\cmd\git.exe`, `C:\Program Files\dotnet\dotnet.exe`, `C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd`. AzureCliCredential needs the CLI directory on process PATH. Never print credentials. For Azure readiness use `az resource wait` with API `2025-01-01`; `az containerapp wait` is unsupported here.
6. Last successful full test command: `dotnet test --solution VoiceOfTheGame.slnx --configuration Release --artifacts-path TestResults/recap-release-20261025 --no-build`, after the initial run built all projects. Rebuild when code changes; do not reuse `--no-build` against stale outputs. Do not interrupt a backgrounded test with another command in the shared terminal.
7. Browser tools: use same-origin `fetch` in `page.evaluate` (`page.request` fails with unsupported `Storage.getCookies`); callbacks lack `URL`, so use string URL matching. Wait for actual `innerWidth` after resizing. Rediscover page IDs next session; hard-refresh older tabs with cached unversioned JS.

### Pending Work In Order

Start with these unfinished checks, not another implementation/build/registration cycle. Keep results evidence-based and update this section as checks are completed.

1. **Trustworthy desktop/mobile visual review:** screenshot output from the integrated browser was cropped/compressed despite passing DOM reflow and canvas-pixel checks. Obtain reliable full-page/element captures or have the user inspect the live app on actual devices. Check controls, captions, pitch, both recap views and expanded evidence. Do not mark full visual acceptance complete from pixel counts alone.
2. **Actual microphone and listening check:** coordinate with the user on the live app. Test tap-to-ask, automatic silence stop, manual stop, hold-to-talk, denied microphone permission/typed fallback, commentary muting while recording and answer ducking. Human listening must judge intelligibility and caption alignment; browser PCM counts and playback acknowledgements only establish delivery.
3. **Concurrent multilingual playback:** use separate browser contexts/devices for English, Spanish and Tamil, with actual listener joins and PCM/caption observations, then check language/mode switching. Prior sequential nine-channel checks and offline pipeline tests are not concurrent Azure speech acceptance. Stop all test listeners afterward. Native-language accuracy still requires fluent reviewers.
4. **Completed-match archive round trip and period boundaries:** inspect `src/Vog.Gateway/MatchArchiveService.cs` and its existing tests before choosing a focused check. Observe a naturally completed deployed match, its write result and the corresponding `/history` read, correlating match identity/timestamps. Existing ten readable historical records do not prove this release wrote one. Observe halftime isolation and fulltime availability when naturally reachable; deterministic tests already cover boundaries. Do not accelerate/reset the live match, seed fake history, delete blobs or treat an empty history array as proof of failure/success. If real-time completion cannot fit the session, record the exact pending observation instead of claiming completion.
5. **Human accessibility and native review:** use the actual-device checklist in [DEMO.md](DEMO.md) and the accessibility ledger. Arrange screen-reader/co-design and Spanish/Tamil phrase-bank/recap review with appropriate people; automation cannot certify these. Record observations, reviewer scope and unresolved issues rather than claiming compliance.
6. **Separate engineering backlog:** `PendingSpeechQueue` and six tests exist but are not integrated into `SpeechWorker`; unbounded queue and synthesis-failure risks remain. Runtime security review and rapid-switch race resilience also remain unfinished. Confirm the next engineering slice with the user before expanding into these changes; read the owning implementation and nearby tests, make a small focused fix, then run its narrow checks. The fresh container base pull is not a security audit.
7. **Submission/demo:** finish the real-device recording only after the relevant checks. Target 110 seconds, strictly under 120. Three natural goals within two minutes remains unresolved and must not be manufactured; preserve seed/timing/realism. Synthetic data alone does not establish full hackathon eligibility. Consult the actual rules and [DEMO.md](DEMO.md).

### Resume Environment

- Workspace: `C:\Users\Admin\repo\soccer-ai`; Windows PowerShell 5.1 (no `&&`). SDK `10.0.400`, Microsoft.Testing.Platform/xUnit v3. Consult the run-tests skill before choosing new test filters. Last full artifacts path: `TestResults/recap-release-20261025` (the directory name predates the observed release date).
- Azure: resource group `rg-vog-dev`, Container App `ca-vog-dev-gateway`, registry `crvogdevy6po56`, storage `stvogdevy6po56`, private container `vog-data`. No provisioning is needed to resume.
- Foundry setup is **already complete**. For future authorized changes only: project endpoint `https://vog-dev-y6po56.services.ai.azure.com/api/projects/vog-dev`; model deployment `gpt-5.6-terra`; MCP URL `https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/mcp`; connection prefix `vog-match-mcp`; guardrail `/subscriptions/9874f146-1f1f-471d-8556-b9481ee17cdd/resourceGroups/rg-vog-dev/providers/Microsoft.CognitiveServices/accounts/vog-dev-y6po56/raiPolicies/VogAgentGuardrail`. These correspond to `VOG_FOUNDRY_PROJECT_ENDPOINT`, `VOG_AGENT_MODEL_DEPLOYMENT`, `VOG_MCP_URL`, `VOG_MCP_CONNECTION_PREFIX`, `VOG_AGENT_GUARDRAIL_ID`. Do not print keys/tokens or recreate existing connections.
- Public routes for checks: `/healthz`, `/history`, `/insights?scope=recent|match|halftime|fulltime&lang=en-GB|es-ES|ta-IN`. `/mcp/{lang}` requires the existing API key; do not put it in source, logs or the handoff. `tools/Vog.FoundrySetup --ask` can verify deployed agent tool calls without exposing it.
- Last browser pages: live `b1b0248f-d00e-4471-a259-ad084adaddf2`, local `c4612495-9ba4-41c4-9b6e-d4c9865b7772`. IDs may not survive; reopen the URLs if necessary. Live listening stopped and injected routes/WebSocket instrumentation removed. Local task `Run gateway locally` at port 5093 remains running, with no listener and no archive storage configured.
- Git identity: `Daniel Melchizedek Arockia Thomas Samuel <danielmelchizedekarockiathomas@gmail.com>`. The requested historical email correction is already pushed. The documentation follow-up uses an ordinary push, not force push. No further branch or history rewrite is needed.

See [README.md](README.md#latest-recap-release-verification-26-october-2026) for release evidence and scope limitations. The following records are historical, not current rollout tasks or approval boundaries.

## Historical Checkpoint: Scoped Recaps (23 October 2026)

**Historical pause at the user's commit/push checkpoint.** At this checkpoint, implementation, full-solution tests and local Playwright checks were complete, but Azure build/deployment, Foundry registration and deployed Playwright verification had not started. These pending steps were completed on 26 October; see [the release verification](README.md#latest-recap-release-verification-26-october-2026). Base before this checkpoint was `22b11ae` on `main` (pre-email-rewrite hash). The remaining bullets and commands in this historical section describe that earlier checkpoint only.

- Historical charts and the public `/data` proxy are removed. No real historical data is used in the submitted app; the unused legacy DataPrep utility/private blobs and honest historical credits remain. Synthetic-only data is not full eligibility approval.
- Public `no-store` `/insights?scope=recent|match|halftime|fulltime&lang=en-GB|es-ES|ta-IN` uses the same `get_match_recap` MCP tool over a single immutable snapshot. Six read-only tools now exist; the Foundry YAML tool allowlist and recap grounding prompt await registration.
- Recaps expose deterministic raw key-event counts, source IDs, goal score effects and dismissal explanations. Scores are at scope end; do not infer window xG/possession, tactics, momentum or probabilities. Recent covers at most two match-clock minutes within the current half. Halftime is unavailable until that half ends and stays first-half-only; fulltime is unavailable until final whistle.
- The English panel discloses its language limitation. Casual selects up to five decisive/latest highlights; Analyst shows the last 40 plus a raw event-count table, with native shot meters and expandable event IDs. Refresh every 15 seconds pauses for a hidden tab, panel focus or open evidence. These are views, not new live channels: all nine channels and seed `20261026`, natural speed `1`, break `120` remain unchanged.
- Reported full-solution Release result: **128 passed**, including **23 companion** and **15 gateway** tests. Six new recap cases cover actual goal-event evidence/deduplication, halftime isolation, availability validation, second-half boundary, final-event inclusion in fulltime counts and an empty interval. No remaining-project subtotal is asserted.
- Remaining acceptance: native-language review, human screen-reader/co-design checks, actual microphone use, subjective audio/caption alignment, concurrent multilingual playback and a completed-match archive write/read round trip. Deployment checks must also verify removed chart routes, `no-store`, scope boundaries, evidence stability while focused/open, and the typed recap against the same deployed match.

Use [DEMO.md](DEMO.md) for a target 110-second actual-device demo and human validation checklist. No goals, accelerated clock or half-time arrival are promised. Keep the final recording strictly under 120 seconds.

### Local Browser Results

- Actual local gateway at `http://localhost:5093/`: `run_playwright_code` verified Casual (five observed moments, metrics hidden) versus Analyst (six observed moments, two team rows), saved view after reload, and keyboard Enter opening source evidence.
- At actual viewport widths 320/390/1280, document widths were 305/375/1265 with no horizontal overflow. API returned `Cache-Control: no-store`, invalid scope returned 400, removed `/data/goals-per-decade.json` returned 404, and full-time recap correctly said the period had not ended.
- Local-only HTTP 503 injection showed the retry message; removing the route and refreshing restored both team rows. All injected routes were removed. No local fixture was passed off as deployed verification.
- Screenshot output was compressed and is not full visual acceptance. Stability across an actual 15-second refresh interval while evidence is open remains to verify. No fresh recap answer through deployed Foundry or new-feature live audio test was performed.

### Historical Recap Resume Steps

1. Inspect branch/worktree before editing. The checkpoint includes application changes, six recap tests, agent YAML, project documentation and new `DEMO.md`; it excludes secrets, private blobs and generated output. Full Release solution passed **128 tests, zero failed or skipped** before this documentation checkpoint.
2. Check VS Code task **Run gateway locally** before starting another process. It was left running on **http://localhost:5093/** with no listener connected. Its `.vscode/tasks.json` is ignored, machine-local and not pushed. Local archiving is disabled because no storage endpoint was supplied. There is no `.env.local`; the task supplies the four nonsecret Azure endpoints below and prepends the CLI directory to PATH. Do not run provisioning just to recreate local configuration.
3. Preserve seed `20261026`, speed `1`, break `120` and all nine channels. Address remaining local acceptance issues first; rerun the tests below after code changes. Old historical-chart CSS is unused and not a release blocker.
4. Build a clean Git archive of the checkpoint/application commit with ACR using `src/Vog.Gateway/Dockerfile`. Do not upload the entire worktree: generated `TestResults` is not comprehensively excluded by `.dockerignore`. Registry **crvogdevy6po56**, resource group **rg-vog-dev**, app **ca-vog-dev-gateway**. Deploy the immutable image digest only; no full provisioning, secret rotation, blob deletion or unrelated infrastructure changes.
5. Once the new gateway serves six tools, register the updated YAML with `tools/Vog.FoundrySetup` for all three languages. Required configuration: `VOG_FOUNDRY_PROJECT_ENDPOINT`, `VOG_AGENT_MODEL_DEPLOYMENT`, `VOG_AGENT_GUARDRAIL_ID` (full ARM ID), `VOG_MCP_URL`, `VOG_MCP_CONNECTION_PREFIX`. Verify these nonsecret values from existing deployment outputs. Existing registrations still allow only the old tools; no registration was attempted this session.
6. Verify Azure latest-ready revision, health and traffic. `az containerapp wait` is unsupported here; use `az resource wait` with API version `2025-01-01` and a latest-ready-revision condition. Last actual Azure read this session still showed **ca-vog-dev-gateway--browserfix-1d5ab9a**. Public URL: `https://ca-vog-dev-gateway.delightfulground-76ebb125.swedencentral.azurecontainerapps.io/`.
7. Run deployed `run_playwright_code` checks without local asset overrides: both views, scopes/availability, event IDs, retry recovery, preferences, keyboard controls, 320/390/1280 reflow, English AD PCM/captions and a typed recap grounded in the deployed snapshot. Check `/healthz`, `/insights`, removed `/data` routes, and evidence stability during automatic refresh. Do not infer subjective audio or human accessibility acceptance.
8. Update README and this handoff with actual image digest, revision, registrations and test evidence. Any later commit/push should follow the user's authorization; this request is a checkpoint of work completed so far. A documentation-only follow-up needs no new image.

### Historical Recap Tooling

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test --solution VoiceOfTheGame.slnx --configuration Release
& 'C:\Program Files\dotnet\dotnet.exe' test --project tests/Vog.Companion.Tests --configuration Release
& 'C:\Program Files\Microsoft SDKs\Azure\CLI2\wbin\az.cmd' containerapp show --name ca-vog-dev-gateway --resource-group rg-vog-dev --query properties.latestReadyRevisionName -o tsv
git diff --check
```

Azure CLI is installed but not on the default PATH; AzureCliCredential also needs its directory on process PATH. Do not print tokens or Key Vault secrets. Known nonsecret endpoints: `https://kv-vog-dev-y6po56.vault.azure.net/`, `https://vog-dev-y6po56.cognitiveservices.azure.com/`, `https://vog-dev-y6po56.services.ai.azure.com/`, project `https://vog-dev-y6po56.services.ai.azure.com/api/projects/vog-dev`. A running gateway may lock Release outputs; stop that process or use a separate artifacts path before rebuilding.

Local shared browser page ID: `1a7b1847-b2bb-4496-8916-8196b96a563a`; rediscover next session if unavailable. `page.request.get` fails with unsupported `Storage.getCookies`: use same-origin `fetch` inside `page.evaluate`. Wait for actual `innerWidth` after `setViewportSize`. Old deployed tabs may cache unversioned JS; use fresh/cache-disabled navigation and remove any pass-through routes afterward. Screenshot tooling remains unreliable.

## Historical Deployment: Browser Fixes

Everything from this heading through the older release records is retained as dated history, superseded for current worktree status by the handoff above. Chart endpoint successes and old active-revision claims are not recap-release acceptance.

The user explicitly authorized commit, push and Azure deployment after the source-only review. Application commit **`1d5ab9a`** is pushed to `origin/main`; ACR build **`dtp`** succeeded from a clean Git archive. Image `crvogdevy6po56.azurecr.io/vog-gateway@sha256:2bae9f40390d1da12522b5e37fc2534322667d28af50dbb3d47bb6b691c5950a` is deployed as **`ca-vog-dev-gateway--browserfix-1d5ab9a`**. Azure confirmed Healthy, latest-ready, one running replica and 100% traffic, Single revision mode. Only the image was updated; infrastructure, secrets, configuration and Foundry registrations were preserved.

- Deployed Playwright checks passed at actual widths 320/390/1280 with settings and charts expanded: no horizontal overflow, 16 rows in each accessible chart table, nonblank pitch/charts, corrected footer, all three localized entry tags and retained English history language after channel changes.
- Live sample: 91 PCM chunks, six caption messages, five playback acknowledgements, no captured runtime errors. Typed companion answered Reds 0-0 Blues at 21 seconds, consistent with the clock. `/healthz` returned 200. The preceding gateway Release suite passed all 15 tests.
- Old tabs can retain cached unversioned JavaScript: hard-refresh after rollout. Testing used a cache-disabled network pass-through, not local asset overrides; routing was removed and listening stopped afterward.
- Full visual screenshot acceptance, microphone input, subjective audio quality, concurrent multilingual playback, completed-match archive write verification and accessibility certification remain open. Submission/data eligibility and natural short-demo scoring constraints are unchanged.
- This deployment supersedes the source-only status and older active-revision claims below. Git author name/email were configured locally for this repository from the prior commit at the user's request. Deployment documentation is a follow-up commit; no second image build is needed for Markdown-only changes.

## Historical Review: Local Browser Fixes

The user subsequently requested a codebase/documentation review and live Playwright testing with fixes. This review made **no commit, push or deployment**; earlier release authorization below is historical, not authorization for another rollout.

- Four project documents were corrected against source: README, this handoff, BUILD_PLAN and the accessibility ledger. Official hackathon rules and vendored documentation were preserved.
- Local browser fixes cover 320px overflow (including hidden chart tables), localized stadium-entry tags, per-row commentary language after channel switches, and the misleading synthetic-only footer. Changes are in `wwwroot/index.html`, `css/site.css` and `js/app.js`; server behavior, simulation timing and datasets are unchanged.
- Live backend testing confirmed English PCM/captions/playback acknowledgements, a typed score answer matching 77 minutes and 0-0, and successful sequential selection of all nine channels. Two archived Norton Borough 0-1 Brackworth Town records were readable; no completed-match archive write was observed.
- Playwright local asset overrides verified the fixes without deploying: no overflow at confirmed actual widths 320/390/1280, both tables with 16 rows, nonblank canvases, three localized entry tags, preserved old-row language, persisted preferences and keyboard zoom/follow. Screenshot tooling changed the effective viewport and produced inconsistent crops, so full screenshot acceptance remains open. No microphone, concurrent multilingual audio or complete accessibility audit was performed.
- Fresh gateway Release suite: **15 passed, zero failed or skipped**, SDK 10.0.400. Browser files and Markdown diagnostics plus `git diff --check` passed. No fresh full-solution or Bicep run was needed for these frontend/documentation changes.
- Review the uncommitted worktree before continuing. Obtain current approval before committing, pushing or deploying. Real historical-data eligibility, native-speaker review, accessibility co-design, queue hardening and short-demo natural scoring remain open.

## Historical Chart Release Handoff

**Purpose:** accessible, multilingual live commentary, captions, Calm mode and grounded companion Q&A over synthetic, football-realistic events. Do not turn this into a general historical football dashboard or change realism to manufacture a short demo.

- The user authorized the 23 October deployment and storage/chart completion, then explicitly requested commit, push and a fresh deployed Playwright test. Base `83229d0` contains feature commit `ab62f5c`, which added charts, archive and DataPrep on 22 October but no storage infrastructure. Storage/mobile fixes and documentation are in `0fc5655`; chart placement and the final deployed verification record are in `8ede73d`. The subsequent documentation review started with a clean worktree and `main` and local `origin/main` at `8ede73d`; no remote fetch was performed. No new branch is required.
- The user requested charts at the page end immediately before latency; `8ede73d` moves the unchanged panel below captions/companion. At the recorded release check, latest ready revision `ca-vog-dev-gateway--charts-bottom-20261023` received 100% traffic; immutable image/public URL are recorded in [README.md](README.md#historical-chart-azure-release-23-october-2026). The documentation review did not recheck Azure. Natural defaults remain seed 20261026, speed 1, break 120. Existing Foundry registrations, secrets and non-storage settings were preserved during that release.
- Added `infra/modules/storage.bicep` and wired `infra/main.bicep`. Account `stvogdevy6po56` has private `vog-data`; shared-key and anonymous blob access are disabled. Gateway/developer contributor roles are scoped to the container. `StorageConfiguration` handles normalized environment keys and literal local settings.
- Uploaded the credited Kaggle results dataset privately and ran DataPrep: 49,945 rows, zero skipped, 16 decades per aggregate. All four supplied CSVs hash-match the download; only results feeds the charts. Both chart endpoints return 200; desktop and final deployed mobile pixel/table checks pass without horizontal overflow.
- All 15 gateway tests passed, including three endpoint-configuration tests; Bicep compilation, touched-file diagnostics and whitespace validation passed. These are not a fresh full-solution or complete accessibility audit. Initial release verified English PCM/captions and a scoreboard-grounded typed companion answer; final frontend release reconnected live state/pitch.
- At the release check, the archive response was empty. Archiving is best-effort after a full synthetic match completes; missing storage or read failures also produce an empty history response. Do not infer storage health from an empty array, seed fake recent results or claim a verified archive write/read round trip.
- **Submission blocker:** the historical panel uses real international results, whereas the official rules require synthetic data. Licensing does not resolve eligibility. Resolve removal from the submission or replacement with honestly labeled simulator-derived data before claiming compliance. The live engine/companion remains synthetic and does not ingest Kaggle results.
- Three natural goals in 120 seconds remains unresolved. Keep original timing/realism boundaries and the rejected 60x history below. Complete co-design/screen-reader, concurrent multilingual playback, queue hardening and runtime security review separately.

### Historical Chart Resume Commands

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