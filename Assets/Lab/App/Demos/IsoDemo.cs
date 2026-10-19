using System.Collections.Generic;
using Lab.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// PoC D — Isometric Expanse (2.5D). Stepped iso terrain from the world's height and biome noise, under fog; the camera
    /// follows the town; tap anywhere to walk there (the walker turns to its heading in the iso projection), tap a site
    /// to choose it. Tests: real exploration and spectacle using art we can generate ourselves.
    /// </summary>
    public class IsoDemo : Demo
    {
        public override string Id => "iso";
        public override string Title => "Isometric Expanse";
        public override string Pitch => "2.5D: tap anywhere to walk the town across hilly iso terrain; radar rings reveal sites. Exploration and spectacle with art we can make.";

        protected const float Cell = 4f, TileW = 132f, TileH = 66f, StepPx = 18f, ScreenCX = 585f, ScreenCY = 1180f;
        private V2 cam;
        private Terrain terrain;
        private WalkerRig2D walker;
        private readonly Dictionary<int, VisualElement> icons = new Dictionary<int, VisualElement>();
        private readonly List<(V2 c, float r)> clear = new List<(V2, float)>();
        private float pulse = -1f, heading;
        private V2 lastPos;

        protected int Level(float x, float y) => Mathf.Clamp((int)(L.World.HeightAt(x, y) * 4f), 0, 3);

        /// <summary>World (x, y) at terrain level → design point.</summary>
        public Vector2 ToScreen(V2 p, float level)
        {
            float u = (p.X - cam.X) / Cell, v = (p.Y - cam.Y) / Cell;
            return new Vector2(ScreenCX + (u - v) * TileW / 2f, ScreenCY + (u + v) * TileH / 2f - level * StepPx);
        }

        private Vector2 SiteOnScreen(Site s) => ToScreen(s.Pos, Level(s.Pos.X, s.Pos.Y));

        /// <summary>Design point → world (on the ground plane; good enough to aim a walk).</summary>
        private V2 FromScreen(Vector2 d)
        {
            float a = (d.x - ScreenCX) / (TileW / 2f), b = (d.y - ScreenCY) / (TileH / 2f);
            float u = (a + b) / 2f, v = (b - a) / 2f;
            return new V2(cam.X + u * Cell, cam.Y + v * Cell);
        }

        public override Vector2 TownOnScreen => ToScreen(L.Town.Pos, Level(L.Town.Pos.X, L.Town.Pos.Y));

        protected override void BuildView(VisualElement view)
        {
            view.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f);
            cam = L.Town.Pos;
            clear.Add((L.Town.Pos, 18f));
            terrain = new Terrain(this) { name = "terrain" };
            Ui.Fill(terrain);
            view.Add(terrain);
            foreach (var s in L.World.Sites)
            {
                var ic = new VisualElement { name = "site-" + s.Id, pickingMode = PickingMode.Ignore };
                ic.style.position = Position.Absolute;
                ic.style.width = ic.style.height = 104;
                ic.style.backgroundImage = new StyleBackground(LabArt.SiteIcon(s.Kind));
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                view.Add(ic);
                icons[s.Id] = ic;
            }
            BuildTown(view);
            lastPos = L.Town.Pos;
        }

        /// <summary>Adds the town's rig to the view, above the terrain and site icons.</summary>
        protected virtual void BuildTown(VisualElement view)
        {
            walker = new WalkerRig2D(WalkerRig2D.View.Iso) { name = "walker", PixelsPerUnit = 13f, Ground = 0.82f };
            // Rotate the foot's rig-local offset by the walker's current heading (the same rotation it already
            // applies to its own geometry) to get a world-space offset from the town, then read this demo's own
            // stepped terrain there — so each leg plants on the ground actually under it, cliffs included.
            walker.GroundAt = (fx, fz) =>
            {
                float c = Mathf.Cos(walker.RenderHeading), s = Mathf.Sin(walker.RenderHeading);
                float wx = L.Town.Pos.X + (fx * c - fz * s), wy = L.Town.Pos.Y + (fx * s + fz * c);
                return Level(wx, wy) * (StepPx / walker.PixelsPerUnit);
            };
            walker.style.position = Position.Absolute;
            walker.style.width = 520; walker.style.height = 520;
            view.Add(walker);
        }

        /// <summary>Per-frame rig update; <paramref name="heading"/> points at the destination (radians, world).</summary>
        protected virtual void TickTown(float dt, float heading)
        {
            walker.Heading = heading;
            walker.Walking = L.Phase == LoopPhase.Walking;
            // The quadruped has a front: it pivots in place, stepping, until it faces where it's going, then sets off.
            L.Holding = L.Phase == LoopPhase.Walking && Mathf.Abs(Mathf.DeltaAngle(walker.RenderHeading * Mathf.Rad2Deg, heading * Mathf.Rad2Deg)) > 4f;
            var tp = TownOnScreen;
            walker.style.left = tp.x - 260; walker.style.top = tp.y - 426;
        }

        protected override void OnTap(Vector2 design)
        {
            var s = PickSite(design, SiteOnScreen, 90f);
            if (s != null) { Select(s); return; }
            var p = FromScreen(design);
            if (p.X < 0 || p.Y < 0 || p.X > L.World.Width || p.Y > L.World.Height || L.Town.Stranded) return;
            L.WalkTo(p);
            Select(null);
        }

        protected override void OnScanned(int found) { clear.Add((L.Town.Pos, L.Town.ScanRadius)); pulse = 0f; }

        protected override void OnSiteCleared(Site s) => clear.Add((s.Pos, 10f));

        public bool IsClear(V2 p) { foreach (var (c, r) in clear) if (V2.Distance(c, p) <= r) return true; return false; }

        protected override void TickView(float dt)
        {
            if (L.Phase == LoopPhase.Walking && V2.Distance(L.Town.Pos, lastPos) > 0.4f)
            {
                clear.Add((L.Town.Pos, 18f));
                lastPos = L.Town.Pos;
            }
            if (L.Destination != null)
            {
                var d = L.Destination.Value - L.Town.Pos;
                if (d.Length > 0.5f) heading = Mathf.Atan2(d.Y, d.X);
            }
            cam = V2.Lerp(cam, L.Town.Pos, 1f - Mathf.Exp(-dt * 3f));
            TickTown(dt, heading);
            foreach (var s in L.World.Sites)
            {
                var ic = icons[s.Id];
                var p = SiteOnScreen(s);
                bool on = s.Revealed && p.x > -100 && p.x < 1270 && p.y > 200 && p.y < 2300;
                ic.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                ic.style.left = p.x - 52; ic.style.top = p.y - 92;
                ic.style.opacity = s.Cleared ? 0.35f : 1f;
                ic.style.scale = new Scale(Vector2.one * (Selected == s ? 1.25f : 1f));
            }
            if (pulse >= 0f) { pulse += dt / 1.0f; if (pulse > 1f) pulse = -1f; }
            terrain.MarkDirtyRepaint();
        }

        /// <summary>Iso tiles back-to-front with stepped cliffs; fog darkens unseen ground; rings for range and radar.</summary>
        private sealed class Terrain : VisualElement
        {
            private readonly IsoDemo d;
            public Terrain(IsoDemo demo) { d = demo; pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }

            private void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                var w = d.L.World;
                int n = (int)(w.Width / Cell);
                int cu = (int)(d.cam.X / Cell), cv = (int)(d.cam.Y / Cell);
                const int R = 13;
                for (int sum = cu + cv - 2 * R; sum <= cu + cv + 2 * R; sum++)
                    for (int u = cu - R; u <= cu + R; u++)
                    {
                        int v = sum - u;
                        if (v < cv - R || v > cv + R || u < 0 || v < 0 || u >= n || v >= n) continue;
                        float x = u * Cell + Cell / 2, y = v * Cell + Cell / 2;
                        int lv = d.Level(x, y);
                        var g = LabArt.Ground[w.BiomeAt(x, y)];
                        var top = Color.Lerp(g * 0.85f, g * 1.15f, lv / 3f); top.a = 1;
                        if (!d.IsClear(new V2(x, y))) top = Color.Lerp(top, new Color(0.05f, 0.06f, 0.10f), 0.8f);
                        var c = d.ToScreen(new V2(u * Cell, v * Cell), lv);
                        var t0 = c; var t1 = c + new Vector2(TileW / 2, TileH / 2); var t2 = c + new Vector2(0, TileH); var t3 = c + new Vector2(-TileW / 2, TileH / 2);
                        // cliff faces down to the ground (level 0)
                        if (lv > 0)
                        {
                            float drop = lv * StepPx;
                            Quad(p, t3, t2, t2 + new Vector2(0, drop), t3 + new Vector2(0, drop), top * 0.62f + new Color(0, 0, 0, 0.38f));
                            Quad(p, t2, t1, t1 + new Vector2(0, drop), t2 + new Vector2(0, drop), top * 0.48f + new Color(0, 0, 0, 0.52f));
                        }
                        Quad(p, t0, t1, t2, t3, top);
                    }
                var tc = d.TownOnScreen;
                Ellipse(p, tc, d.L.Town.BikeRange / Cell * TileW / 2f, d.L.Town.BikeRange / Cell * TileH / 2f, new Color(0.5f, 0.85f, 1f, 0.7f), 5f);
                if (d.pulse >= 0f)
                {
                    float r = d.L.Town.ScanRadius / Cell * d.pulse;
                    Ellipse(p, tc, r * TileW / 2f, r * TileH / 2f, new Color(0.3f, 1f, 0.75f, 1f - d.pulse), 10f);
                }
                if (d.L.Destination != null)
                {
                    var dp = d.ToScreen(d.L.Destination.Value, d.Level(d.L.Destination.Value.X, d.L.Destination.Value.Y));
                    Ellipse(p, dp, 46f, 23f, new Color(1f, 0.85f, 0.3f, 0.95f), 6f);
                }
            }

            private static void Quad(Painter2D p, Vector2 a, Vector2 b, Vector2 c, Vector2 dd, Color col)
            {
                p.fillColor = col;
                p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.LineTo(c); p.LineTo(dd); p.ClosePath(); p.Fill();
            }

            private static void Ellipse(Painter2D p, Vector2 c, float rx, float ry, Color col, float w)
            {
                p.strokeColor = col; p.lineWidth = w;
                p.BeginPath();
                for (int i = 0; i <= 48; i++) { float a = i / 48f * Mathf.PI * 2f; var q = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry); if (i == 0) p.MoveTo(q); else p.LineTo(q); }
                p.Stroke();
            }
        }
    }
}
