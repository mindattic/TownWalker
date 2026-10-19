---
description: Build the Windows release player and publish it to C:\Apps\TownWalker.
argument-hint: "[-Launch]"
allowed-tools: Read, Bash, PowerShell
---

# /deploy — TownWalker

Project-owned override of the global MindAttic `/deploy` router: this repo's deploy mechanism is
`tools\deploy.ps1` (`deploy.bat` wraps it at the repo root), authoritative per the router's own
rule 1. Run it directly, right away:

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools\deploy.ps1 $ARGUMENTS
```

This is a mechanical build/publish utility, not project work — it needs no workflow ticket, no
orchestrator approval, and no pace-gate hold. Run it the instant this command is invoked and
surface its output verbatim (build errors, publish path, shortcut path). Don't second-guess what
the script does; don't fabricate steps beyond what it prints.

What it does: stops any running `TownWalker.exe`, clears `Build\WindowsRelease`, builds via
Unity batchmode (`Lab.EditorTools.LabBuild.BuildWindowsRelease`), copies the result to
`C:\Apps\TownWalker\`, and refreshes the "TownWalker" Desktop shortcut so it always
points at the latest published copy. Requires the Unity Editor closed for this project.
