# Voice of the Game: hackathon research and ideas

*Microsoft × Premier League "Inside the Game: Developer Hackathon" · researched 1–2 October 2026*

> **Chosen idea: #4, Voice of the Game.** Accessibility-first, multilingual AI match commentary.
> The detailed build plan is in [BUILD_PLAN.md](BUILD_PLAN.md).

> **Implementation update, 23 October 2026:** this is the historical research and idea-selection record, not a shipped-feature inventory. The deployed app remains a .NET 10 gateway/static JavaScript listener with self-hosted SignalR/JSON, deterministic Booth scheduling and Foundry prompt-agent companion Q&A through Voice Live and five read-only MCP tools. Natural speed and fictional live data remain the defaults. Private Blob Storage, match archival support and two accessible historical charts are now wired; endpoint and desktop/mobile checks pass. Blazor, hosted-agent workflows, Fabric and dedicated overlay/Studio remain planned. See [README.md](README.md) for the current release, tests, storage/data provenance and remaining gates, and [the accessibility ledger](please-reasearch-online-then-mighty-hanrahan.md) for partial delivery. Historical research, regulatory, availability and pricing claims below were not revalidated.

> **Purpose and data gate:** idea #4 is still accessible synthetic live-match commentary, not a general results dashboard. The supplementary charts use real CC0 international results, not synthetic matches. This conflicts with the entry's synthetic-only assumption and has unresolved eligibility under the official rules. Resolve removal from the entry or an honestly labeled simulator-derived replacement; do not treat a licence or the reference repository as approval. Three natural goals in 120 seconds remains unresolved; no new realism tradeoff has been approved.

---

## 1. The hackathon at a glance

