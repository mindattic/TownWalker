using System.Collections.Generic;
using System.Linq;
using Lab.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// PoC C — Radar Sectors (hex fog grid), in REAL TIME rather than turns: the town walks continuously along an A* path
    /// of hex centres and can be retargeted mid-walk; a scan reveals rings of sectors outward from the town. Tests whether
    /// a hex grid can read as exploration rather than a turn-based board (owner's worry).
    /// </summary>
    public sealed class HexDemo : Demo
    {
        public override string Id => "hex";
        public override string Title => "Radar Sectors";
        public override string Pitch => "Hex fog grid in real time: scans reveal rings of sectors, the town walks hex to hex, retarget any time.";

        private const float HexSize = 6f, MapLeft = 30f, MapTop = 290f, MapWidth = 1110f;
        private float Scale => MapWidth / L.World.Width;
        private readonly HashSet<int> seen = new HashSet<int>();
        private readonly Queue<V2> waypoints = new Queue<V2>();
        private Grid grid;
        private WalkerRig2D walker;
        private readonly Dictionary<int, VisualElement> icons = new Dictionary<int, VisualElement>();
        private float pulse = -1f;
        private V2 lastPos;

        protected override World CreateWorld(int seed) => WorldGen.Create(seed, 120f, 168f, 34);

        public Vector2 ToScreen(V2 p) => new Vector2(MapLeft + p.X * Scale, MapTop + p.Y * Scale);
        public override Vector2 TownOnScreen => ToScreen(L.Town.Pos);

        private bool InWorld(Hex h) { var c = h.Center(HexSize); return c.X >= 0 && c.Y >= 0 && c.X <= L.World.Width && c.Y <= L.World.Height; }

        private void RevealRing(V2 at, float radius)
        {
            var c = Hex.FromPoint(at, HexSize);
            int r = Mathf.CeilToInt(radius / (HexSize * 1.5f));
            for (int q = -r; q <= r; q++)
                for (int rr = -r; rr <= r; rr++)
                {
                    var h = new Hex(c.Q + q, c.R + rr);
                    if (h.Distance(c) <= r && InWorld(h)) seen.Add(h.Key);
                }
        }

        protected override void BuildView(VisualElement view)
        {
            view.style.backgroundColor = new Color(0.05f, 0.06f, 0.10f);
            RevealRing(L.Town.Pos, 14f);
            grid = new Grid(this) { name = "hexgrid" };
            Ui.Abs(grid, 0, 0, 1170, 2532);
            view.Add(grid);
            foreach (var s in L.World.Sites)
            {
                var ic = new VisualElement { name = "site-" + s.Id, pickingMode = PickingMode.Ignore };
                ic.style.position = Position.Absolute;
                ic.style.width = ic.style.height = 80;
                ic.style.backgroundImage = new StyleBackground(LabArt.SiteIcon(s.Kind));
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var p = ToScreen(Hex.FromPoint(s.Pos, HexSize).Center(HexSize));
                ic.style.left = p.x - 40; ic.style.top = p.y - 40;
                view.Add(ic);
                icons[s.Id] = ic;
            }
            walker = new WalkerRig2D(WalkerRig2D.View.Iso) { name = "walker", PixelsPerUnit = 6.5f, Ground = 0.8f };
            walker.style.position = Position.Absolute;
            walker.style.width = 260; walker.style.height = 260;
            view.Add(walker);
            lastPos = L.Town.Pos;
        }

        private List<V2> HexPath(V2 from, V2 to)
        {
            var a = Hex.FromPoint(from, HexSize); var b = Hex.FromPoint(to, HexSize);
            var path = AStar.Find(a.Key, b.Key,
                k => { var h = Hex.FromKey(k); return Enumerable.Range(0, 6).Select(i => h.Neighbour(i)).Where(InWorld).Select(x => x.Key); },
                (x, y) => 1f + L.World.HeightAt(Hex.FromKey(y).Center(HexSize).X, Hex.FromKey(y).Center(HexSize).Y) * 1.5f, // hills cost a little more
                k => Hex.FromKey(k).Distance(b));
            return path?.Skip(1).Select(k => Hex.FromKey(k).Center(HexSize)).ToList() ?? new List<V2>();
        }

        private void Go(V2 target)
        {
            waypoints.Clear();
            foreach (var p in HexPath(L.Town.Pos, target)) waypoints.Enqueue(p);
            if (waypoints.Count > 0) L.WalkTo(waypoints.Dequeue());
        }

        protected override void OnTap(Vector2 design)
        {
            var s = PickSite(design, x => ToScreen(Hex.FromPoint(x.Pos, HexSize).Center(HexSize)), 60f);
            if (s != null) { Select(s); return; }
            var world = new V2((design.x - MapLeft) / Scale, (design.y - MapTop) / Scale);
            if (world.X < 0 || world.Y < 0 || world.X > L.World.Width || world.Y > L.World.Height || L.Town.Stranded) return;
            Go(Hex.FromPoint(world, HexSize).Center(HexSize)); // retarget any time, even mid-walk
            Select(null);
        }

        protected override V2 WalkTarget(Site s)
        {
            var path = HexPath(L.Town.Pos, s.Pos);
            // stop one sector short of the site (inside bike range)
            if (path.Count > 1) path.RemoveAt(path.Count - 1);
            waypoints.Clear();
            foreach (var p in path) waypoints.Enqueue(p);
            return waypoints.Count > 0 ? waypoints.Dequeue() : L.Town.Pos;
        }

        protected override void OnArrived()
        {
            if (waypoints.Count > 0 && !L.Town.Stranded) { L.WalkTo(waypoints.Dequeue()); return; }
            base.OnArrived();
        }

        protected override void OnScanned(int found) { RevealRing(L.Town.Pos, L.Town.ScanRadius); pulse = 0f; }

        protected override void TickView(float dt)
        {
            // a route interrupted (team sent mid-walk) or cut short (stranded) is abandoned, not resumed later
            if (L.Phase != LoopPhase.Walking) waypoints.Clear();
            if (V2.Distance(L.Town.Pos, lastPos) > 0.3f)
            {
                RevealRing(L.Town.Pos, 14f);
                var d = L.Town.Pos - lastPos;
                walker.Heading = Mathf.Atan2(d.Y, d.X);
                lastPos = L.Town.Pos;
            }
            walker.Walking = L.Phase == LoopPhase.Walking;
            var tp = TownOnScreen;
            walker.style.left = tp.x - 130; walker.style.top = tp.y - 208;
            foreach (var s in L.World.Sites)
            {
                var ic = icons[s.Id];
                ic.style.display = s.Revealed && seen.Contains(Hex.FromPoint(s.Pos, HexSize).Key) ? DisplayStyle.Flex : DisplayStyle.None;
                ic.style.opacity = s.Cleared ? 0.35f : 1f;
                ic.style.scale = new Scale(Vector2.one * (Selected == s ? 1.25f : 1f));
            }
            if (pulse >= 0f) { pulse += dt / 1.0f; if (pulse > 1f) pulse = -1f; }
            grid.MarkDirtyRepaint();
        }

        private sealed class Grid : VisualElement
        {
            private readonly HexDemo d;
            public Grid(HexDemo demo) { d = demo; pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }

            private void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                var w = d.L.World;
                var town = Hex.FromPoint(d.L.Town.Pos, HexSize);
                int ringNow = d.pulse >= 0f ? Mathf.RoundToInt(d.pulse * d.L.Town.ScanRadius / (HexSize * 1.5f)) : -1;
                for (int r = -2; r < 30; r++)
                    for (int q = -20; q < 30; q++)
                    {
                        var h = new Hex(q, r);
                        if (!d.InWorld(h)) continue;
                        var c = h.Center(HexSize);
                        var g = LabArt.Ground[w.BiomeAt(c.X, c.Y)];
                        var col = Color.Lerp(g * 0.75f, g * 1.15f, w.HeightAt(c.X, c.Y)); col.a = 1;
                        bool seen = d.seen.Contains(h.Key);
                        if (!seen) col = Color.Lerp(col, new Color(0.05f, 0.06f, 0.10f), 0.82f);
                        var sc = d.ToScreen(c);
                        float rad = HexSize * d.Scale * 0.97f;
                        p.fillColor = col;
                        p.BeginPath();
                        for (int i = 0; i < 6; i++) { float a = (60f * i - 30f) * Mathf.Deg2Rad; var v = sc + new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad); if (i == 0) p.MoveTo(v); else p.LineTo(v); }
                        p.ClosePath(); p.Fill();
                        if (ringNow >= 0 && h.Distance(town) == ringNow) { p.strokeColor = new Color(0.3f, 1f, 0.75f, 1f - d.pulse); p.lineWidth = 6f; p.Stroke(); }
                    }
                // route ahead
                var pts = new List<V2> { d.L.Town.Pos };
                if (d.L.Destination != null) pts.Add(d.L.Destination.Value);
                pts.AddRange(d.waypoints);
                p.strokeColor = new Color(1f, 0.85f, 0.3f, 0.95f); p.lineWidth = 8f; p.lineCap = LineCap.Round;
                for (int i = 1; i < pts.Count; i++) { p.BeginPath(); p.MoveTo(d.ToScreen(pts[i - 1])); p.LineTo(d.ToScreen(pts[i])); p.Stroke(); }
                // bike range
                p.strokeColor = new Color(0.5f, 0.85f, 1f, 0.6f); p.lineWidth = 4f;
                p.BeginPath(); p.Arc(d.TownOnScreen, d.L.Town.BikeRange * d.Scale, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
        }
    }
}
