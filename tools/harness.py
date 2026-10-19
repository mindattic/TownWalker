"""
Self-play harness: launches the development player with the automation endpoint and drives it.

    python tools/harness.py smoke            # title → new game → hub → battle (autoplay) → results
    python tools/harness.py sweep            # screenshot + layout-audit every menu screen
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

BOARD_LEFT, BOARD_TOP, TILE = 45, 450, 180


def tile(x, y):
    return (BOARD_LEFT + TILE * x + TILE // 2, BOARD_TOP + TILE * y + TILE // 2)


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


def drag_tiles(*tiles, step=0.12):
    pts = ";".join(f"{x},{y}" for x, y in (tile(*t) for t in tiles))
    return call("drag", path=pts, step=step)


def smoke(run="smoke"):
    art = launch(run)
    files = [shot("01-title")]
    call("clicktext", text="New Game"); time.sleep(0.8)
    call("clicktext", text="Slot 1"); time.sleep(1.2)
    files.append(shot("02-hub"))
    call("clicktext", text="Adventure"); time.sleep(0.8)
    files.append(shot("03-stages"))
    call("clicktext", text="Green Valley I"); time.sleep(1.5)
    st = call("state")
    if st["screen"] == "crawl":
        files.append(shot("04-crawl"))
        call("clicktext", text="Skip"); time.sleep(1.5)
    files.append(shot("05-battle"))
    call("autoplay", on=1)
    for i in range(40):
        time.sleep(1.5)
        st = call("state")
        if i % 3 == 0 and st["screen"] == "battle":
            files.append(shot(f"06-battle-{i:02d}"))
        if st["screen"] != "battle":
            break
    time.sleep(1)
    files.append(shot("07-results"))
    logs = call("logs")
    print(json.dumps({"final": call("state").get("screen"), "save": call("state").get("save"), "errors": [l for l in logs if l.startswith(("Error", "Exception"))]}, indent=1))
    print("\n".join(files))


def sweep(run="sweep"):
    launch(run)
    call("newgame", slot=0, gold=5000)
    time.sleep(1)
    report = {}
    for screen in ["hub", "stages", "items", "status", "equip", "abilities", "party", "shop", "blacksmith",
                   "alchemist", "summon", "bounties", "bestiary", "settings", "credits"]:
        call("goto", screen=screen)
        time.sleep(0.9)
        shot(f"sweep-{screen}")
        report[screen] = call("layout").get("problems")
        call("back")
        time.sleep(0.6)
    print(json.dumps(report, indent=1))
    logs = call("logs")
    print("errors:", [l for l in logs if l.startswith(("Error", "Exception"))])


def campaign(run="campaign", stages=6, speed=3):
    """Autoplays consecutive stages through the real UI (Next stage / Retry), checking for stalls and errors."""
    launch(run)
    call("newgame", slot=0)
    time.sleep(1)
    call("speed", x=speed)
    call("clicktext", text="Adventure"); time.sleep(0.8)
    call("clicktext", text="Green Valley I"); time.sleep(1.2)
    if call("state")["screen"] == "crawl":
        call("clicktext", text="Skip"); time.sleep(1.2)
    results = []
    for n in range(stages):
        call("autoplay", on=1)
        t0 = time.time(); last_clock = -1; stuck = 0; shot_taken = False
        while time.time() - t0 < 240:
            time.sleep(1.0)
            st = call("state")
            if st["screen"] != "battle":
                break
            b = st["battle"]
            if not shot_taken and b["clock"] > 60:
                shot(f"stage{n}-mid"); shot_taken = True
            stuck = stuck + 1 if b["clock"] == last_clock and not b["busy"] else 0
            last_clock = b["clock"]
            if stuck > 12:
                results.append({"stage": n, "result": "STALLED", "state": b}); break
        time.sleep(1.2)
        st = call("state")
        shot(f"stage{n}-result")
        title = "?"
        results.append({"stage": n, "screen": st["screen"], "gold": st.get("save", {}).get("Gold"), "levels": st.get("save", {}).get("levels")})
        if st["screen"] != "postbattle":
            break
        r = call("clicktext", text="Next stage")
        if isinstance(r, dict) and r.get("error"):
            call("clicktext", text="Retry")
        time.sleep(1.5)
        if call("state")["screen"] == "crawl":
            call("clicktext", text="Skip"); time.sleep(1.2)
    logs = call("logs")
    print(json.dumps(results, indent=1))
    print("errors:", [l[:300] for l in logs if l.startswith(("Error", "Exception")) and "first mouse" not in l])


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "smoke"
    if cmd == "smoke":
        smoke(sys.argv[2] if len(sys.argv) > 2 else "smoke")
    elif cmd == "campaign":
        campaign(stages=int(sys.argv[2]) if len(sys.argv) > 2 else 6)
    elif cmd == "sweep":
        sweep(sys.argv[2] if len(sys.argv) > 2 else "sweep")
    elif cmd == "shot":
        print(shot(sys.argv[2]))
    elif cmd == "call":
        kv = dict(a.split("=", 1) for a in sys.argv[3:])
        print(json.dumps(call(sys.argv[2], **kv), indent=1))