| Item | Detail |
|---|---|
| Challenge | Build an AI solution on Azure that turns **synthetic** football events into explainable match intelligence, real-time narratives, recaps and personalized experiences for studio, broadcast and streaming |
| Registration | 29 Sep – 20 Oct 2026 (12:00 PM PT) |
| **Submission window** | **6 Oct – 27 Oct 2026, 11:59 PM PT.** Our target is **26 Oct**, keeping a day of buffer. |
| Judging / winners | 27 Oct – 10 Nov / announced by 20 Nov 2026 |
| Team | Up to 4 people. The project must be newly built after registration opens. |
| Prizes | Grand Prize 1st and 2nd · Best Foundry Use · Best Enterprise · Best Multi-Agent · Best Azure Native |
| Hero technologies | Microsoft Foundry · Microsoft Agent Framework · Azure MCP · GitHub Copilot Agent Mode · Microsoft Fabric · AKS / Container Apps / Functions / Logic Apps · Azure AI Services · GitHub SDK/CLI |
| Rubric (20% each) | 1. Technological implementation (including the quality of the synthetic data) · 2. Agentic design and innovation · 3. Real-world impact and production readiness · 4. User experience and presentation · 5. Adherence to the category |
| Submit | Project pitch (what we built, which Microsoft/Azure tech, the problem solved) · **demo video under 2 minutes** (judges needn't watch past 2:00) on YouTube, Vimeo, Facebook Video or Youku · **public** GitHub repo |
| Data rule | Synthetic, football-realistic data only. No real Premier League match data is provided, so we must generate our own. |
| Trademarks | No third-party trademarks, copyrighted music or other material without permission, so we use a **fictional league, clubs and players**, and procedural audio |
| Learning sessions | Reactor livestreams on 7, 14 and 21 Oct (AKS/Container Apps soccer insights; Azure databases + vector search) |

### The required pipeline
Every project needs **one pipeline with five stages**:

1. **Ingest** synthetic match events as they happen: passes, shots, tackles, possession changes, pressure events, metadata.
2. **Interpret** them into statistics, patterns and context.
3. **Explain** why a moment matters, not just that it happened.
4. **Render** the insight so it can sit on screen alongside the match: timed and machine-readable, for broadcast overlays.
5. **Personalize** it, so an analyst and a casual fan get genuinely different experiences: modes, favourite clubs and players, player-focus mode, language, a single metric.

**Suggested features:**
- Player identification: real-time name tags, plus speed and distance tags when set thresholds are crossed.
- Pass quality: distance, accuracy and a difficulty rating.
- Ball speed and shot speed.
- Auto-eventing from a video feed.
- Narratives, milestones and recaps.
- Explainability (control vs chaos, pressure shifts, game rhythm).
- New synthetic datasets ("data innovation").
- Multi-language storytelling.

---

## 2. What the research tells us

### 2.1 What the Premier League and Microsoft already have
- Microsoft and the Premier League signed a **five-year partnership in July 2025**. It produced the **Premier League Companion**, built on Copilot and Azure OpenAI, which answers questions over 30+ seasons of stats, 300,000 articles and 9,000 videos.
- **Implication:** don't build "chat with stats." The partnership's *announced next step* is **real-time broadcast data overlays and post-match insight**, and that's the gap to aim at.

### 2.2 Prior art to beat
| Who | What | Lesson for us |
|---|---|---|
| Bundesliga Match Facts (AWS) | xGoals, Pressure Handling (escape rate), Win Probability, shown as live broadcast graphics | Explainable metrics on air are proven; ours must explain *why* |
| NBC "AI Al Michaels" (Paris 2024) | Personalized daily recaps in a synthetic voice, ~7M possible variants | Personalization at scale is achievable |
| IBM at Wimbledon | Generative AI commentary on highlight packages | AI narration is accepted in premium sport |
| WSC Sports / NBA | Multilingual AI voice-over for highlights | Multilingual audio is a real product line |
| ESPN | AI-written recaps reviewed by human editors; a publicized AI headline error | **Grounded, fact-checked output is a differentiator** |

### 2.3 The production path for overlays
- Premier League Productions' graphics are delivered by **AE Live on Vizrt**, which supports HTML5 and data-driven graphics.
- Our overlays should therefore be **timed, machine-readable cue JSON** feeding a transparent HTML5 page. That page can be loaded by Vizrt-style engines, CasparCG, vMix or OBS.

### 2.4 Synthetic data and auto-eventing
- **Google Research Football** (Apache-2.0) is a physics-based 3D simulator. It has been used to simulate thousands of matches with tracking-data output ([arXiv 2503.19809](https://arxiv.org/abs/2503.19809)).
- **socceraction** implements the SPADL action format plus the xT and VAEP valuation models.
- **SoccerNet / Roboflow**-style YOLO + ByteTrack pipelines can detect players and the ball. Running them on *our own rendered synthetic video* avoids footage-rights problems entirely.
- **Decision for this project:** a custom, seeded C# simulator, because the team stack is .NET. Its realism is documented in a "synthetic data card."

### 2.5 State of the Microsoft platform (October 2026)
- **Microsoft Agent Framework:**
  - **1.0 GA since April 2026**, with .NET packages at 1.23.
  - Workflow patterns: sequential, concurrent, handoff, group chat, Magentic (experimental in C#).
  - Also: checkpointing, human-in-the-loop, MCP + A2A, OpenTelemetry.
  - **Declarative YAML workflows** have been stable since July 2026.
- **Foundry Hosted Agents** are GA and run Agent Framework code, including workflows, inside Foundry. The portal shows a playground, traces, versions and sessions.
- **The Foundry portal's visual Workflows designer** is a **preview** and **retires on 1 December 2026**. Microsoft directs new work to Agent Framework, deployed as hosted agents.
- **Microsoft Fabric Real-Time Intelligence:** Eventstream → Eventhouse (KQL) → Real-Time Dashboard + Activator.
  - Official sample **microsoft/fabric-racing-sim** streams live sports-game telemetry from a .NET client into Fabric.
  - **Too slow for a sub-second loop:** Activator can take up to minutes.
- **Azure Speech and Voice Live:**
  - Neural, HD and Omni voices.
  - Voice Live (speech-to-speech for agents, interruption handling, noise suppression), GA in .NET.
  - **No built-in "sports commentary" voice style exists.**

### 2.6 The accessibility landscape (why idea #4 matters)
| Finding | Source |
|---|---|
| At least **2.2 billion** people live with a vision impairment | WHO World report on vision |
| **European Accessibility Act** in force since **28 June 2025**: streaming and audiovisual services must offer synchronized, user-controllable subtitles, audio description and screen-reader-compatible players | Taylor Wessing analysis; EN 301 549 §7 |
| **FIFA World Cup 2026** offered audio-descriptive commentary on **all 104 matches**, but only in English, Spanish and French, plus haptic boards at some stadiums | FIFA |
| **Microsoft Seeing AI + the OneCourt** haptic tablet helped blind fans at the Seattle World Cup match (June 2026) | News report |
| **Only 4 of 20** Premier League clubs had trained in-house audio describers; 3 had a **30-second delay** (2015 survey, since improved) | Level Playing Field via Disability News Service |
| CAFE has trained ~100 audio-description commentators in 30 countries: **human describers are scarce** | Zero Project / CAFE |
| Good football audio description is **short, clipped bursts**: ball location and direction of play, pass shape, shot speed and angle, regular score and time reminders, reading on-screen graphics aloud | Red Bee Media (AFCON live TV audio description) |
| **Action Audio** (Australian Open) made the ball audible in 3D sound for blind fans; Touch2See and Field of Vision make tactile tablets for football | Monash / Tennis Australia; EBU newsletter; CNN |

### 2.7 The design implication everyone must get right: latency
Calling an LLM on every event is too slow and too expensive for live play. Every strong design needs **three speeds**:
- **HOT** (under 1 s): deterministic stats and templates, no LLM.
- **WARM** (2–8 s): agent explanations.
- **COLD** (30–120 s): recaps and voice-overs.

---

## 3. Idea catalogue

Each idea lists its pitch, how it uses agents, the demo "wow" moment, the best-fit prize and the effort.

### Idea 1: "The Gallery", a virtual broadcast crew of agents
- **Pitch:** a multi-agent team modelled on a TV production gallery. It includes a Moment Detector, Tactical Analyst, Player Analyst, Storyline Editor, Fact-Checker, a **Director** (decides *what* goes on screen *when*, within a screen budget with cooldowns), a Graphics Operator (emits cue JSON) and a Localizer.
- **Wow moment:** a producer console shows the agents' trace as they compete for air time, and then the overlay appears.
- **Prize fit:** Best Multi-Agent, Grand Prize. **Effort:** medium-high.

### Idea 2: "MyMatch Lens", the same match with a genuinely different broadcast per fan
- **Pitch:** the overlay layer is personalized per viewer.
- **Modes:** Analyst (xT, PPDA, pass difficulty), Casual, Kids, New-to-football (explains rules such as offside), Fantasy manager, single-metric ("only show pressing"), club-partisan tone, spoiler-safe delayed viewing for other time zones, and language.
- **Wow moment:** the same goal, split-screen, rendered for three fans in three languages.
- **Prize fit:** Grand Prize, UX. **Effort:** medium.

### Idea 3: "Why It Mattered", the explainability engine
- **Pitch:** every key moment gets a reason:
  - the **win-probability delta** and xT added
  - a **decision-quality counterfactual** ("the open runner had 0.31 xG vs 0.08 for the shot taken")
  - a **Control-vs-Chaos index** (turnover rate, possession-spell length, zone-transition entropy)
  - **pressure-shift detection** (rolling PPDA and field tilt plus change-point detection)
  - game-rhythm waves
- **Prize fit:** Best Foundry Use, Grand Prize. **Effort:** medium.

### Idea 4: "Voice of the Game", accessibility-first multilingual AI commentary ✅ CHOSEN
- **Pitch:** a live AI commentator that serves fans who are usually left out.
  - **AI audio-descriptive commentary** for blind and low-vision fans, following real describer practice. Every on-screen graphic is read aloud.
  - **Multilingual transcreation:** culturally adapted football idiom rather than literal translation, with persona voices.
  - **Hands-free voice Q&A** mid-match ("where's the ball?", "why did that matter?").
  - **Earcons and stereo ball position.**
  - **Live captions** with sound labels for Deaf and hard-of-hearing fans.
  - A **Calm mode** for neurodivergent fans; a low-vision magnifier.
  - A sighted overlay whose every cue carries alt text.
- **Wow moment:** the video opens on a black screen ("headphones on") and the match is told entirely through sound. A blind fan's experience becomes the judges' experience.
- **Prize fit:** Grand Prize, **Best Enterprise** (EAA compliance evidence), Best Multi-Agent, Best Foundry Use. **Effort:** medium.

### Idea 5: "Studio Copilot", a producer and pundit assistant (B2B)
- **Pitch:**
  - At half-time it builds the rundown: three talking points, graphics and clip timestamps.
  - Pundits ask in natural language ("every turnover in their own third") and get a graphic.
  - Human approval (Agent Framework) before anything goes on air, plus a compliance and brand-safety agent.
  - Logic Apps notifies the production crew in Teams.
- **Prize fit:** Best Enterprise. **Effort:** medium.

### Idea 6: "Recap Factory", one story in every format and every language
- **Pitch:** the canonical match story becomes:
  - a 30-second vertical social script
  - a 3-minute studio recap with voice-over
  - an article
  - a push notification
  - a kids' version
  - an analyst brief

  It is personalized by name and club, NBC-style, and scored for groundedness with Foundry evaluations.
- **Prize fit:** Best Foundry Use. **Effort:** low-medium; strongest as a module of another idea.

### Idea 7: "Synthetic Stadium", data innovation plus auto-eventing
- **Pitch:**
  - An LLM "script director" steers a simulator toward storylines (e.g. a red card, then an underdog comeback).
  - The simulator outputs events, tracking data and rendered video.
  - A YOLO + ByteTrack model detects passes and ball/shot speed **from the synthetic video**, scored against ground truth.
  - The dataset spec is published openly.
- **Prize fit:** Best Azure Native (GPU on AKS / Azure ML), technical implementation. **Effort:** high, because the computer-vision work is risky.

### Idea 8: "Player Cam", follow one player all match
- **Pitch:** live speed, distance and sprint tags at thresholds; a mini-map and heatmap; form vs the player's synthetic season baseline; milestone alerts ("fastest sprint this season, 34.8 km/h"); a shareable full-time player story card.
- **Prize fit:** UX. **Effort:** low-medium; best as a mode inside idea 2 or 4.

### Wildcards
- **Ask-the-Match MCP:** expose live match state as an MCP server or A2A agent, so Copilot or Teams can ask "what's happening?"
- **Match Rhythm sonification:** turn the game's tempo into music or visuals.
- **Rivalry mode:** two partisan AI commentators debating.

### Comparison
| # | Idea | Covers all 5 stages | Agentic depth | Real-world impact | Demo strength | Effort |
|---|---|---|---|---|---|---|
| 1 | The Gallery | ✔ | ★★★ | ★★ | ★★ | M-H |
| 2 | MyMatch Lens | ✔ | ★★ | ★★ | ★★★ | M |
| 3 | Why It Mattered | ✔ | ★★ | ★★ | ★★ | M |
| **4** | **Voice of the Game** | ✔ | ★★★ | **★★★** | **★★★** | M |
| 5 | Studio Copilot | ✔ | ★★ | ★★ | ★★ | M |
| 6 | Recap Factory | partly (render is weak) | ★★ | ★★ | ★★ | L-M |
| 7 | Synthetic Stadium | partly (personalization is weak) | ★ | ★ | ★★ | H |
| 8 | Player Cam | ✔ | ★ | ★ | ★★ | L-M |

---

## 4. Cross-cutting design patterns (they apply to any idea)

1. **Three-speed pipeline:** HOT deterministic stats and templates (under 1 s), WARM agent explanations (2–8 s), COLD recaps.
2. **One canonical cue contract:** a timed, machine-readable `InsightCue` with a template, priority, time-to-live, anchor, a payload per persona and language, evidence event IDs, confidence and **alt text**.
3. **Grounded explainability:** every claim cites event IDs. A **code-based Fact-Checker** verifies claims against the match state, and Foundry evaluations score groundedness.
4. **Compute once, personalize at render time:** generate an insight once, then fan out to persona × language presenters.
5. **A fictional synthetic league** with season history (so milestones work), deterministic seeds and replay at 1×/5×/20×.
6. **Production readiness:** infrastructure as code, CI/CD with OIDC, OpenTelemetry tracing, content safety, managed identity, Key Vault, and **no secrets in the repo**.
7. **Avoid:** real club or player names and logos, betting features, music in the video, and claims of using real Premier League data.

---

## 5. Decision: why idea #4

- **Real impact:** serving blind, low-vision, Deaf, hard-of-hearing and neurodivergent fans addresses a documented, regulated gap (EAA), and human describers can't scale to every match and language.
- **It still meets every hackathon requirement:**
  - all five pipeline stages, with a sighted overlay as well as audio
  - the suggested features (speed and distance tags, pass difficulty, shot speed)
  - explanations, recaps and multilingual output
- **It has the strongest multi-agent story.** Audio is serial, so agents must *bid* for airtime, and a deterministic **Booth Producer** decides. That is a non-trivial orchestration problem judges can see.
- **The demo is memorable:** a black screen and a match told through sound.
- **Ideas absorbed into #4:**
  - #1's Director becomes the Booth Producer.
  - #2's personas become listener modes.
  - #3's explanations come from the Insight Desk.
  - #6's recaps come from the Recap workflow.
  - #8's player focus becomes a mode.

### Decisions log
| Date | Decision |
|---|---|
| 1 Oct 2026 | Idea #4 chosen; stack **.NET 10 + Blazor** |
| 1 Oct 2026 | Agent workflows run **in Foundry as Hosted Agents** (Agent Framework, code-first + declarative YAML). The retiring portal designer is not used. |
| 1 Oct 2026 | Reuse patterns from the team's previous entry [certflow-ai](https://github.com/daniel-melchizedek/certflow-ai), and fix its gaps (no workflow, no tests, prompt-parsed JSON, imperative deploy) |
| 2 Oct 2026 | One **medium-cost** model for every LLM agent: **gpt-5.6-terra**, a little above gpt-4o in price |
| 2 Oct 2026 | `.gitignore` first; **no secrets or personal data in the repo** |
| 2 Oct 2026 | **Microsoft Fabric is used** as the telemetry, operations and grounding layer (F1–F4 on the free trial), never on the live audio path |
| 21 Oct 2026 | Superseding implementation status: static JavaScript instead of Blazor; PowerShell + Azure CLI + Bicep instead of azd/Aspire; prompt agents rather than hosted workflows. Fabric/Insight Desk/recap/transcreation remain planned. Azure has one healthy gateway replica and `gpt-5.6-terra` version `2026-07-09`; see README for the dated evidence. Earlier decisions above record intent, not delivery. |

### Claims to verify before they go in the pitch
- **WHO hearing-loss figure** (">1.5 billion"): confirm from a WHO primary source.
- **"Game changer" quote** from Microsoft's accessibility leadership about OneCourt: find the primary source and the exact speaker.
- **RNIB finding** that blind TV viewers miss on-screen graphics: confirm from the RNIB report itself.
- **Model and capacity prices** (gpt-5.6-terra, Voice Live, Fabric F2): confirm in the Azure pricing calculator on deployment day. The official page showed placeholders when checked.

---

## 6. Sources

**Hackathon**
- [Official repo and rules](https://github.com/microsoft/insidethegamehackathon) · [Reactor series](https://developer.microsoft.com/en-us/reactor/series/S-1709/)

**Premier League × Microsoft**
- [Partnership announcement](https://news.microsoft.com/source/2025/07/01/premier-league-and-microsoft-announce-five-year-strategic-partnership-to-personalize-the-fan-experience-with-ai-for-1-8-billion-people/) · [TechRadar on the PL Companion](https://www.techradar.com/computing/artificial-intelligence/the-premier-league-companion-is-a-new-ai-powered-tool-for-soccer-fans-but-its-useless-at-least-for-now)

**Prior art**
- Bundesliga: [Pressure Handling and Win Probability](https://bundesliga.com/en/bundesliga/news/pressure-handling-win-probability-join-bundesliga-match-facts-powered-by-aws-21390) · [xGoals explained](https://www.bundesliga.com/en/bundesliga/news/expected-goals-xg-and-goal-probability-explained-13847)
- AI commentary: [NBC AI Al Michaels (CNN)](https://edition.cnn.com/2024/06/26/tech/al-michaels-ai-olympics) · [IBM Wimbledon AI commentary](https://uk.newsroom.ibm.com/2023-06-21-IBM-Brings-Generative-AI-Commentary-and-AI-Draw-Analysis-to-the-Wimbledon-Digital-Experience) · [WSC Sports multilingual (NBA)](https://wsc-sports.com/blog/customer-spotlight/nba-launches-genai-technology-with-wsc-sports-to-automate-multilingual-content/) · [ESPN AI recaps](https://getcoai.com/news/espn-is-now-using-ai-to-recap-womens-soccer/)
- Broadcast graphics: [AE Live and Vizrt](https://www.vizrt.com/news-articles/ae-live-and-vizrt)

**Synthetic data and computer vision**
- [Simulating tracking data with Google Research Football (arXiv 2503.19809)](https://arxiv.org/abs/2503.19809) · [socceraction (SPADL, xT, VAEP)](https://socceraction.readthedocs.io/en/stable/documentation/intro.html) · [SoccerNet-Tracking](https://arxiv.org/pdf/2204.06918)

**Microsoft platform**
- Agent Framework and Foundry:
  - [Agent Framework 1.0](https://devblogs.microsoft.com/agent-framework/microsoft-agent-framework-version-1-0/) · [Agent Framework at BUILD 2026](https://devblogs.microsoft.com/agent-framework/microsoft-agent-framework-at-build-2026-announce/)
  - [Foundry workflows (preview, retiring 1 Dec 2026)](https://learn.microsoft.com/en-us/azure/foundry/agents/concepts/workflow) · [Declarative workflows (C#)](https://learn.microsoft.com/en-us/agent-framework/workflows/declarative?pivots=programming-language-csharp)
  - [Foundry hosted agents](https://learn.microsoft.com/en-us/agent-framework/hosting/foundry-hosted-agent)
- Fabric: [Fabric Real-Time Intelligence](https://www.microsoft.com/en/microsoft-fabric/features/real-time-intelligence) · [Activator latency](https://learn.microsoft.com/en-us/fabric/real-time-intelligence/data-activator/activator-latency) · [microsoft/fabric-racing-sim](https://github.com/microsoft/fabric-racing-sim)
- Speech: [Azure Speech overview](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/overview) · [Voice Live](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/voice-live)

**Accessibility**
- Prevalence and regulation: [WHO World report on vision](https://www.who.int/publications/i/item/9789241516570) · [EAA and audiovisual services (Taylor Wessing)](https://www.taylorwessing.com/en/interface/2025/media-hot-topics-2025/access-to-audiovisual-media-services-under-the-european-accessibility-act) · [EN 301 549 §7](https://accessible.canada.ca/node/382)
- FIFA and assistive tech: [FIFA World Cup 2026 ADC and haptics](https://inside.fifa.com/organisation/news/audio-desctiptive-commentary-haptic-boards-jillian-sloane-canada) · [Seeing AI + OneCourt at the World Cup](https://thenationaldesk.com/top-videos/microsofts-seeing-ai-and-onecourt-help-blind-fans-experience-world-cup-action-soccer-match-seattle-social-media-technology-artificial-intelligence-disability-phone-camera-audio-stadium-seat-concession-data-score) · [Action Audio (Monash)](https://www.monash.edu/news/articles/monash-university-and-tennis-australia-serve-up-world-first-accessible-audio-stream-for-fans-with-blindness-or-low-vision) · [Touch2See (EBU)](https://www.euroblind.org/newsletter/2024/january/en/touch2see-revolutionising-sports-tracking-blind-and-partially-sighted) · [Field of Vision (CNN)](https://www.cnn.com/world/europe/field-of-vision-blind-sport-stadium-spc)
- Describer practice: [Level Playing Field 2015 survey (DNS)](https://www.disabilitynewsservice.com/premier-league-should-be-ashamed-of-treatment-of-blind-fans/) · [CAFE audio-descriptive commentary (Zero Project)](https://zeroproject.org/view/project/caba43b6-ad4a-ec11-8c62-000d3ab5a6d0) · [Red Bee: audio describing live football at AFCON](https://redbeemedia.com/news-events/audio-describing-live-football-behind-the-mic-for-afcon) · [Audio Describing Football (JoSTrans)](https://www.jostrans.org/article/view/5620) · [RNIB accessible stadiums](https://www.rnib.org.uk/accessible-stadiums)
