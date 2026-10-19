using System.Collections;
using System.Linq;
using Lab.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// One traversal proof of concept. Every demo plays the identical loop (scan → choose a site → walk the town
    /// and/or send the team → battle → haul) through the shared <see cref="LabSession"/>; a demo only supplies the
    /// view (how the world is drawn) and how a tap maps to a site or a walk target. The HUD, the ride, the placeholder
    /// battle and the haul are shared so the demos compare like for like (time-to-battle, taps-per-decision).
    /// </summary>
    public abstract class Demo
    {
        public abstract string Id { get; }
        public abstract string Title { get; }
        public abstract string Pitch { get; }

        protected LabApp App;
        public LabSession L { get; private set; }
        protected VisualElement Root, View, Hud;
        public Site Selected { get; private set; }
        public bool Busy { get; private set; }

        private VisualElement fuelFill, sitePanel;
        private Label toast;
        private Label fuelLabel, resLabel, statsLabel, phaseLabel;

        public void Mount(LabApp app, VisualElement parent, int seed)
        {
            App = app;
            L = new LabSession(CreateWorld(seed));
            Root = new VisualElement { name = "demo-" + Id };
            Ui.Fill(Root);
            parent.Add(Root);
            View = new VisualElement { name = "view" };
            Ui.Fill(View);
            View.RegisterCallback<PointerDownEvent>(e => { if (e.button == 0) Tap(e.localPosition); });
            Root.Add(View);
            BuildView(View);
            Hud = new VisualElement { name = "hud", pickingMode = PickingMode.Ignore };
            Ui.Fill(Hud);
            Root.Add(Hud);
            BuildHud();
            RefreshHud();
        }

        /// <summary>The region this demo explores (each demo may shape it to its screen).</summary>
        protected virtual World CreateWorld(int seed) => WorldGen.Create(seed);

        protected abstract void BuildView(VisualElement view);
        protected abstract void TickView(float dt);
        /// <summary>A tap on the view in design coordinates (1170×2532): select a site, or pick a walk target.</summary>
        protected abstract void OnTap(Vector2 design);
        /// <summary>Draw the radar pulse; <paramref name="found"/> sites were just revealed.</summary>
        protected abstract void OnScanned(int found);
        /// <summary>Where the ride beat starts (the town) in design coordinates — for the bike's departure.</summary>
        public abstract Vector2 TownOnScreen { get; }

        public virtual void Unmount() { }

        public void Tick(float dt)
        {
            if (L.Tick(dt)) OnArrived();
            TickView(dt);
            RefreshHud();
        }

        protected virtual void OnArrived() => Toast(L.Town.Stranded ? "Out of fuel — the town is stranded" : "The town settles on its legs");

        // ── Actions (also driven by the automation server) ────────────────────

        public void Tap(Vector2 design)
        {
            if (Busy) return;
            L.Tap();
            OnTap(design);
        }

        public void DoScan()
        {
            if (Busy) return;
            L.Tap();
            int n = L.Scan();
            OnScanned(n);
            Toast(L.Town.Fuel < L.Town.ScanCost && n == 0 ? "Not enough fuel to run the radar" : $"Radar: {n} new site{(n == 1 ? "" : "s")}");
        }

        public void Select(Site s)
        {
            Selected = s;
            BuildSitePanel();
        }

        public void WalkSelected()
        {
            if (Busy || Selected == null) return;
            L.Tap();
            L.WalkTo(WalkTarget(Selected));
            Toast("The town lumbers toward " + Selected.Name);
        }

        /// <summary>Default: stop a little short of the site, inside bike range.</summary>
        protected virtual V2 WalkTarget(Site s)
        {
            var d = s.Pos - L.Town.Pos;
            float stop = Mathf.Min(d.Length, Mathf.Max(0f, d.Length - L.Town.BikeRange * 0.5f));
            return L.Town.Pos + d.Normalized * stop;
        }

        public void SendSelected()
        {
            if (Busy || Selected == null) return;
            L.Tap();
            var ex = L.Send(Selected);
            if (ex == null) { Toast(Selected.Cleared ? "Already cleared" : "Out of bike range — walk the town closer"); return; }
            App.StartCoroutine(RunExpedition(ex));
        }

        // ── Shared HUD (one-handed: everything the thumb needs sits at the bottom) ──

        private void BuildHud()
        {
            var top = Ui.Window(Hud, "window--compact");
            top.pickingMode = PickingMode.Position;
            Ui.Abs(top, 16, 16, LabApp.DesignWidth - 32);
            var row = Ui.El("row", top);
            Ui.Button("‹", () => App.ShowMenu(), "btn--small btn--icon", row).name = "back";
            var title = Ui.Text(Title, "h2 grow", row);
            title.style.marginLeft = 16;
            resLabel = Ui.Text("", "small gold", row);
            var fuelRow = Ui.El("row", top);
            fuelRow.style.marginTop = 10;
            Ui.Text("FUEL", "tiny dim", fuelRow).style.width = 90;
            var gauge = Ui.El("gauge grow", fuelRow);
            gauge.style.height = 26;
            fuelFill = Ui.El("gauge-fill", gauge);
            fuelFill.name = "fuel-fill";
            fuelLabel = Ui.Text("", "small", fuelRow);
            fuelLabel.style.marginLeft = 12; fuelLabel.style.width = 140;
            phaseLabel = Ui.Text("", "tiny dim", top);

            var bottom = Ui.El("", Hud);
            bottom.pickingMode = PickingMode.Ignore;
            Ui.Abs(bottom, 16, LabApp.DesignHeight - 250, LabApp.DesignWidth - 32, 230);
            var scan = Ui.Button("SCAN", DoScan, "btn--primary", bottom);
            scan.name = "scan";
            Ui.Abs(scan, (LabApp.DesignWidth - 32) / 2f - 170, 60, 340, 130);
            scan.style.fontSize = 52;
            statsLabel = Ui.Text("", "tiny dim shadow", bottom);
            Ui.Abs(statsLabel, 0, 196, LabApp.DesignWidth - 32);
            statsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            sitePanel = Ui.Window(Hud);
            sitePanel.name = "site-panel";
            sitePanel.pickingMode = PickingMode.Position;
            Ui.Abs(sitePanel, 16, LabApp.DesignHeight - 640, LabApp.DesignWidth - 32);
            sitePanel.style.display = DisplayStyle.None;

            toast = Ui.Text("", "toast", Hud);
            toast.style.opacity = 0;
            toast.style.bottom = 680;
        }

        private void BuildSitePanel()
        {
            sitePanel.Clear();
            var s = Selected;
            if (s == null) { sitePanel.style.display = DisplayStyle.None; return; }
            sitePanel.style.display = DisplayStyle.Flex;
            var head = Ui.El("row", sitePanel);
            var icon = Ui.El("", head);
            icon.style.width = icon.style.height = 110; icon.style.marginRight = 20;
            icon.style.backgroundImage = new StyleBackground(LabArt.SiteIcon(s.Kind));
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            var col = Ui.El("col grow", head);
            Ui.Text(s.Name + (s.Cleared ? "  ✓ cleared" : ""), "h2", col);
            Ui.Text($"Danger {new string('◆', s.Danger)}{new string('◇', 5 - s.Danger)} · {s.Waves} wave{(s.Waves == 1 ? "" : "s")}{(s.HasBoss ? " + boss" : "")}", "small warn", col);
            Ui.Text($"Haul: {(s.Fuel > 0 ? $"{s.Fuel} fuel " : "")}{(s.Ore > 0 ? $"{s.Ore} ore " : "")}{(s.Scrap > 0 ? $"{s.Scrap} scrap" : "")}", "small gold", col);
            float dist = V2.Distance(L.Town.Pos, s.Pos);
            bool range = L.InBikeRange(s);
            Ui.Text($"{dist:0} km away · {(range ? "in bike range" : $"bike reaches {L.Town.BikeRange:0} km")}", "tiny dim", col);
            var row = Ui.El("row", sitePanel);
            row.style.marginTop = 14; row.style.justifyContent = Justify.SpaceBetween;
            var walk = Ui.Button("Walk town", WalkSelected, null, row);
            walk.name = "walk"; walk.style.flexGrow = 1;
            var send = Ui.Button("Send team", SendSelected, "btn--primary", row);
            send.name = "send"; send.style.flexGrow = 1; send.style.marginLeft = 16;
            Ui.SetEnabled(send, range && !s.Cleared);
            Ui.SetEnabled(walk, !L.Town.Stranded);
        }

        private void RefreshHud()
        {
            var t = L.Town;
            fuelFill.style.width = Length.Percent(Mathf.Clamp01(t.Fuel / t.FuelMax) * 100f);
            fuelFill.EnableInClassList("gauge-fill--low", t.Fuel < t.FuelMax * 0.25f);
            fuelLabel.text = $"{t.Fuel:0}/{t.FuelMax:0}";
            resLabel.text = $"ore {t.Ore}  scrap {t.Scrap}";
            phaseLabel.text = L.Phase == LoopPhase.Walking ? "Walking…" : t.Stranded ? "STRANDED — send the team for fuel" : "Tap a site · SCAN to reveal more";
            statsLabel.text = $"battles {L.BattlesWon} · avg {L.AverageTimeToBattle:0.0}s and {L.AverageTapsToBattle:0.0} taps to battle · scans {L.Scans}";
        }

        protected void Toast(string text)
        {
            toast.text = text;
            toast.style.opacity = 1;
            toast.schedule.Execute(() => toast.style.opacity = 0).ExecuteLater(1600);
        }

        // ── The ride, the battle, the haul (shared beats) ──────────────────────

        public int ExpeditionsRun { get; private set; }

        private IEnumerator RunExpedition(Expedition ex)
        {
            Busy = true;
            sitePanel.style.display = DisplayStyle.None;
            var ride = Ui.Window(App.Overlay);
            ride.name = "ride";
            Ui.Abs(ride, 40, 820, LabApp.DesignWidth - 80, 420);
            ride.style.overflow = Overflow.Hidden;
            var strip = Ui.El("", ride);
            Ui.Abs(strip, -28, -22, LabApp.DesignWidth - 80 + 56, 300);
            strip.style.backgroundImage = new StyleBackground(LabArt.Background(L.World.BiomeAt(ex.Site.Pos.X, ex.Site.Pos.Y)));
            strip.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            var bike = Ui.El("", ride);
            bike.name = "bike";
            Ui.Abs(bike, -140, 150, 140, 110);
            bike.style.backgroundImage = new StyleBackground(LabArt.Icon("bike"));
            bike.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            var caption = Ui.Text($"Ground team rides out to the {ex.Site.Name} — {ex.RoadFights} road fight{(ex.RoadFights == 1 ? "" : "s")} on the way", "small shadow", ride);
            Ui.Abs(caption, 0, 300, LabApp.DesignWidth - 140);
            caption.style.whiteSpace = WhiteSpace.Normal;
            float w = LabApp.DesignWidth - 80;
            yield return Tween.Run(ex.RideSeconds, k =>
            {
                bike.style.left = -140 + (w + 140) * k;
                bike.style.top = 150 + Mathf.Sin(k * 40f) * 4f; // rough terrain
                strip.style.left = -28 - 28 * k;
            });
            for (int i = 0; i < ex.RoadFights; i++)
            {
                caption.text = $"Road fight {i + 1}/{ex.RoadFights} — monsters on the trail!";
                yield return Tween.Wait(0.45f);
            }
            ride.RemoveFromHierarchy();

            // Placeholder battle: waves, then the boss.
            L.BeginBattle();
            var card = Ui.Window(App.Overlay);
            card.name = "battle-card";
            Ui.Abs(card, 80, 900, LabApp.DesignWidth - 160);
            Ui.Text("⚔ BATTLE", "h1", card).style.unityTextAlign = TextAnchor.MiddleCenter;
            var line = Ui.Text("", "h2", card);
            line.style.unityTextAlign = TextAnchor.MiddleCenter;
            Ui.Text("(placeholder battle)", "tiny dim", card).style.unityTextAlign = TextAnchor.MiddleCenter;
            int total = ex.Site.Waves + (ex.Site.HasBoss ? 1 : 0);
            for (int i = 0; i < total; i++)
            {
                line.text = i < ex.Site.Waves ? $"Wave {i + 1}/{ex.Site.Waves}" : "BOSS";
                yield return Tween.Wait(0.4f);
            }
            card.RemoveFromHierarchy();
            var site = ex.Site;
            L.EndBattle(true);
            ExpeditionsRun++;
            OnSiteCleared(site);
            Toast($"Haul home: +{site.Fuel} fuel{(site.Ore > 0 ? $", +{site.Ore} ore" : "")}{(site.Scrap > 0 ? $", +{site.Scrap} scrap" : "")}");
            Selected = null;
            BuildSitePanel();
            Busy = false;
        }

        protected virtual void OnSiteCleared(Site s) { }

        /// <summary>The nearest revealed, uncleared site to a design point within <paramref name="radius"/>, for tap selection.</summary>
        protected Site PickSite(Vector2 design, System.Func<Site, Vector2> toScreen, float radius = 110f)
        {
            Site best = null; float bd = radius;
            foreach (var s in L.World.Sites.Where(x => x.Revealed))
            {
                float d = Vector2.Distance(toScreen(s), design);
                if (d < bd) { bd = d; best = s; }
            }
            return best;
        }
    }
}
