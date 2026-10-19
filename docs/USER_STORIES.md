---
codex: 1
project: GridGameCaravan
code: GGC
layer: stories
status: living
updated: 2026-10-12
---
<!-- GENERATED from MindAttic.Workflow/data/workflow.db by `workflow db render`. Do not edit: this file is rewritten from the database; change stories and tickets with the workflow CLI. -->

# GridGameCaravan — User Stories
> ✅ done (shipped & tested) · 🟡 partial · ⬜ planned. Every ✅ cites the test.

## Priority backlog
_Nothing recorded yet._

## Tickets
> Permanent log of every change and fix — this repo's ticket system. Rows are append-only: never
> delete or reword one; only move its Status (open → closed / wont-do) and fill in Ref. Status is
> a word, not a glyph, so tickets don't count toward the story index. Keep this section last.

| ID | Date | Type | Status | Summary | Ref |
|---|---|---|---|---|---|
| GGC-T1 | 2026-10-12 | docs | closed | Start the permanent ticket log in USER_STORIES.md | — |
| GGC-T2 | 2026-10-14 | chore | closed | Move Tests/Core.Tests from xUnit to NUnit (swap xunit packages for NUnit + NUnit3TestAdapter, port the 6 LoopTests facts as-is) so the Lab core tests follow the workspace's NUnit standard and the other GGC test tickets land in one framework | 749002d |
| GGC-T3 | 2026-10-14 | chore | closed | NUnit tests for AStar and RouteGraph (Assets/Lab/Core/Routing.cs): AStar finds the cheapest path not just the first, returns [start] when start==goal, null when unreachable or when maxNodes is exhausted, skips infinite-cost edges; RouteGraph.Build keeps edges symmetric, caps each node at 'links' nearest, always links the nearest neighbour even beyond maxLen (so no isolated site), and node 0 is the start | 49e1a64 |
| GGC-T4 | 2026-10-14 | chore | open | NUnit tests for LabSession edge cases (Assets/Lab/Core/Town.cs): Scan with fuel < ScanCost returns 0 and spends nothing; WalkTo while stranded is a no-op; Tick returns true on the arrival frame and doesn't overshoot; Send returns null for null/cleared/out-of-bike-range sites and computes RoadFights=min(2,dist/11) and RideSeconds; EndBattle(false) gives no loot and still resets the phase/metric baselines; won fuel is capped at FuelMax; AverageTimeToBattle/AverageTapsToBattle are 0 with no battles | — |
| GGC-T5 | 2026-10-14 | chore | open | NUnit tests for world generation (Assets/Lab/Core/World.cs WorldGen/World/Rng/Site): no two sites closer than minGap and none within 16 of Start, Danger always 1..5, each SiteKind gets its documented loot formula, Ids are 1..n, generation stops at the 5000 guard on a too-small map; Rng is deterministic and Range stays in [a,b); HeightAt stays in 0..1 and BiomeAt in 0..3; Site.Waves>=1 and HasBoss for Danger>=3 or Pass; V2.Normalized of zero is zero | — |
| GGC-T6 | 2026-10-14 | chore | open | NUnit tests for Hex (Assets/Lab/Core/Routing.cs): Distance is symmetric and equals 1 for all six Neighbour(i) directions, FromPoint(Center(size)) round-trips across negative/positive q,r and lands in the right hex for points near cell edges (cube rounding fix-up), Key/FromKey round-trips across the -512..511 range, and Equals/GetHashCode agree | — |
| GGC-T7 | 2026-10-14 | chore | open | Unity Test Framework (NUnit) EditMode tests for Lab.App pure math (Assets/Lab/App/Tween.cs, Walker/WalkerRig2D.cs): EaseOut/EaseInOut/Back hit 0 at t=0 and 1 at t=1 and EaseInOut is continuous at 0.5; WalkerRig2D's Ik keeps both bone lengths and falls back gracefully when the foot is out of reach; FootSnapshot/Step keep at least the planted feet grounded through a gait cycle - needs a Lab.App.Tests EditMode asmdef (none exists today) | — |
| GGC-T8 | 2026-10-15 | chore | closed | Remove duplicate command copies (/quickload, /quicksave) now centralized at the user level (~/.claude/commands) | M.FLOW-T14 |
| GGC-T9 | 2026-10-16 | chore | closed | install.ps1 adds this repo's 15 .prose/commands stubs for the shared global commands, so any LLM client finds them | mindattic-agent-standard/install.ps1 run 2026-10-16 |
| GGC-T10 | 2026-10-16 | chore | open | Bug hunt: run an exhaustive /bughunt over the whole repo and fix every real bug found (each fix under its own fix ticket), then hunt again; close this only when a full pass finds nothing | user request 2026-10-16: approved repo sweep |
| GGC-T11 | 2026-10-16 | chore | open | Run the full unit test suite and fix every failure (each under its own fix ticket) until it passes; put the passing run in --ref. No test project: close this wont-do saying so | user request 2026-10-16: approved repo sweep |
| GGC-T12 | 2026-10-16 | chore | open | Clean up code and docs: remove dead code, stale comments and outdated or duplicated docs, and simplify where behaviour doesn't change (/simplify); tests stay green | user request 2026-10-16: approved repo sweep |
| GGC-T13 | 2026-10-16 | docs | open | Bring README.md up to date with the code as it is now (what it is, setup, usage, commands, current state), following codex-standard/README-STYLE.md; README.htm regenerated if the repo has one | user request 2026-10-16: approved repo sweep |
| GGC-T14 | 2026-10-16 | docs | closed | Update USER_STORIES.md | — |
| GGC-T15 | 2026-10-17 | feature | closed | Walker rig: per-leg terrain contact, flat stomping hooves, slow turn-in-place, and a chamfered hull base, so the isometric town reads as a heavy quadruped instead of developer art | manual play test, Isometric Expanse |
| GGC-T16 | 2026-10-18 | chore | closed | Add deploy.bat and project-owned /deploy, /run commands wrapping tools/deploy.ps1 so they run without a workflow ticket or pace-gate hold | — |
| GGC-T17 | 2026-10-19 | chore | closed | Back up the Walking Town Traversal Claude artifact into artifacts/ per HOUSE-LAW-12 | GridGameCaravan (7f8948b) |
| GGC-T18 | 2026-10-19 | feature | closed | Spider Town demo: the iso expanse walked by a radially symmetric spider town on 3-6 insect (/\) legs, rigid-body + spring-leg physics, a free gait with a static-stability check (tripod lurch, crawl, wave, alternating tripod), feet planting on every terrain step | — |
| GGC-T19 | 2026-10-19 | feature | closed | The cow walker's hull rides on simulated spring legs (rigid-body lift/roll/pitch) instead of a scripted bob/tilt, and in the iso expanse it turns in place to face its destination before it sets off | — |
| GGC-T20 | 2026-10-19 | chore | closed | Rename GridGameCaravan to TownWalker (owner 2026-10-19): it is now a standalone project, not GridGameGaiden's traversal lab. Product name, exe, deploy folder and shortcut, tools, docs and comments renamed; GitHub repo renamed; workflow repo key renamed; the owner renames the Windows folder. | — |
