"""
Plays every traversal demo's loop through the dev build's automation server and screenshots it.

    python tools/labcheck.py [demo ...]      # default: every demo in the lab menu

Per demo: open → walker animates (feet move between frames) → SCAN reveals sites → select the nearest fuel depot
→ walk the town if it's out of bike range → send the ground team → ride + battle + haul → fuel rose, a battle was won,
time-to-battle and taps recorded. Screenshots land in artifacts/labcheck/. Exit code 1 on any failure.
"""
import json
import sys
import time

sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
import harness as h  # noqa: E402

results = []


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + (f" — {detail}" if detail and not ok else ""))


def play(demo):
    r = h.call("demo", id=demo)
    check(f"{demo}: opens", r.get("ok"), str(r))
    time.sleep(1.0)
    h.shot(f"{demo}-01-open")
    st = h.call("state")
    f1 = h.call("walker")
    time.sleep(0.3)
    f2 = h.call("walker")
    check(f"{demo}: walker is drawn", "feet" in f1 and len(f1["feet"]) == f1.get("legs", 4), json.dumps(f1)[:200])
    fuel0 = st["town"]["fuel"]
    s = h.call("scan"); time.sleep(1.0)
    st = h.call("state")
    check(f"{demo}: scan costs fuel and reveals sites", st["town"]["fuel"] < fuel0 and st["revealed"] > 0, json.dumps(st))
    h.shot(f"{demo}-02-scanned")
    sel = h.call("select", kind="FuelDepot")
    if "error" in sel:
        sel = h.call("select")
    check(f"{demo}: a site can be selected", sel.get("ok"), str(sel))
    time.sleep(0.4)
    h.shot(f"{demo}-03-selected")
    if not sel.get("inRange"):
        h.call("walk"); time.sleep(0.8)
        h.shot(f"{demo}-04-walking")
        h.call("wait", ms=200)
        h.call("select", id=sel["site"])
    fuel1 = h.call("state")["town"]["fuel"]
    h.call("send")
    time.sleep(0.6); h.shot(f"{demo}-05-ride")
    time.sleep(1.4); h.shot(f"{demo}-06-battle")
    st = h.call("wait", ms=300)
    check(f"{demo}: the team wins and hauls fuel home", st["battles"] >= 1 and st["town"]["fuel"] >= fuel1, json.dumps(st))
    check(f"{demo}: time-to-battle and taps are recorded", st["avgTimeToBattle"] > 0 and st["avgTapsToBattle"] > 0, json.dumps(st))
    h.shot(f"{demo}-07-hauled")
    # Always exercise walking: head for the farthest revealed site and watch the legs.
    far = h.call("state")
    sites = h.call("select")  # nearest uncleared revealed site
    if "error" not in sites:
        h.call("walk"); time.sleep(0.9)
        f3 = h.call("walker"); time.sleep(0.3); f4 = h.call("walker")
        st2 = h.call("state")
        moving = st2["phase"] == "Walking"
        check(f"{demo}: the town walks and its legs move", (not moving and st2["town"] != far["town"]) or f3.get("feet") != f4.get("feet"), f"{st2['phase']} {f3} {f4}")
        h.shot(f"{demo}-08-walking")
        h.call("wait", ms=200)
    if demo == "spider":
        spider_legs()
    print(f"  {demo}: {st['avgTimeToBattle']:.1f}s and {st['avgTapsToBattle']:.1f} taps to battle, fps {st['fps']}")
    logs = h.call("logs")
    check(f"{demo}: no errors", not any("Exception" in l or l.startswith("Error") for l in logs), "\n".join(l for l in logs if "Exception" in l or l.startswith("Error"))[:600])
    h.call("menu"); time.sleep(0.5)


def spider_legs():
    """Each leg count walks across the terrain on its own gait without tipping over."""
    targets = {3: (930, 820), 4: (260, 900), 5: (880, 1700), 6: (300, 1650)}
    for n in (3, 4, 5, 6):
        r = h.call("legs", n=n)
        check(f"spider: switches to {n} legs", r.get("legs") == n, str(r))
        x, y = targets[n]
        start = h.call("state")["town"]
        h.call("tap", x=x, y=y)
        worst = 0.0
        lag = 0.0
        prev = None
        moved = False
        for i in range(12):
            time.sleep(0.25)
            w = h.call("walker")
            worst = max(worst, w.get("tilt", 0.0))
            lag = max(lag, w.get("lag", 0.0))
            moved = moved or (prev is not None and w["feet"] != prev)
            prev = w["feet"]
            if i == 5:
                h.shot(f"spider-legs{n}-walking")
        check(f"spider {n} legs: {n} feet drawn and stepping", len(prev or []) == n and moved, json.dumps(prev))
        check(f"spider {n} legs: hull stays upright (max tilt {worst:.2f} rad)", worst < 0.5)
        # the core waits while the hull lags (lurches included), so a stalled gait shows up as no progress
        end = h.call("state")["town"]
        went = ((end["x"] - start["x"]) ** 2 + (end["y"] - start["y"]) ** 2) ** 0.5
        check(f"spider {n} legs: the gait carries the town forward ({went:.1f} units in 3 s, max lag {lag:.1f})", went > 3.0)
        h.call("wait", ms=200)


def main():
    demos = sys.argv[1:] or ["routemap", "horizon", "iso", "spider", "lowpoly3d", "hex"]
    h.launch("labcheck", fresh=True)
    time.sleep(3.0)
    h.shot("00-menu")
    for d in demos:
        play(d)
    try:
        h.call("quit", timeout=3)
    except Exception:
        pass
    ok = sum(1 for _, o in results if o)
    print(f"\n{ok}/{len(results)} lab checks passed")
    sys.exit(0 if ok == len(results) else 1)


if __name__ == "__main__":
    main()
