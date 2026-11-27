---
layer: bible
status: living
---
# TownWalker — Project Bible

> Single source of truth for what TownWalker IS, is NOT, and the rules that keep it coherent.
> README says how to build and run it; this says how to think about it. The finish line lives in
> `docs/RELEASE.md`. The original lab plan is `docs/PLAN.md`.

## 1. The one sentence {#GGC-§1}
A Unity 6 (6000.4.3f1) traversal lab where towns walk the surface on giant legs, with six demos that play the
same scan → choose → walk/send → battle placeholder → haul loop so traversal styles can be compared.

## 2. The product promise {#GGC-§2}
- Premise: mankind lives on walking towns because the surface is overrun; the party is a ground team that
  disembarks to fight and bring back fuel; the town is the hub.
- Battle-first: traversal is a 15–40 s beat between expeditions; each demo logs time-to-battle and taps.
- One hand: tap, hold or drag only.
- Legs, not wheels: the town always walks with a visible gait; the bike is only the team's short hop.

## 3. What it is NOT {#GGC-§3}
- Not part of GridGameGaiden: nothing here is destined for Gaiden, which has no world map.
- Not a finished game: battles are a placeholder card; no fuel or stranding rules are decided.
- Not built on the mood board's art: it is a style reference only; everything is drawn or modelled from scratch.

## 4. Architecture canon {#GGC-§4}
```text
 Assets/Lab/Core (engine-free: world, town, routing, hex)  ◄── Tests/Core.Tests (dotnet test)
        ▲
 Assets/Lab/App/LabApp.cs boots each demo in code
   Demos: RouteMap, Horizon, Iso, Spider, LowPoly3D, Hex     Walker/WalkerRig2D (Painter2D rig)
        ▲
 tools/labcheck.py (automation server: state, tap, drag, screenshot) → artifacts/labcheck
```
- **Nouns:** WalkerTown, Site (depot, mine, wreck, nest, pass), Route/Hex, LabSession.
- **Verbs:** Scan, WalkTo, Send (bike), EndBattle, haul.

## 5. The Laws {#GGC-§5}
Inherits `MindAttic.HouseRules.md` by reference.
- {#GGC-LAW-1} **Shared core, comparable demos.** Every demo plays the same `Lab.Core` loop.
- {#GGC-LAW-2} **Everything is code-built.** `LabApp.cs` boots each demo; no prefab or Inspector wiring.
- {#GGC-LAW-3} **Done is verified by play:** `dotnet test Tests/Core.Tests`, then `tools/labcheck.py`, and
  look at the screenshots.
- {#GGC-LAW-4} Free assets only, recorded in CREDITS.md.

## 6. Verified state {#GGC-§6}
- 2026-10-10: five demos timed; Isometric + Route Map recommended (237ee53). Spider Town (ffa6881) and the
  spring-leg cow (14abbef) followed.
- labcheck covers all six demos; shared run menu adopted (675cc2a); R: players not yet deployed.

## 7. Active frontier {#GGC-§7}
See `docs/RELEASE.md`: the traversal lab verdict.

## 8. Quality bar {#GGC-§8}
Core tests and labcheck pass for the touched demos, screenshots reviewed, committed once to main with its
ticket ID.

## 9. Glossary {#GGC-§9}
- **Time-to-battle / taps per decision:** the two scoreboard metrics every demo reports.
- **Ground team / bike:** the party and its short-hop vehicle; the town itself only walks.
