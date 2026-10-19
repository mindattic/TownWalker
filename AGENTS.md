# TownWalker — agent entrypoint

Read the shared protocol at ..\mindattic-agent-standard\AGENTS.md, then this file and README.md.

## What this is
TownWalker (formerly GridGameCaravan) is a standalone Unity 6 (6000.4.3f1) project about walking towns
crossing the surface. It began as GridGameGaiden's traversal lab and is now its own entity: nothing here
is destined for Gaiden, which has no world map. See README.md for the premise and the demos (Route Map,
Horizon Strip, Isometric Expanse, Spider Town, 3D Low-Poly Walker, Radar Sectors).

## Laws (do not break)
1. **Shared core, comparable demos.** Every demo plays the same `Lab.Core` loop (scan → choose a
   site → walk/bike to it → battle placeholder → haul resources home), so demos stay comparable on
   feel, time-to-battle, and taps per decision.
2. **Everything is code-built.** `Assets/Lab/App/LabApp.cs` boots each demo; no prefab/Inspector
   wiring. The walker (`Assets/Lab/App/Walker/WalkerRig2D.cs`) is drawn from scratch with Painter2D,
   not an asset import — the owner's mood board is a style reference only.
3. **Done is verified by play.** `dotnet test Tests/Core.Tests` (engine-free core: world, town,
   routing, hex) then `python tools/labcheck.py [demo ...]`, which plays each demo's loop via
   automation and writes screenshots to `artifacts/labcheck`. Look at the screenshots.
4. Commit at verified milestones only; end messages with the session attribution lines.

## Build, run and check
```
powershell -File tools/build.ps1                       # Windows dev build -> Build/Windows/TownWalker.exe
python tools/labcheck.py [demo ...]                    # plays each demo's loop, screenshots -> artifacts/labcheck
dotnet test Tests/Core.Tests                           # the engine-free core (world, town, routing, hex)
python tools/make_lab_icons.py                         # pixel icons (game-icons.net, CC BY 3.0)
deploy.bat  /  powershell -File tools/deploy.ps1        # release build -> publish to C:\Apps\TownWalker, refresh Desktop shortcut
```
`/deploy` and `/run` are project-owned commands (`.claude/commands/`) that run `tools/deploy.ps1`
and launch `C:\Apps\TownWalker\TownWalker.exe` directly — mechanical utilities, not
project work, so they run instantly with no ticket or pace-gate hold.

## Map
- `Assets/Lab/Core` — engine-free model (world, town, routing, hex) backing every demo
- `Assets/Lab/App/LabApp.cs` — boots each demo in code
- `Assets/Lab/App/Walker/WalkerRig2D.cs` — the from-scratch walker rig
- `Assets/Lab/Editor` — editor-time tooling
- `tools/build.ps1`, `tools/labcheck.py`, `tools/webcheck.mjs` — build + automated demo verification
