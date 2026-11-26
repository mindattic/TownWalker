---
description: Launch the deployed TownWalker Release build from R:\MindAttic\Apps.
argument-hint: ""
allowed-tools: Read, Bash, PowerShell
---

# /run — TownWalker

Project-owned override of the generic `/run` skill: this repo already has a deployed, standalone
copy — don't search for a launch pattern, just start it:

```
powershell -NoProfile -ExecutionPolicy Bypass -File run.ps1 -RunRelease
```

If nothing is deployed yet, say so and point at `/deploy` (it builds the copy this launches) —
don't fall back to running the Unity Editor or a dev build instead.

This is a mechanical launch, not project work — it needs no workflow ticket, no orchestrator
approval, and no pace-gate hold. Run it the instant this command is invoked.
