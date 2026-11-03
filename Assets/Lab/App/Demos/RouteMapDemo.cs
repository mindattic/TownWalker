using System.Collections.Generic;
using System.Linq;
using Lab.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// PoC A — Route Map (node graph, FTL / Slay-the-Spire style). A fogged region map; sites are linked by roads; the
    /// walker token strides from node to node along the A* route; scan pings lift the fog. Tests: fastest decisions,
    /// clearest strategy.
    /// </summary>
    public sealed class RouteMapDemo : Demo
    {
        public override string Id => "routemap";
        public override string Title => "Route Map";
        public override string Pitch => "A fogged map of sites joined by roads. Tap a site, walk the town along the route or send the bike. Fast decisions, clear strategy.";

        private const float MapLeft = 30f, MapTop = 290f, MapWidth = 1110f;
        private float Scale => MapWidth / L.World.Width;

        /// <summary>A tall region that fills the portrait screen between the HUD and the thumb zone.</summary>
        protected override World CreateWorld(int seed) => WorldGen.Create(seed, 120f, 168f, 34);
        private RouteGraph graph;
        private MapLayer layer;
        private WalkerRig2D walker;
        private readonly Dictionary<int, VisualElement> icons = new Dictionary<int, VisualElement>();
        private readonly List<(V2 c, float r)> clear = new List<(V2, float)>();
        private readonly Queue<V2> waypoints = new Queue<V2>();
        private float pulse = -1f;
        private V2 lastPos;

        public Vector2 ToScreen(V2 p) => new Vector2(MapLeft + p.X * Scale, MapTop + p.Y * Scale);
        public override Vector2 TownOnScreen => ToScreen(L.Town.Pos);

        protected override void BuildView(VisualElement view)
        {
            view.style.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
            graph = RouteGraph.Build(L.World, L.Town.Pos);
            clear.Add((L.Town.Pos, 18f));
            layer = new MapLayer(this) { name = "map" };
            Ui.Abs(layer, MapLeft, MapTop, MapWidth, L.World.Height * Scale);
            view.Add(layer);
            foreach (var s in L.World.Sites)
            {
                var ic = new VisualElement { name = "site-" + s.Id, pickingMode = PickingMode.Ignore };
                ic.style.position = Position.Absolute;
                ic.style.width = ic.style.height = 92;
                ic.style.backgroundImage = new StyleBackground(LabArt.SiteIcon(s.Kind));
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                var p = ToScreen(s.Pos);
                ic.style.left = p.x - 46; ic.style.top = p.y - 46;
                view.Add(ic);
                icons[s.Id] = ic;
            }
            walker = new WalkerRig2D(WalkerRig2D.View.Iso) { name = "walker", PixelsPerUnit = 7.5f, Ground = 0.8f };
            walker.style.position = Position.Absolute;
            walker.style.width = 300; walker.style.height = 300;
            view.Add(walker);
            lastPos = L.Town.Pos;
        }

        protected override void OnTap(Vector2 design)
        {
            var s = PickSite(design, x => ToScreen(x.Pos), 70f);
            Select(s);
        }

        /// <summary>The Route Map walks node to node along the road graph.</summary>
        protected override V2 WalkTarget(Site s)
        {
            waypoints.Clear();
            int from = graph.Nodes.OrderBy(n => V2.Distance(n.Value, L.Town.Pos)).First().Key;
            var path = graph.Path(from, s.Id);
            if (path == null) return s.Pos;
            foreach (var id in path.Skip(1)) waypoints.Enqueue(graph.Nodes[id]);
            return waypoints.Count > 0 ? waypoints.Dequeue() : s.Pos;
        }

        protected override void OnArrived()
        {
            if (waypoints.Count > 0 && !L.Town.Stranded) { L.WalkTo(waypoints.Dequeue()); return; }
            base.OnArrived();
        }

        protected override void OnScanned(int found)
        {
            clear.Add((L.Town.Pos, L.Town.ScanRadius));
            pulse = 0f;
        }

        protected override void TickView(float dt)
        {
            // a route interrupted (team sent mid-walk) or cut short (stranded) is abandoned, not resumed later
            if (L.Phase != LoopPhase.Walking) waypoints.Clear();
            if (L.Phase == LoopPhase.Walking && V2.Distance(L.Town.Pos, lastPos) > 0.5f)
            {
                clear.Add((L.Town.Pos, 18f));
                var d = L.Town.Pos - lastPos;
                walker.Heading = Mathf.Atan2(d.Y, d.X);
                lastPos = L.Town.Pos;
            }
            walker.Walking = L.Phase == LoopPhase.Walking;
            var tp = TownOnScreen;
            walker.style.left = tp.x - 150; walker.style.top = tp.y - 240;
            foreach (var s in L.World.Sites)
            {
                var ic = icons[s.Id];
                ic.style.display = s.Revealed ? DisplayStyle.Flex : DisplayStyle.None;
                ic.style.opacity = s.Cleared ? 0.35f : 1f;
                ic.style.scale = new Scale(Vector2.one * (Selected == s ? 1.25f + Mathf.Sin(Time.unscaledTime * 6f) * 0.06f : 1f));
            }
            if (pulse >= 0f) { pulse += dt / 0.9f; if (pulse > 1f) pulse = -1f; }
            layer.MarkDirtyRepaint();
        }

        protected override void OnSiteCleared(Site s) => clear.Add((s.Pos, 10f));

        public bool IsClear(V2 p)
        {
            foreach (var (c, r) in clear) if (V2.Distance(c, p) <= r) return true;
            return false;
        }

        /// <summary>Terrain, fog, roads, the bike-range ring and the radar pulse.</summary>
        private sealed class MapLayer : VisualElement
        {
            private readonly RouteMapDemo d;
            public MapLayer(RouteMapDemo demo) { d = demo; pickingMode = PickingMode.Ignore; generateVisualContent += Draw; }

            private void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                var w = d.L.World;
                float k = d.Scale, cell = 4f;
                for (float y = 0; y < w.Height; y += cell)
                    for (float x = 0; x < w.Width; x += cell)
                    {
                        var g = LabArt.Ground[w.BiomeAt(x, y)];
                        float h = w.HeightAt(x, y);
                        var col = Color.Lerp(g * 0.7f, g * 1.15f, h); col.a = 1f;
                        bool seen = d.IsClear(new V2(x + cell / 2, y + cell / 2));
                        if (!seen) col = Color.Lerp(col, new Color(0.05f, 0.06f, 0.09f), 0.82f);
                        p.fillColor = col;
                        p.BeginPath(); p.MoveTo(new Vector2(x * k, y * k)); p.LineTo(new Vector2((x + cell) * k + 0.5f, y * k)); p.LineTo(new Vector2((x + cell) * k + 0.5f, (y + cell) * k + 0.5f)); p.LineTo(new Vector2(x * k, (y + cell) * k + 0.5f)); p.ClosePath(); p.Fill();
                    }
                // roads between revealed nodes
                p.lineCap = LineCap.Round;
                foreach (var e in d.graph.Edges)
                    foreach (var o in e.Value)
                    {
                        if (o < e.Key) continue;
                        var a = d.graph.Nodes[e.Key]; var b = d.graph.Nodes[o];
                        if (!d.IsClear(a) || !d.IsClear(b)) continue;
                        Dashed(p, new Vector2(a.X * k, a.Y * k), new Vector2(b.X * k, b.Y * k), new Color(0.95f, 0.88f, 0.7f, 0.55f), 5f);
                    }
                // the route ahead
                if (d.waypoints.Count > 0 || d.L.Destination != null)
                {
                    var pts = new List<V2> { d.L.Town.Pos };
                    if (d.L.Destination != null) pts.Add(d.L.Destination.Value);
                    pts.AddRange(d.waypoints);
                    for (int i = 1; i < pts.Count; i++) Line(p, new Vector2(pts[i - 1].X * k, pts[i - 1].Y * k), new Vector2(pts[i].X * k, pts[i].Y * k), new Color(1f, 0.85f, 0.3f, 0.95f), 9f);
                }
                var t = new Vector2(d.L.Town.Pos.X * k, d.L.Town.Pos.Y * k);
                Ring(p, t, d.L.Town.BikeRange * k, new Color(0.5f, 0.85f, 1f, 0.6f), 4f);
                if (d.pulse >= 0f) Ring(p, t, d.L.Town.ScanRadius * k * d.pulse, new Color(0.3f, 1f, 0.75f, 1f - d.pulse), 10f);
                if (d.Selected != null)
                {
                    var s = new Vector2(d.Selected.Pos.X * k, d.Selected.Pos.Y * k);
                    Ring(p, s, 62f, new Color(1f, 0.85f, 0.3f, 0.95f), 6f);
                }
            }

            private static void Line(Painter2D p, Vector2 a, Vector2 b, Color c, float w) { p.strokeColor = c; p.lineWidth = w; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }

            private static void Dashed(Painter2D p, Vector2 a, Vector2 b, Color c, float w)
            {
                float len = Vector2.Distance(a, b); int n = Mathf.Max(1, (int)(len / 22f));
                for (int i = 0; i < n; i += 2) Line(p, Vector2.Lerp(a, b, i / (float)n), Vector2.Lerp(a, b, Mathf.Min(1f, (i + 1) / (float)n)), c, w);
            }

            private static void Ring(Painter2D p, Vector2 c, float r, Color col, float w)
            {
                p.strokeColor = col; p.lineWidth = w;
                p.BeginPath(); p.Arc(c, Mathf.Max(1f, r), Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            }
        }
    }
}
