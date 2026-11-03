using Lab.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// PoC B — Horizon Strip (side view). A long side-scrolling stretch of surface in parallax layers (sky, far ridges,
    /// mid hills, near ground); the walker strides across it, big and centred; sites sit on the horizon, the farther the
    /// higher and smaller; the radar sweeps an arc. Tap the ground to walk there, tap a site to choose it.
    /// Tests: maximum atmosphere for minimal effort. Expected weakness: little sense of exploration (1-D).
    /// </summary>
    public sealed class HorizonDemo : Demo
    {
        public override string Id => "horizon";
        public override string Title => "Horizon Strip";
        public override string Pitch => "Side view: the walker strides across parallax landscapes; the radar paints sites on the horizon. Maximum atmosphere.";

        private const float GroundY = 1640f, CenterX = 585f, PxPerUnit = 26f;
        private float camX;
        private Parallax layers;
        private WalkerRig2D walker;
        private readonly System.Collections.Generic.Dictionary<int, VisualElement> icons = new System.Collections.Generic.Dictionary<int, VisualElement>();
        private float pulse = -1f;

        /// <summary>A long, shallow strip: x is the journey, y is how far back from the path a site sits.</summary>
        protected override World CreateWorld(int seed)
        {
            var w = WorldGen.Create(seed, 420f, 40f, 30, 13f);
            return w;
        }

        public override Vector2 TownOnScreen => new Vector2(CenterX, GroundY - 200f);

        private Vector2 SiteOnScreen(Site s)
        {
            float depth = s.Pos.Y / L.World.Height;          // 0 near … 1 far
            float k = Mathf.Lerp(1f, 0.45f, depth);            // farther sites scroll slower and sit smaller
            return new Vector2(CenterX + (s.Pos.X - camX) * PxPerUnit * k, Mathf.Lerp(GroundY - 40f, GroundY - 520f, depth));
        }

        protected override void BuildView(VisualElement view)
        {
            L.Town.Pos = new V2(14f, 0f); // the town walks the road at the front of the strip
            // the session revealed the world's centre before the town was moved here: undo that, reveal only around the town
            foreach (var s in L.World.Sites) s.Revealed = false;
            L.Reveal(L.Town.Pos, 18f);
            camX = L.Town.Pos.X;
            layers = new Parallax(this) { name = "parallax" };
            Ui.Fill(layers);
            view.Add(layers);
            foreach (var s in L.World.Sites)
            {
                var ic = new VisualElement { name = "site-" + s.Id, pickingMode = PickingMode.Ignore };
                ic.style.position = Position.Absolute;
                ic.style.backgroundImage = new StyleBackground(LabArt.SiteIcon(s.Kind));
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                view.Add(ic);
                icons[s.Id] = ic;
            }
            walker = new WalkerRig2D(WalkerRig2D.View.Side) { name = "walker", PixelsPerUnit = 30f, Ground = 0.93f };
            Ui.Abs(walker, CenterX - 450f, GroundY - 640f, 900f, 690f);
            view.Add(walker);
        }

        protected override void OnTap(Vector2 design)
        {
            var s = PickSite(design, SiteOnScreen, 90f);
            if (s != null) { Select(s); return; }
            if (design.y > GroundY - 600f && design.y < GroundY + 300f && !L.Town.Stranded)
            {
                // tap the ground: walk there (x only — the strip is the road)
                float x = camX + (design.x - CenterX) / PxPerUnit;
                L.WalkTo(new V2(Mathf.Clamp(x, 0f, L.World.Width), L.Town.Pos.Y));
            }
        }

        /// <summary>Walking stays on the road (y fixed); stop a little short of the site.</summary>
        protected override V2 WalkTarget(Site s)
        {
            float dir = Mathf.Sign(s.Pos.X - L.Town.Pos.X);
            return new V2(s.Pos.X - dir * Mathf.Min(Mathf.Abs(s.Pos.X - L.Town.Pos.X), 6f), L.Town.Pos.Y);
        }

        protected override void OnScanned(int found) => pulse = 0f;

        protected override void TickView(float dt)
        {
            camX = Mathf.Lerp(camX, L.Town.Pos.X, 1f - Mathf.Exp(-dt * 4f));
            walker.Walking = L.Phase == LoopPhase.Walking;
            if (L.Destination != null) walker.Facing = L.Destination.Value.X >= L.Town.Pos.X ? 1 : -1;
            foreach (var s in L.World.Sites)
            {
                var ic = icons[s.Id];
                bool on = s.Revealed;
                var p = SiteOnScreen(s);
                ic.style.display = on && p.x > -120 && p.x < 1290 ? DisplayStyle.Flex : DisplayStyle.None;
                float size = Mathf.Lerp(120f, 64f, s.Pos.Y / L.World.Height) * (Selected == s ? 1.2f : 1f);
                ic.style.width = ic.style.height = size;
                ic.style.left = p.x - size / 2f; ic.style.top = p.y - size;
                ic.style.opacity = s.Cleared ? 0.35f : 1f;
            }
            if (pulse >= 0f) { pulse += dt / 1.1f; if (pulse > 1f) pulse = -1f; }
            layers.Scroll();
        }

        /// <summary>Sky, the biome backdrop, three hill layers and the near ground, scrolling at different speeds.</summary>
        private sealed class Parallax : VisualElement
        {
            private readonly HorizonDemo d;
            private readonly VisualElement backdrop;

            public Parallax(HorizonDemo demo)
            {
                d = demo;
                pickingMode = PickingMode.Ignore;
                backdrop = new VisualElement { pickingMode = PickingMode.Ignore };
                backdrop.style.position = Position.Absolute;
                backdrop.style.left = -400; backdrop.style.top = 260; backdrop.style.width = 2000; backdrop.style.height = 1100;
                backdrop.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                backdrop.style.unityBackgroundImageTintColor = new Color(0.75f, 0.75f, 0.85f);
                Add(backdrop);
                generateVisualContent += Draw;
            }

            private int shownBiome = -1;

            /// <summary>Style changes must happen outside the draw callback (UI Toolkit rule).</summary>
            public void Scroll()
            {
                int biome = d.L.World.BiomeAt(d.L.Town.Pos.X, 20f);
                if (biome != shownBiome) { shownBiome = biome; backdrop.style.backgroundImage = new StyleBackground(LabArt.Background(biome)); }
                backdrop.style.left = -400 - d.camX * 0.6f % 400f;
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext ctx)
            {
                var p = ctx.painter2D;
                var w = d.L.World;
                int biome = w.BiomeAt(d.L.Town.Pos.X, 20f);
                var g = LabArt.Ground[biome];
                Hills(p, d.camX * 0.18f, 1250f, 260f, 0.004f, new Color(0.22f, 0.24f, 0.32f, 0.92f), 11);
                Hills(p, d.camX * 0.45f, 1420f, 180f, 0.007f, Color.Lerp(g * 0.55f, new Color(0.2f, 0.22f, 0.3f), 0.4f) + new Color(0, 0, 0, 1), 23);
                Hills(p, d.camX * 0.75f, 1560f, 90f, 0.012f, g * 0.8f + new Color(0, 0, 0, 1), 37);
                // the road band
                p.fillColor = g * 0.95f + new Color(0, 0, 0, 1);
                p.BeginPath(); p.MoveTo(new Vector2(0, GroundY - 30)); p.LineTo(new Vector2(1170, GroundY - 30)); p.LineTo(new Vector2(1170, 2532)); p.LineTo(new Vector2(0, 2532)); p.ClosePath(); p.Fill();
                p.strokeColor = new Color(0.95f, 0.88f, 0.7f, 0.4f); p.lineWidth = 6f;
                for (float x = -(d.camX * PxPerUnit) % 80f; x < 1170f; x += 80f) { p.BeginPath(); p.MoveTo(new Vector2(x, GroundY + 60)); p.LineTo(new Vector2(x + 40, GroundY + 60)); p.Stroke(); }
                // destination flag
                if (d.L.Destination != null)
                {
                    float fx = CenterX + (d.L.Destination.Value.X - d.camX) * PxPerUnit;
                    p.strokeColor = new Color(1f, 0.85f, 0.3f); p.lineWidth = 6f;
                    p.BeginPath(); p.MoveTo(new Vector2(fx, GroundY + 20)); p.LineTo(new Vector2(fx, GroundY - 110)); p.Stroke();
                    p.fillColor = new Color(1f, 0.85f, 0.3f);
                    p.BeginPath(); p.MoveTo(new Vector2(fx, GroundY - 110)); p.LineTo(new Vector2(fx + 60, GroundY - 90)); p.LineTo(new Vector2(fx, GroundY - 70)); p.ClosePath(); p.Fill();
                }
                // bike range on the road
                float br = d.L.Town.BikeRange * PxPerUnit;
                p.strokeColor = new Color(0.5f, 0.85f, 1f, 0.7f); p.lineWidth = 8f;
                p.BeginPath(); p.MoveTo(new Vector2(CenterX - br, GroundY + 110)); p.LineTo(new Vector2(CenterX + br, GroundY + 110)); p.Stroke();
                // the radar sweep: an arc rising from the town
                if (d.pulse >= 0f)
                {
                    float r = d.L.Town.ScanRadius * PxPerUnit * 1.4f * d.pulse;
                    p.strokeColor = new Color(0.3f, 1f, 0.75f, 1f - d.pulse); p.lineWidth = 12f;
                    p.BeginPath(); p.Arc(new Vector2(CenterX, GroundY - 380f), Mathf.Max(1f, r), Angle.Degrees(180), Angle.Degrees(360)); p.Stroke();
                }
            }

            private static void Hills(Painter2D p, float offset, float baseY, float amp, float freq, Color c, int seed)
            {
                p.fillColor = c;
                p.BeginPath(); p.MoveTo(new Vector2(0, 2532));
                for (float x = 0; x <= 1170; x += 30)
                {
                    float u = (x + offset * 10f) * freq;
                    float y = baseY - amp * (0.55f * Mathf.Sin(u + seed) + 0.3f * Mathf.Sin(u * 2.3f + seed * 1.7f) + 0.15f * Mathf.Sin(u * 5.1f + seed * 0.3f) + 0.5f) * 0.8f;
                    p.LineTo(new Vector2(x, y));
                }
                p.LineTo(new Vector2(1170, 2532)); p.ClosePath(); p.Fill();
            }
        }
    }
}
