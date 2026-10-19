---
description: Launch the published TownWalker build from C:\Apps.
argument-hint: ""
allowed-tools: Read, Bash, PowerShell
---

# /run — TownWalker

Project-owned override of the generic `/run` skill: this repo already has a published, standalone
copy — don't search for a launch pattern, just start it:

```
Start-Process "C:\Apps\TownWalker\TownWalker.exe"
```

If that path is missing, say so and point at `/deploy` (it publishes the copy this launches and
refreshes the "TownWalker" Desktop shortcut) — don't fall back to running the Unity Editor or
a dev build instead.

This is a mechanical launch, not project work — it needs no workflow ticket, no orchestrator
approval, and no pace-gate hold. Run it the instant this command is invoked.
