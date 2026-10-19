#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Lab.Core;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// Dev-build remote control (start with <c>-automation 7777</c>), trimmed from GridGameGaiden's: semantic commands
    /// drive the demos (tap at a design point, scan, select/walk/send) so a script can play every PoC and screenshot it.
    /// </summary>
    public sealed class LabAutomation : MonoBehaviour
    {
        private HttpListener listener;
        private Thread thread;
        private readonly ConcurrentQueue<Action> main = new ConcurrentQueue<Action>();
        private readonly List<string> logs = new List<string>();
        private string artifacts;
        private static readonly object Pending = new object();

        public static void TryStart(LabApp app)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-automation");
            if (i < 0 || i + 1 >= args.Length) return;
            int a = Array.IndexOf(args, "-artifacts");
            var s = app.gameObject.AddComponent<LabAutomation>();
            s.artifacts = a >= 0 && a + 1 < args.Length ? args[a + 1] : Path.Combine(Application.persistentDataPath, "artifacts");
            s.Begin(int.Parse(args[i + 1]));
        }

        private void Begin(int port)
        {
            Directory.CreateDirectory(artifacts);
            Application.logMessageReceivedThreaded += (msg, stack, type) =>
            {
                if (type == LogType.Log && !msg.StartsWith("[")) return;
                lock (logs) { logs.Add($"{type}: {msg}" + (type == LogType.Exception || type == LogType.Error ? "\n" + stack : "")); if (logs.Count > 300) logs.RemoveAt(0); }
            };
            listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            thread = new Thread(() =>
            {
                while (listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = listener.GetContext(); } catch { return; }
                    ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
                }
            }) { IsBackground = true };
            thread.Start();
            Debug.Log($"[Automation] listening on {port}");
        }

        private void OnDestroy() { try { listener?.Stop(); } catch { } }

        private void Update() { while (main.TryDequeue(out var a)) { try { a(); } catch (Exception e) { Debug.LogException(e); } } }

        private void Handle(HttpListenerContext ctx)
        {
            string body; int status = 200;
            try
            {
                var q = ctx.Request.QueryString;
                var path = ctx.Request.Url.AbsolutePath.Trim('/');
                var done = new ManualResetEventSlim();
                object result = null; Exception error = null;
                main.Enqueue(() =>
                {
                    try { var r = Dispatch(path, q, v => { result = v; done.Set(); }); if (r != Pending) { result = r; done.Set(); } }
                    catch (Exception e) { error = e; done.Set(); }
                });
                if (!done.Wait(TimeSpan.FromSeconds(60))) throw new TimeoutException("main thread did not answer");
                if (error != null) throw error;
                body = result as string ?? JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception e) { status = 500; body = JsonConvert.SerializeObject(new { error = e.Message }); }
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }

        private static float F(string s, float d = 0f) => float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;

        private object Dispatch(string path, System.Collections.Specialized.NameValueCollection q, Action<object> reply)
        {
            var app = LabApp.I;
            var d = app.Current;
            switch (path)
            {
                case "ping": return new { ok = true, demo = d?.Id };
                case "state": return State();
                case "logs": lock (logs) { var c = logs.ToList(); if (q["clear"] == "1") logs.Clear(); return c; }
                case "menu": app.ShowMenu(); return new { ok = true };
                case "demo": app.Open(q["id"]); return new { ok = app.Current?.Id == q["id"], demo = app.Current?.Id };
                case "tap": if (d == null) return new { error = "no demo" }; d.Tap(new Vector2(F(q["x"]), F(q["y"]))); return new { ok = true, selected = d.Selected?.Id };
                case "scan": d?.DoScan(); return new { ok = d != null, revealed = d?.L.World.Sites.Count(s => s.Revealed) };
                case "select":
                {
                    if (d == null) return new { error = "no demo" };
                    var site = q["id"] != null ? d.L.World.Sites.FirstOrDefault(s => s.Id == (int)F(q["id"]))
                        : d.L.World.Sites.Where(s => s.Revealed && !s.Cleared && (q["kind"] == null || s.Kind.ToString() == q["kind"])).OrderBy(s => V2.Distance(s.Pos, d.L.Town.Pos)).FirstOrDefault();
                    if (site == null) return new { error = "no such site" };
                    d.Select(site);
                    return new { ok = true, site = site.Id, kind = site.Kind.ToString(), inRange = d.L.InBikeRange(site), dist = V2.Distance(site.Pos, d.L.Town.Pos) };
                }
                case "walk": d?.WalkSelected(); return new { ok = d != null };
                case "send": d?.SendSelected(); return new { ok = d != null, busy = d?.Busy };
                case "seed": app.Seed = (int)F(q["value"], app.Seed); return new { ok = true, app.Seed };
                case "wait": StartCoroutine(WaitIdle(F(q["ms"], 300) / 1000f, reply)); return Pending;
                case "screenshot":
                case "shot": StartCoroutine(Shot(q["name"] ?? DateTime.Now.ToString("HHmmss"), reply)); return Pending;
                case "names":
                {
                    var prefix = q["prefix"] ?? "";
                    return app.Document.rootVisualElement.Query<VisualElement>().ToList().Where(e => e.name != null && e.name.StartsWith(prefix) && e.worldBound.width > 0).Select(e => e.name).ToList();
                }
                case "walker": return Walker(q["name"] ?? "walker");
                case "legs":
                    if (!(d is SpiderDemo sd)) return new { error = "not the spider demo" };
                    sd.SetLegs((int)F(q["n"], 4));
                    return new { ok = true, legs = sd.LegCount };
                case "quit": Application.Quit(); return new { ok = true };
            }
            return new { error = "unknown endpoint " + path };
        }

        private object State()
        {
            var d = LabApp.I.Current;
            var st = new Dictionary<string, object> { ["demo"] = d?.Id, ["fps"] = Mathf.Round(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)), ["screen"] = new[] { Screen.width, Screen.height } };
            if (d != null)
            {
                var l = d.L;
                st["phase"] = l.Phase.ToString();
                st["busy"] = d.Busy;
                st["town"] = new { x = l.Town.Pos.X, y = l.Town.Pos.Y, fuel = l.Town.Fuel, l.Town.Ore, l.Town.Scrap, stranded = l.Town.Stranded };
                st["revealed"] = l.World.Sites.Count(s => s.Revealed);
                st["cleared"] = l.World.Sites.Count(s => s.Cleared);
                st["battles"] = l.BattlesWon;
                st["taps"] = l.Taps;
                st["avgTimeToBattle"] = l.AverageTimeToBattle;
                st["avgTapsToBattle"] = l.AverageTapsToBattle;
                st["selected"] = d.Selected?.Id;
            }
            return st;
        }

        /// <summary>The walker's current leg pose (foot positions) — proves the gait animates between frames.</summary>
        private object Walker(string name)
        {
            var w = LabApp.I.Document.rootVisualElement.Q<WalkerRig2D>(name);
            if (w == null)
            {
                var sp = LabApp.I.Document.rootVisualElement.Q<SpiderRig>();
                if (sp?.Body != null)
                {
                    return new { feet = sp.FootSnapshot(), legs = sp.Body.N, tilt = sp.Body.Tilt.magnitude, lag = sp.Body.Lag, sway = sp.Body.Sway.magnitude };
                }
                var r3 = FindAnyObjectByType<WalkerRig3D>();
                if (r3 != null) return new { feet = r3.FootSnapshot() };
                return new { error = "no walker named " + name };
            }
            return new { feet = w.FootSnapshot() };
        }

        private IEnumerator WaitIdle(float min, Action<object> reply)
        {
            float t = 0f;
            while (t < min || (LabApp.I.Current != null && (LabApp.I.Current.Busy || LabApp.I.Current.L.Phase == LoopPhase.Walking)))
            {
                t += Time.unscaledDeltaTime;
                if (t > 50f) break;
                yield return null;
            }
            reply(State());
        }

        private IEnumerator Shot(string name, Action<object> reply)
        {
            yield return new WaitForEndOfFrame();
            var file = Path.Combine(artifacts, name.EndsWith(".png") ? name : name + ".png");
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Destroy(tex);
            reply(new { file });
        }
    }
}
#endif
