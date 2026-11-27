---
layer: release
status: living
---
# TownWalker — Release charter

> The one finish line this repo is working toward. Every open ticket in this repo sits under one of the epics
> below, and every epic is part of the milestone. Work that does not fit is out of scope until this milestone is
> reached (owner, 2026-11-27, MAT-T319).

## 1. What this is
A Unity 6 traversal lab about towns that walk the surface on giant legs: six demos (Route Map, Horizon Strip,
Isometric Expanse, Spider Town, 3D Low-Poly Walker, Radar Sectors) play the same scan → choose → walk/send →
battle placeholder → haul loop so traversal styles can be compared on feel, time-to-battle and taps.

## 2. The milestone: TownWalker traversal lab verdict {#milestone}
- **Milestone:** MS-78, "TownWalker traversal lab verdict"
- **Published means:** internal Windows players on `R:\MindAttic\Apps\TownWalker\{Debug,Release}` (run.ps1);
  not shipped to a store.
- **The natural stopping point:** docs/PLAN.md defines the lab's deliverable: every demo playable on the shared
  loop, then a side-by-side comparison with a recommendation, and the owner picks a direction. When the six
  demos pass labcheck, the players are on R:, and the comparison (now including Spider Town) is written and
  signed off, the lab has done its job; building the chosen direction is a new milestone.

## 3. Done when (acceptance checks)
1. build — the Windows player builds (`run.ps1 -Redeploy`).
2. exists — `R:\MindAttic\Apps\TownWalker\Release\TownWalker.exe`.
3. harness — `python tools/labcheck.py` passes for all six demos.
4. signoff — the owner reads docs/COMPARISON.md and records the chosen traversal direction.

## 4. Epics, in priority order
| Priority | Epic (ticket) | What it delivers | Features |
|---|---|---|---|
| 900 | GGC-T42 Six-demo lab | All six demos pass labcheck; the loop model is correct | GGC-T44 Loop health (990: GGC-T48), GGC-T45 Core loop correctness (980: GGC-T4) |
| 800 | GGC-T43 Direction and delivery | Players on R: and the written comparison for the owner's pick | GGC-T46 Windows players on R: (890: GGC-T41), GGC-T47 Traversal comparison (880: GGC-T49) |

## 5. Out of scope (not before this milestone)
- Real battles, fuel and stranding rules, economy (PLAN.md: developed only after a direction is picked).
- Art passes, CC0 asset imports, Android/WebGL release builds.
- Porting anything to GridGameGaiden.
- Audit-generated unit-test sweeps, generic cleanups and open-ended bughunts.

## 6. Current verified state
- Six demos on the shared `Lab.Core` loop; labcheck drives all six (tools/labcheck.py).
- 2026-10-10 timed comparison of five demos recommended Isometric + Route Map (commit 237ee53, quicksave);
  Spider Town (ffa6881) and spring-leg cow (14abbef) came after it.
- Shared run menu adopted (675cc2a, d081550); the R: players are not deployed yet.
- Core tests: AStar/RouteGraph (49e1a64), loop tests (`Tests/Core.Tests`).

## 7. Decision record
- 2026-11-27 — charter drafted from README/AGENTS/docs/PLAN.md and the open tickets; Legion vote: Approve, 9/9 voters (plurality quorum reached, consensus 1.0); no dissent (all voters routed through the xai provider).
- Triage: 9 open → done 0, duplicate 0, wont-do 7 (GGC-T5, T6, T7 test nice-to-haves; T11, T12, T13 sweeps; T38 bughunt), kept 2 (GGC-T4, GGC-T41), new tasks 2 (GGC-T48, GGC-T49).
