---
description: Build the Debug and Release Windows players and deploy them to R:\MindAttic\Apps\TownWalker.
argument-hint: "[-RunRelease]"
allowed-tools: Read, Bash, PowerShell
---

# /deploy — TownWalker

Project-owned override of the global MindAttic `/deploy` router: this repo's deploy mechanism is
the standard `run.ps1` at the repo root (`run.bat` opens its menu), authoritative per the router's
own rule 1. Run it directly, right away:

```
powershell -NoProfile -ExecutionPolicy Bypass -File run.ps1 -Redeploy $ARGUMENTS
```

This is a mechanical build/publish utility, not project work — it needs no workflow ticket, no
orchestrator approval, and no pace-gate hold. Run it the instant this command is invoked and
surface its output verbatim (build errors, deploy paths). Don't second-guess what the script does;
don't fabricate steps beyond what it prints.

What it does: `Invoke-AppRun` in MindAttic.Agency's `shared\deploy-lib.ps1` builds the Unity
project in batch mode twice (Development into `R:\MindAttic\Apps\TownWalker\Debug`, Release into
`R:\MindAttic\Apps\TownWalker\Release`), stopping any copy running from those folders first.
`-RunRelease` then starts the Release copy. Requires the Unity Editor closed for this project.
