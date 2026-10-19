using Lab.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// PoC D′ — Spider Town. The Isometric Expanse, walked by a physics-driven spider town instead of the quadruped
    /// rig: a rigid, radially symmetric hull (no front, it never turns, it walks any direction) on 3–6 spring legs,
    /// each with an insect's high /\ knee, stepping in a free gait that only lifts a leg when the remaining feet can
    /// carry the body. Every foot plants on the terrain step under it, and the hull tips to match.
    /// </summary>
    public sealed class SpiderDemo : IsoDemo
    {
        public override string Id => "spider";
        public override string Title => "Spider Town";
        public override string Pitch => "The iso expanse walked by a physics spider town: 3 to 6 insect legs, no front (it walks any direction without turning), real spring-leg physics, a gait for each leg count.";

        /// <summary>World units of height per terrain level: one StepPx step is drawn at ~15 px per unit.</summary>
        private const float LevelHeight = StepPx / 15f;

        private SpiderRig rig;
        private SpiderPhysics body;
        private readonly Button[] legButtons = new Button[4];
        public int LegCount { get; private set; } = 4;

        private float GroundAt(float x, float y) => Level(x, y) * LevelHeight;

        private Vector2 Project(Vector3 w) => ToScreen(new V2(w.x, w.y), w.z / LevelHeight);

        protected override void BuildTown(VisualElement view)
        {
            rig = new SpiderRig { name = "spider", Project = Project, Px = TileW / 2f / Cell };
            Ui.Fill(rig);
            view.Add(rig);
            SetLegs(LegCount);

            var bar = Ui.El("row", view);
            bar.name = "leg-picker";
            Ui.Abs(bar, 16, 250, LabApp.DesignWidth - 32);
            bar.style.justifyContent = Justify.Center;
            // a tap on the picker must not also walk the town
            bar.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            for (int i = 0; i < legButtons.Length; i++)
            {
                int n = i + 3;
                var b = Ui.Button($"{n} legs", () => SetLegs(n), "btn--small", bar);
                b.name = "legs-" + n;
                b.style.marginLeft = b.style.marginRight = 10;
                legButtons[i] = b;
            }
            RefreshPicker();
        }

        public void SetLegs(int n)
        {
            LegCount = Mathf.Clamp(n, 3, 6);
            var at = body != null ? new Vector2(body.P.x, body.P.y) : new Vector2(L.Town.Pos.X, L.Town.Pos.Y);
            body = new SpiderPhysics(LegCount, GroundAt, at, body?.Yaw ?? 0f);
            rig.Body = body;
            RefreshPicker();
        }

        private void RefreshPicker()
        {
            for (int i = 0; i < legButtons.Length; i++)
                legButtons[i]?.EnableInClassList("btn--primary", i + 3 == LegCount);
        }

        protected override void TickTown(float dt, float heading)
        {
            // radially symmetric: no front to turn, it just walks whichever way the town is going
            body.Step(dt, new Vector2(L.Town.Pos.X, L.Town.Pos.Y), L.Phase == LoopPhase.Walking);
            // the town goes only as fast as its gait: when the hull falls behind, the core waits for its legs
            L.Holding = L.Phase == LoopPhase.Walking && body.Lag > (L.Holding ? 2.5f : 4f);
            rig.Tick(dt);
        }
    }
}
