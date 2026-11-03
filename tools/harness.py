"""
Self-play harness: launches the development player with the automation endpoint and drives it.
The scripted per-demo walkthrough is tools/labcheck.py, which imports this module.

    python tools/harness.py launch [RUN]     # start the development player (artifacts/RUN, default "manual")
    python tools/harness.py shot NAME        # screenshot the running player
    python tools/harness.py call PATH [k=v]  # raw endpoint call, e.g. call state

Screenshots land in artifacts/<run>/. Design coordinates are 1170x2532 (top-left origin).
"""
import json
import os
import subprocess
import sys
import time
import urllib.parse
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
EXE = os.path.join(ROOT, "Build", "Windows", "TownWalker.exe")
PORT = 7777
BASE = f"http://127.0.0.1:{PORT}"

def call(endpoint, timeout=130, **params):
    q = urllib.parse.urlencode({k: v for k, v in params.items() if v is not None})
    url = f"{BASE}/{endpoint}" + (f"?{q}" if q else "")
    with urllib.request.urlopen(url, timeout=timeout) as r:
        body = r.read().decode("utf-8")
    try:
        return json.loads(body)
    except json.JSONDecodeError:
        return body


def alive():
    try:
        call("ping", timeout=2)
        return True
    except Exception:
        return False


def launch(run, width=585, height=1266, extra=(), fresh=False):
    if alive():
        try:
            call("quit", timeout=3)
        except Exception:
            pass
        time.sleep(1.5)
    art = os.path.join(ROOT, "artifacts", run)
    saves = os.path.join(ROOT, "artifacts", run, "saves")
    if fresh and os.path.isdir(saves):
        import shutil
        shutil.rmtree(saves, ignore_errors=True)
    os.makedirs(saves, exist_ok=True)
    args = [EXE, "-automation", str(PORT), "-artifacts", art, "-saveRoot", saves,
            "-screen-width", str(width), "-screen-height", str(height), "-screen-fullscreen", "0",
            "-logFile", os.path.join(art, "player.log"), *extra]
    # Automated runs skip MindAttic.Capture (capture.json defaults it on): harness launches are
    # frequent and short-lived, and a lossless recording of every labcheck/smoke run would flood
    # R:\Recording for no benefit. MINDATTIC_CAPTURE=0 is a hard kill switch, so passing -capture
    # in `extra` would NOT re-enable it here; a deliberately recorded run should set the env var
    # itself instead of going through launch().
    env = {**os.environ, "MINDATTIC_CAPTURE": "0"}
    subprocess.Popen(args, cwd=ROOT, env=env)
    for _ in range(60):
        if alive():
            return art
        time.sleep(0.5)
    raise RuntimeError("player did not start")


def shot(name):
    return call("screenshot", name=name)["file"]


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else ""
    if cmd == "launch":
        print(launch(sys.argv[2] if len(sys.argv) > 2 else "manual"))
    elif cmd == "shot":
        print(shot(sys.argv[2]))
    elif cmd == "call":
        kv = dict(a.split("=", 1) for a in sys.argv[3:])
        print(json.dumps(call(sys.argv[2], **kv), indent=1))
    else:
        print(__doc__)
        sys.exit(2)
