# TownWalker — a traversal lab for the walking town (separate repo)

## Context

The owner's direction (2026-10-10):

- Mankind lives in **towns on giant walking legs** because the surface is overrun. The player's party is one of the ground teams that disembark to fight, mine and bring back fuel.
- The town is the hub, so the shops move with the party.
- The town needs **fuel**. It **scans**, picks a destination, and either walks there or sends the team by bike or machine. The team fights minor mobs on the road, then waves and a boss at the site, then collects the fuel and maybe mines extra.
- It must be one-handed and simple. It must **not overshadow the battles**, and it must not be Conestoga wagons or trains.
- The owner doesn't yet know which traversal style works:
  - side view feels easy but kills exploration;
  - hex grid feels turn-based;
  - isometric would be amazing but there's no art;
  - full 3D would be best if assets allow.
- The owner wants **several working demos** of traversal methods, built in a **separate Unity project** (new repo `https://github.com/mindattic/TownWalker`) so the idea doesn't leak into GridGameGaiden yet.

**Out of scope (owner's answers):**
- No changes to GridGameGaiden, its bible or its systems.
- No fuel or stranding rules decided yet. Those are developed in the new repo.

**Mood board (owner image).**
- A dense, tiered stone or steam town (spires, chimneys, smoke) on a mechanical undercarriage with **4 massive jointed legs**, walking like a lumbering quadruped (Howl's Moving Castle, Mortal Engines).
- Those images are licensed, stock or AI art, so they are a **style reference only**. Everything here is drawn or modelled from scratch, or taken from CC0 sources.

**Design guard-rails for every demo:**
1. **Battle-first.** Traversal is a 15–40 s beat between expeditions. Each demo logs *time-to-next-battle* and *taps-per-decision*.
2. **One hand.** Tap, hold or drag only; no virtual sticks, no menus inside menus.
3. **Legs, not wheels.**
   - The town always moves by walking: a visible gait, with footfalls and a sway the hub can inherit.
   - The bike or crawler is only the ground team's short-hop vehicle.
4. **Same loop in every demo**, so they compare like for like:
   - scan → choose a site → walk the town and/or send the team → a placeholder "battle" card (Gaiden's battles plug in later) → haul back → the fuel gauge changes.

## Project setup (the new repo)

- Clone `mindattic/TownWalker` to `D:\Projects\MindAttic\TownWalker`.
- Create a Unity **6000.4.3f1** project there in batchmode (`-createProject`, URP).
- Use the same code-only conventions as Gaiden: one bootstrap scene, everything built in C#, UI Toolkit HUD, Git LFS for binaries.
- Copy, don't link, the small reusable bits from Gaiden:
  - `Tween`, `Ui` helpers, the `Art` loader pattern;
  - the dev automation server (HTTP: state, tap, drag, screenshot, quit), plus `tools/harness.py` and `tools/build.ps1` / `unity.ps1`;
  - `tools/make_icons.py` and the pixel pipeline (for map icons).
- **Lab menu:** a start screen listing the demos, plus a "loop scoreboard" (time-to-battle, taps).

## Shared core (pure C#, in the new repo)

- `WalkerTown` model: position, heading, fuel, scan radius, gait phase.
- `Site` (fuel depot, mine, wreck, nest, pass) with reward, danger and distance.
- `Radar.Scan(radius)` reveals sites. `Route` uses A* on the demo's own space (graph, hex, iso grid or 3D navgrid).
- `Team.Send(site, vehicleRange)` produces 0–2 road encounters, the site, then the haul.
- The loop above runs identically in every demo. Only the presentation and input differ.
- Small dotnet tests for scan, route, fuel and haul, as in Gaiden.

## The walker (shared rig, three fidelities)

- **2D pixel walker** (side view, map token):
  - layered pixel-art town body (undercarriage, 3 tiers of houses and spires, chimneys with pixel smoke) composited from game-icons silhouettes through the icon pipeline;
  - **4 two-bone legs** drawn with `Painter2D` (hip → knee → foot pad, pistons);
  - diagonal-pair quadruped gait with foot planting, dust puffs, body bob, sway and list.
- **Iso walker:** the same rig projected 2:1 isometric, with the body sprite re-rendered at a 3/4 angle (procedural shading).
- **3D walker:**
  - procedural low-poly: a hexagonal or rounded platform, extruded blocky town tiers, spires and chimneys (primitive meshes built in code);
  - 4 legs as cylinders and boxes with **2-bone analytic IK** to raycast foot targets on the terrain; a gait controller (step when a foot strays past threshold, diagonal pairs); body height and tilt follow the feet;
  - chimney particle smoke. No external assets needed.
- **Optional CC0 asset pass** (only if procedural looks too plain): Kenney (CC0) 3D kits for terrain props and buildings, and Quaternius (CC0) low-poly packs for monsters and mechs. Licences are verified and recorded in `CREDITS.md` before anything is imported.

## The demos (each is a playable PoC with the full loop)

1. **Horizon Strip (side view).**
   - Parallax biome layers; the walker strides left or right while you hold.
   - The radar sweep paints sites on the horizon; forks offer up and down paths to the next strip.
   - Tests: maximum atmosphere and minimal effort. Expected weakness: little exploration.
2. **Route Map (node graph).**
   - Fog-covered region; scan pings reveal nodes and paths.
   - Tap a node: "Walk town" (the walker token strides along the path, fuel ticks) or "Send team" (a bike-range ring, a road beat).
   - Tests: the fastest decisions and the clearest strategy.
3. **Radar Sectors (hex).**
   - Hex fog; scans reveal rings.
   - Tested in **real time with pause-on-decision** rather than turns: the town walks continuously toward the chosen hex, and you can retarget anytime.
   - Tests whether hex reads as "turn-based"; if it does, it gets cut.
4. **Isometric Expanse (2.5D).**
   - Procedural iso terrain: diamond tiles from the biome palettes, height steps, and props (rocks, ruins, fuel derricks) from pixel silhouettes.
   - Tap-to-walk anywhere (A* on the iso grid), camera follow.
   - Radar rings reveal sites. The bike is a small iso sprite driving to the site.
   - Tests: exploration and spectacle with art we can actually make.
5. **3D Low-Poly Walker (full 3D).**
   - Perlin heightmap terrain mesh with vertex-colour biomes, fog, and procedural props.
   - Tap-to-walk, with the IK-legged walker climbing terrain; the camera orbits gently on a one-finger drag.
   - Radar pulse shader rings reveal sites. A crawler vehicle drives out.
   - Tests: whether 3D is worth it (frame time on mobile, readability, effort).

**Build order:** 2D walker rig → Route Map → Horizon Strip → Isometric → 3D → Hex. This gets something showable fastest; 3D is the riskiest.

## Verification

- Each demo has a scripted harness run (scan → choose → walk/send → battle card → haul) with screenshots and a short GIF-like frame strip of the gait, reviewed before reporting.
- The scoreboard records time-to-battle and taps.
- Frame-time capture on the Windows build for each demo; the 3D demo also gets a WebGL build plus a browser smoke check, as in Gaiden's `webcheck.mjs`.
- dotnet tests for the shared core.
- Deliverable to the owner: a side-by-side comparison page (screenshots, gait strip, time-to-battle, effort and art notes) with my recommendation. The owner picks; only then is anything ported toward Gaiden.

## Critical files (new repo)

- `Assets/Lab/Core/` (WalkerTown, Site, Radar, Route, Team) and `Tests/Core.Tests/`
- `Assets/Lab/Walker/` (WalkerRig2D, WalkerRigIso, WalkerRig3D, GaitController)
- `Assets/Lab/Demos/` (HorizonDemo, RouteMapDemo, HexDemo, IsoDemo, LowPoly3DDemo)
- `Assets/Lab/App/` (bootstrap, lab menu, scoreboard, automation server copied from `GridGameGaiden/Assets/Gaiden/App/Automation`)
- `tools/` (harness.py, build.ps1, unity.ps1, make_icons.py copied from Gaiden), `README.md`, `CREDITS.md`
