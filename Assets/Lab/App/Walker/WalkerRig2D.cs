using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// The walking town, drawn from scratch to the owner's mood board (a dense stone/steam town on a mechanical platform,
    /// four heavy jointed legs, a lumbering quadruped gait). A tiny 3D model — boxes and prisms for the town, two-bone IK legs —
    /// is projected either side-on or 2:1 isometric and painted with Painter2D (back-face culled, depth sorted, lit).
    /// Diagonal leg pairs step together; planted feet stay put on the ground while the body glides; the body bobs and sways;
    /// feet kick up dust as they plant; the chimneys smoke.
    /// </summary>
    public sealed class WalkerRig2D : VisualElement
    {
        public enum View { Side, Iso }

        public View Mode;
        public bool Walking;
        /// <summary>Screen pixels per rig unit (the town platform is ~10 units long).</summary>
        public float PixelsPerUnit = 6f;
        /// <summary>Side view: +1 faces right, −1 left.</summary>
        public int Facing = 1;
        /// <summary>Iso view: the heading to turn toward, in radians (0 = toward +x). The rig doesn't snap to this —
        /// it turns toward it at <see cref="TurnRateDegPerSec"/>; read <see cref="RenderHeading"/> for the actual
        /// drawn orientation.</summary>
        public float Heading;
        /// <summary>How fast the hull turns to face a new heading, in degrees/sec — slow and deliberate, not a snap.</summary>
        public float TurnRateDegPerSec = 70f;
        /// <summary>Where the ground line sits, as a fraction of the element height.</summary>
        public float Ground = 0.9f;
        public float GaitPeriod = 2.4f;
        /// <summary>Rig-local foot offset (forward x, lateral z, in rig units) → the ground height there, in rig
        /// units. Null (the default) means flat ground at y=0, for demos with no real terrain to sample.</summary>
        public Func<float, float, float> GroundAt;

        private float t, walkBlend, smokeTimer;
        // The hull is a rigid body riding on four spring-damper legs: lift (rig units up), roll (about x; +
        // lowers the +z side) and pitch (about z; + raises the bow), with their rates.
        private float lift, roll, pitch, liftV, rollV, pitchV;
        private readonly bool[] planted = new bool[4];
        private float renderHeading;
        private bool headingInit;
        /// <summary>The hull's actual drawn orientation — lags <see cref="Heading"/>, turning toward it gradually.</summary>
        public float RenderHeading => renderHeading;
        private readonly float[] lastPhase = new float[4];
        private readonly List<Puff> smoke = new List<Puff>();
        private readonly List<Puff> dust = new List<Puff>();
        private readonly List<Item> items = new List<Item>();
        private readonly Vector3[] feet = new Vector3[4];
        private readonly float[] footAngle = new float[4];

        private struct Puff { public Vector3 P; public float Age, Life, Size; }
        private struct Item { public float Depth; public int Kind; public int Index; public Vector2[] Poly; public Color Fill; }

        // ── The model (rig units: x forward, y up, z lateral) ────────────────────────
        private struct Box { public Vector3 C, S; public Color Col; public bool Roof; public Color RoofCol; public bool Windows; }
        // Declared before Body: static field initializers run in source order, and BuildBody() (which Body's
        // initializer calls) reads these — after Body, they'd still be default (transparent black) when read.
        private static readonly Color StoneCol = new Color(0.62f, 0.58f, 0.52f);
        private static readonly Color BrassCol = new Color(0.55f, 0.43f, 0.28f);
        private static readonly Color IronCol = new Color(0.34f, 0.33f, 0.34f);
        private static readonly List<Box> Body = BuildBody();
        private static readonly Vector3[] Hips = { new Vector3(3.6f, 7.4f, 3.2f), new Vector3(3.6f, 7.4f, -3.2f), new Vector3(-3.6f, 7.4f, 3.2f), new Vector3(-3.6f, 7.4f, -3.2f) };
        private static readonly float[] PhaseOffset = { 0f, 0.5f, 0.5f, 0f }; // FL+BR, FR+BL
        private const float UpperLeg = 4.9f, LowerLeg = 4.9f, Stance = 0.62f, Lift = 1.4f;
        /// <summary>How far the hoof's toe breaks over (lifts toward the cannon bone) at the peak of swing — 0 at rest,
        /// so the block sits flat on the ground through stance.</summary>
        private const float BreakoverDeg = 22f;
        // Hull dynamics: unit mass; leg springs soft enough that the town visibly settles onto each footfall and
        // rocks onto the diagonal pair it's standing on; wide pads resist tilt up to load × half-width.
        private const float G = 9.8f, LegK = 8f, LegC = 1.0f, RollInertia = 20f, PitchInertia = 26f;
        private const float PadHalfWidth = 0.9f, PadK = 30f, PadC = 8f, SubStep = 1f / 240f;
        private const float LegRest = 7.4f + G / (4f * LegK); // preload: rides at hip height 7.4 on four feet
        private static readonly Vector3[] Chimneys = { new Vector3(-3.6f, 15.6f, 1.6f), new Vector3(-2.4f, 14.6f, -2.2f) };

        private static List<Box> BuildBody()
        {
            var stone = StoneCol;
            var brass = BrassCol;
            var iron = IronCol;
            var redRoof = new Color(0.62f, 0.27f, 0.20f);
            var slate = new Color(0.32f, 0.37f, 0.48f);
            var b = new List<Box>
            {
                // keel, platform and deck are drawn separately as chamfered prisms (see AddChamferedPrism) for a
                // rounded hull edge instead of a sharp right-angle box.
                // tier 1: a ring of houses
                new Box { C = new Vector3(3.6f, 10.4f, 2.6f), S = new Vector3(2.4f, 2.4f, 2.2f), Col = stone, Roof = true, RoofCol = redRoof, Windows = true },
                new Box { C = new Vector3(3.8f, 10.0f, -2.4f), S = new Vector3(2.2f, 1.8f, 2.4f), Col = stone, Roof = true, RoofCol = slate, Windows = true },
                new Box { C = new Vector3(1.0f, 10.6f, 3.0f), S = new Vector3(2.6f, 2.8f, 1.8f), Col = stone, Roof = true, RoofCol = slate, Windows = true },
                new Box { C = new Vector3(-1.6f, 10.2f, 3.0f), S = new Vector3(2.4f, 2.0f, 1.8f), Col = stone, Roof = true, RoofCol = redRoof, Windows = true },
                new Box { C = new Vector3(-4.0f, 10.4f, 2.4f), S = new Vector3(2.2f, 2.4f, 2.6f), Col = stone, Roof = true, RoofCol = redRoof, Windows = true },
                new Box { C = new Vector3(-4.0f, 10.2f, -2.4f), S = new Vector3(2.2f, 2.0f, 2.6f), Col = stone, Roof = true, RoofCol = slate, Windows = true },
                new Box { C = new Vector3(-1.2f, 10.3f, -3.0f), S = new Vector3(3.0f, 2.2f, 1.6f), Col = stone, Roof = true, RoofCol = redRoof, Windows = true },
                new Box { C = new Vector3(1.4f, 10.1f, -3.0f), S = new Vector3(2.0f, 1.8f, 1.6f), Col = stone, Roof = true, RoofCol = slate, Windows = true },
                // tier 2: the keep
                new Box { C = new Vector3(0.0f, 12.2f, 0.0f), S = new Vector3(4.4f, 6.0f, 4.2f), Col = stone, Windows = true },
                new Box { C = new Vector3(0.2f, 16.0f, 0.2f), S = new Vector3(2.4f, 2.4f, 2.4f), Col = stone, Roof = true, RoofCol = slate, Windows = true },
                // chimneys
                new Box { C = new Vector3(-3.6f, 13.4f, 1.6f), S = new Vector3(0.7f, 4.4f, 0.7f), Col = iron },
                new Box { C = new Vector3(-2.4f, 12.8f, -2.2f), S = new Vector3(0.6f, 3.6f, 0.6f), Col = iron },
                // the radar mast at the bow
                new Box { C = new Vector3(4.8f, 12.0f, 0.0f), S = new Vector3(0.3f, 3.6f, 0.3f), Col = iron },
                new Box { C = new Vector3(4.8f, 14.0f, 0.0f), S = new Vector3(1.4f, 0.3f, 1.4f), Col = new Color(0.3f, 0.75f, 0.62f) },
            };
            return b;
        }

        public WalkerRig2D(View mode)
        {
            Mode = mode;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            schedule.Execute(Step).Every(16);
            for (int i = 0; i < 4; i++) feet[i] = new Vector3(Hips[i].x, 0f, Hips[i].z * 1.18f);
        }

        /// <summary>Foot positions on screen (for tests: they must change between frames while walking).</summary>
        public float[][] FootSnapshot()
        {
            var r = new float[4][];
            for (int i = 0; i < 4; i++) { var p = Project(BodyToScreenSpace(feet[i], false)); r[i] = new[] { Mathf.Round(p.x * 10f) / 10f, Mathf.Round(p.y * 10f) / 10f }; }
            return r;
        }

        // ── Animation ───────────────────────────────────────────────────────────────

        private void Step()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            t += dt;
            // The hull turns to face a new heading like a heavy thing actually pivoting on its legs, not a sprite
            // flipping instantly — snap only on the very first tick, since there's no prior orientation to turn from.
            if (!headingInit) { renderHeading = Heading; headingInit = true; }
            else renderHeading = Mathf.Deg2Rad * Mathf.MoveTowardsAngle(renderHeading * Mathf.Rad2Deg, Heading * Mathf.Rad2Deg, TurnRateDegPerSec * dt);
            walkBlend = Mathf.MoveTowards(walkBlend, Walking ? 1f : 0f, dt * 1.5f);
            float stride = 3.0f * walkBlend;
            for (int i = 0; i < 4; i++)
            {
                float p = Mathf.Repeat(t / GaitPeriod + PhaseOffset[i], 1f);
                float fx, fy;
                if (p < Stance) { fx = Mathf.Lerp(stride / 2f, -stride / 2f, p / Stance); fy = 0f; }
                else
                {
                    // Swing matches a trotting quadruped's clearance curve rather than a symmetric arc: the knee snaps to
                    // its most flexed point just after toe-off (peak lift at ~38% of swing, not 50%) for ground clearance,
                    // then extends through a longer, slower reach into footstrike — cat-trot kinematics (thigh ROM ~50°,
                    // knee ROM ~62°, stance-phase knee comparatively stiff).
                    // Clamp01 before Mathf.Pow: a fractional exponent on even a hair-negative base (float round-off
                    // right at the Stance boundary) returns NaN, which would otherwise poison the foot, the IK knee
                    // solve, and every screen point the leg draws.
                    float k = Mathf.Clamp01((p - Stance) / (1f - Stance));
                    fx = Mathf.Lerp(-stride / 2f, stride / 2f, Mathf.SmoothStep(0f, 1f, Mathf.Pow(k, 0.82f)));
                    fy = Mathf.Sin(Mathf.Pow(k, 0.72f) * Mathf.PI) * Lift * walkBlend;
                }
                // Each leg samples the ground at its own local offset — not a shared body height — so a leg
                // standing on a step up or a cliff edge plants where its own foot actually lands, independent of
                // what the other three are doing.
                float lateralZ = Hips[i].z * 1.18f;
                float groundY = GroundAt != null ? GroundAt(Hips[i].x + fx, lateralZ) : 0f;
                feet[i] = new Vector3(Hips[i].x + fx, groundY + fy, lateralZ);
                // The hoof block sits flat on the ground through stance and only tilts (toe breaking over toward the
                // cannon bone) while clearing the ground mid-swing — never below flat, so the sole is always the
                // lowest point of the leg.
                footAngle[i] = (p < Stance ? 0f : Mathf.Sin(Mathf.Pow((p - Stance) / (1f - Stance), 0.72f) * Mathf.PI) * BreakoverDeg) * walkBlend;
                // a foot just planted: dust (the hull's settling onto it comes out of the physics below)
                if (walkBlend > 0.3f && lastPhase[i] >= Stance - 0.02f && p < Stance && p < 0.1f)
                    for (int d = 0; d < 3; d++) dust.Add(new Puff { P = feet[i] + new Vector3(UnityEngine.Random.Range(-0.6f, 0.6f), 0.1f, UnityEngine.Random.Range(-0.6f, 0.6f)), Life = 0.9f, Size = 0.7f });
                lastPhase[i] = p;
                // a foot bears load through stance; once the gait has blended out every foot is down
                planted[i] = p < Stance || walkBlend < 0.05f;
            }
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / SubStep));
            for (int s = 0; s < n; s++) Integrate(dt / n);
            smokeTimer -= dt;
            if (smokeTimer <= 0f)
            {
                smokeTimer = 0.28f;
                foreach (var c in Chimneys) smoke.Add(new Puff { P = c, Life = 2.6f, Size = 0.8f });
            }
            Age(smoke, dt, new Vector3(-0.9f, 1.6f, 0.3f));
            Age(dust, dt, new Vector3(0f, 0.6f, 0f));
            MarkDirtyRepaint();
        }

        /// <summary>One physics substep: each planted leg pushes (never pulls) its hip up with a damped spring
        /// on the gap between hip and foot, so the hull's height, roll and pitch come from which feet are down
        /// and what ground they're standing on, not from a scripted bob.</summary>
        private void Integrate(float h)
        {
            float force = -G, rollT = 0f, pitchT = 0f;
            for (int i = 0; i < 4; i++)
            {
                if (!planted[i]) continue;
                var hp = Hips[i];
                float ext = hp.y + lift - roll * hp.z + pitch * hp.x - feet[i].y;
                float v = liftV - rollV * hp.z + pitchV * hp.x;
                float f = Mathf.Max(0f, LegK * (LegRest - ext) - LegC * v);
                force += f;
                rollT -= f * hp.z;
                pitchT += f * hp.x;
                float cap = f * PadHalfWidth;
                rollT += Mathf.Clamp(-PadK * roll - PadC * rollV, -cap, cap);
                pitchT += Mathf.Clamp(-PadK * pitch - PadC * pitchV, -cap, cap);
            }
            liftV += force * h; lift += liftV * h;
            rollV += rollT / RollInertia * h; roll += rollV * h;
            pitchV += pitchT / PitchInertia * h; pitch += pitchV * h;
            roll = Mathf.Clamp(roll, -0.5f, 0.5f);
            pitch = Mathf.Clamp(pitch, -0.5f, 0.5f);
        }

        private static void Age(List<Puff> list, float dt, Vector3 drift)
        {
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var p = list[i];
                p.Age += dt; p.P += drift * dt; p.Size += dt * 0.9f;
                if (p.Age >= p.Life) list.RemoveAt(i); else list[i] = p;
            }
        }

        /// <summary>Body motion: the simulated hull pose (see <see cref="Integrate"/>).</summary>
        private Vector3 BodyToScreenSpace(Vector3 p, bool onBody)
        {
            if (onBody)
            {
                p = new Vector3(p.x, p.y * Mathf.Cos(roll) - p.z * Mathf.Sin(roll), p.y * Mathf.Sin(roll) + p.z * Mathf.Cos(roll));
                p = new Vector3(p.x * Mathf.Cos(pitch) - p.y * Mathf.Sin(pitch), p.x * Mathf.Sin(pitch) + p.y * Mathf.Cos(pitch), p.z);
                p.y += lift;
            }
            if (Mode == View.Iso)
            {
                float c = Mathf.Cos(renderHeading), s = Mathf.Sin(renderHeading);
                p = new Vector3(p.x * c - p.z * s, p.y, p.x * s + p.z * c);
            }
            else p.x *= Facing;
            return p;
        }

        private Vector2 Project(Vector3 p)
        {
            var r = contentRect;
            float cx = r.width / 2f, gy = r.height * Ground, k = PixelsPerUnit;
            if (Mode == View.Iso) return new Vector2(cx + (p.x - p.z) * 0.866f * k, gy + (p.x + p.z) * 0.5f * k - p.y * k);
            return new Vector2(cx + p.x * k, gy - p.y * k + p.z * 0.22f * k);
        }

        private float Depth(Vector3 p) => Mode == View.Iso ? p.x + p.z + p.y * 0.001f : -p.z + p.y * 0.0005f;

        private Vector3 TowardViewer => Mode == View.Iso ? new Vector3(1f, 1.2f, 1f).normalized : new Vector3(0f, 0.22f, -1f).normalized;

        private static readonly Vector3 Light = new Vector3(0.45f, 0.85f, -0.35f).normalized;

        private Color Shade(Color c, Vector3 n) => Color.Lerp(c * 0.5f, c * 1.12f, Mathf.Clamp01(Vector3.Dot(n, Light) * 0.5f + 0.5f)) + new Color(0, 0, 0, 1);

        // ── Drawing ─────────────────────────────────────────────────────────────────

        private void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width <= 0) return;
            var p2 = ctx.painter2D;
            items.Clear();
            // ground shadow
            var sc = Project(BodyToScreenSpace(Vector3.zero, false));
            p2.fillColor = new Color(0f, 0f, 0f, 0.28f);
            p2.BeginPath();
            float rw = (Mode == View.Iso ? 9.5f : 7.5f) * PixelsPerUnit, rh = (Mode == View.Iso ? 5.5f : 1.4f) * PixelsPerUnit;
            for (int i = 0; i <= 24; i++) { float a = i / 24f * Mathf.PI * 2f; var q = sc + new Vector2(Mathf.Cos(a) * rw, Mathf.Sin(a) * rh); if (i == 0) p2.MoveTo(q); else p2.LineTo(q); }
            p2.Fill();

            // The hull's base — keel, platform, deck — as chamfered prisms: a rounded hull edge, not a right-angle box.
            AddChamferedPrism(new Vector3(0, 6.4f, 0), 3.7f, 2.8f, 5.7f, 7.1f, 1.0f, IronCol);
            AddChamferedPrism(new Vector3(0, 7.9f, 0), 5.3f, 4.0f, 7.0f, 8.8f, 1.6f, BrassCol);
            AddChamferedPrism(new Vector3(0, 9.0f, 0), 5.1f, 3.8f, 8.8f, 9.2f, 1.5f, StoneCol);
            foreach (var b in Body) AddBox(b);
            for (int i = 0; i < 4; i++) items.Add(new Item { Kind = 1, Index = i, Depth = Depth(BodyToScreenSpace(Vector3.Lerp(Hips[i], feet[i], 0.5f), false)) });
            items.Sort((a, b) => a.Depth.CompareTo(b.Depth));
            foreach (var it in items)
            {
                if (it.Kind == 1) DrawLeg(p2, it.Index);
                else Poly(p2, it.Poly, it.Fill);
            }
            foreach (var d in dust) Circle(p2, Project(BodyToScreenSpace(d.P, false)), d.Size * PixelsPerUnit, new Color(0.78f, 0.7f, 0.58f, 0.5f * (1f - d.Age / d.Life)));
            foreach (var s in smoke) Circle(p2, Project(BodyToScreenSpace(s.P, true)), s.Size * PixelsPerUnit, new Color(0.55f, 0.55f, 0.58f, 0.55f * (1f - s.Age / s.Life)));
        }

        private void AddBox(Box b)
        {
            var h = b.S * 0.5f;
            var c = b.C;
            Vector3[] v =
            {
                c + new Vector3(-h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, -h.z), c + new Vector3(h.x, h.y, -h.z), c + new Vector3(-h.x, h.y, -h.z),
                c + new Vector3(-h.x, -h.y, h.z), c + new Vector3(h.x, -h.y, h.z), c + new Vector3(h.x, h.y, h.z), c + new Vector3(-h.x, h.y, h.z),
            };
            int[][] faces = { new[] { 0, 1, 2, 3 }, new[] { 5, 4, 7, 6 }, new[] { 4, 0, 3, 7 }, new[] { 1, 5, 6, 2 }, new[] { 3, 2, 6, 7 }, new[] { 4, 5, 1, 0 } };
            Vector3[] normals = { Vector3.back, Vector3.forward, Vector3.left, Vector3.right, Vector3.up, Vector3.down };
            for (int f = 0; f < 6; f++)
            {
                var n = BodyToScreenSpace(normals[f], false); // rotate the normal with the heading (no translation)
                if (Mode == View.Side) n = new Vector3(normals[f].x * Facing, normals[f].y, normals[f].z);
                if (Vector3.Dot(n, TowardViewer) <= 0.02f) continue;
                var poly = new Vector2[4];
                var centre = Vector3.zero;
                for (int k = 0; k < 4; k++) { var w = BodyToScreenSpace(v[faces[f][k]], true); poly[k] = Project(w); centre += w; }
                items.Add(new Item { Kind = 0, Poly = poly, Fill = Shade(b.Col, n), Depth = Depth(centre / 4f) - 0.001f });
                if (b.Windows && f != 4 && f != 5) AddWindows(v, faces[f], n, centre / 4f);
            }
            if (b.Roof)
            {
                // a pyramid roof: four triangles to an apex
                var apex = c + new Vector3(0, h.y + Mathf.Min(b.S.x, b.S.z) * 0.55f, 0);
                int[][] tris = { new[] { 2, 3 }, new[] { 6, 2 }, new[] { 7, 6 }, new[] { 3, 7 } };
                foreach (var tr in tris)
                {
                    var a = v[tr[0]]; var bb = v[tr[1]];
                    var n = Vector3.Cross(bb - a, apex - a).normalized;
                    var wn = BodyToScreenSpace(n, false);
                    if (Mode == View.Side) wn = new Vector3(n.x * Facing, n.y, n.z);
                    if (Vector3.Dot(wn, TowardViewer) <= 0.02f) continue;
                    var wa = BodyToScreenSpace(a, true); var wb = BodyToScreenSpace(bb, true); var wp = BodyToScreenSpace(apex, true);
                    items.Add(new Item { Kind = 0, Poly = new[] { Project(wa), Project(wb), Project(wp) }, Fill = Shade(b.RoofCol, wn), Depth = Depth((wa + wb + wp) / 3f) + 0.002f });
                }
            }
        }

        /// <summary>A box whose four vertical edges are cut back by <paramref name="chamfer"/>, turning its horizontal
        /// cross-section from a rectangle into an octagon — a rounded hull edge instead of a sharp right angle,
        /// still built entirely from flat faces (one per octagon edge, plus the flat octagon top).</summary>
        private void AddChamferedPrism(Vector3 center, float hw, float hd, float y0, float y1, float chamfer, Color col)
        {
            Vector2[] pts =
            {
                new Vector2(-hw + chamfer, -hd), new Vector2(hw - chamfer, -hd),
                new Vector2(hw, -hd + chamfer), new Vector2(hw, hd - chamfer),
                new Vector2(hw - chamfer, hd), new Vector2(-hw + chamfer, hd),
                new Vector2(-hw, hd - chamfer), new Vector2(-hw, -hd + chamfer),
            };
            int n = pts.Length;
            var topWorld = new Vector3[n];
            var topScreen = new Vector2[n];
            var topCentre = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var local = new Vector3(center.x + pts[i].x, y1, center.z + pts[i].y);
                topWorld[i] = BodyToScreenSpace(local, true);
                topCentre += topWorld[i];
            }
            // The top face is always visible from this camera (both views look down at the deck), same as AddBox's
            // top/bottom faces: no culling test needed, just draw it.
            var topNormal = Mode == View.Side ? Vector3.up : BodyToScreenSpace(Vector3.up, false);
            for (int i = 0; i < n; i++) topScreen[i] = Project(topWorld[i]);
            items.Add(new Item { Kind = 0, Poly = topScreen, Fill = Shade(col, topNormal), Depth = Depth(topCentre / n) - 0.001f });
            for (int i = 0; i < n; i++)
            {
                var a2 = pts[i]; var b2 = pts[(i + 1) % n];
                var edge = b2 - a2;
                var normal2 = new Vector2(edge.y, -edge.x).normalized;
                var n3Local = new Vector3(normal2.x, 0, normal2.y);
                var n3 = Mode == View.Side ? n3Local : BodyToScreenSpace(n3Local, false);
                if (Vector3.Dot(n3, TowardViewer) <= 0.02f) continue;
                var wa0 = BodyToScreenSpace(new Vector3(center.x + a2.x, y0, center.z + a2.y), true);
                var wb0 = BodyToScreenSpace(new Vector3(center.x + b2.x, y0, center.z + b2.y), true);
                var wa1 = BodyToScreenSpace(new Vector3(center.x + a2.x, y1, center.z + a2.y), true);
                var wb1 = BodyToScreenSpace(new Vector3(center.x + b2.x, y1, center.z + b2.y), true);
                var poly = new[] { Project(wa0), Project(wb0), Project(wb1), Project(wa1) };
                var centre = (wa0 + wb0 + wb1 + wa1) / 4f;
                items.Add(new Item { Kind = 0, Poly = poly, Fill = Shade(col, n3), Depth = Depth(centre) - 0.001f });
            }
        }

        private void AddWindows(Vector3[] v, int[] face, Vector3 n, Vector3 centre)
        {
            // two lit windows per face, inset on the face plane
            var a = v[face[0]]; var b = v[face[1]]; var d = v[face[3]];
            for (int w = 0; w < 2; w++)
            {
                float u0 = 0.2f + w * 0.42f, u1 = u0 + 0.18f;
                Vector3 P(float u, float vv) => a + (b - a) * u + (d - a) * vv;
                var q = new[] { P(u0, 0.35f), P(u1, 0.35f), P(u1, 0.65f), P(u0, 0.65f) };
                var poly = new Vector2[4];
                for (int k = 0; k < 4; k++) poly[k] = Project(BodyToScreenSpace(q[k], true));
                items.Add(new Item { Kind = 0, Poly = poly, Fill = new Color(1f, 0.82f, 0.42f), Depth = Depth(centre) + 0.0005f });
            }
        }

        private void DrawLeg(Painter2D p2, int i)
        {
            var hip = Hips[i];
            var foot = feet[i]; // the ground contact the gait function plants — always the lowest point of the leg
            // Two-bone IK in the leg plane, reaching all the way to the ground contact (not stopping short at some
            // intermediate ankle), so the leg always fully extends — heavy and stompy, not perpetually half-crouched.
            // Both front and rear knees kink the same way — rearward and up — the actual silhouette of a real
            // quadruped's leg (a dog's "elbow" and a horse's hock both point to the back), not a mirrored front/back bend.
            var hint = new Vector3(-1f, 0.55f, 0f);
            var wHip = BodyToScreenSpace(hip, true);
            var wFoot = BodyToScreenSpace(foot, false);
            var hintW = BodyToScreenSpace(hip + hint, true) - wHip;
            var knee = Ik(wHip, wFoot, UpperLeg, LowerLeg, hintW);
            float near = Mode == View.Side ? (hip.z < 0 ? 1f : 0.62f) : 1f;
            var dark = new Color(0.07f, 0.06f, 0.06f);
            var iron = new Color(0.22f * near + 0.05f, 0.22f * near + 0.05f, 0.23f * near + 0.06f);
            var steel = new Color(0.34f * near + 0.05f, 0.33f * near + 0.05f, 0.34f * near + 0.06f);
            var brass = new Color(0.64f * near, 0.50f * near, 0.26f * near);
            var brassDark = new Color(0.40f * near, 0.31f * near, 0.15f * near);
            var a = Project(wHip); var k = Project(knee); var f = Project(wFoot);
            float w = PixelsPerUnit;
            p2.lineCap = LineCap.Round; p2.lineJoin = LineJoin.Round;

            // the side the knee kinks toward — the actuator and brackets live on that side, like the exposed hydraulic
            // rod on a real mechanical leg.
            var hintScreen = Project(wHip + hintW) - a;
            Vector2 Outward(Vector2 d)
            {
                var pr = new Vector2(-d.y, d.x);
                if (pr.sqrMagnitude < 1e-6f) return Vector2.up;
                pr.Normalize();
                return Vector2.Dot(pr, hintScreen) < 0f ? -pr : pr;
            }
            var outUpper = Outward(k - a);
            var outLower = Outward(f - k);

            // The thigh and shin are built from straight-edged armor plates bolted together at a seam, each panel
            // tapering toward the next joint — the silhouette of a real limb (thick at the root, narrowing toward the
            // joint below it), fabricated as hardware rather than cast as one smooth, soft tube.
            PlatedLimb(p2, a, k, 1.05f * w, 0.72f * w, 0.5f * w, dark, steel, brassDark);
            PlatedLimb(p2, k, f, 0.68f * w, 0.46f * w, 0.32f * w, dark, steel, brassDark);
            for (int r = 1; r <= 2; r++) Circle(p2, Vector2.Lerp(a, k, r / 3f), 0.16f * w, brassDark);
            for (int r = 1; r <= 2; r++) Circle(p2, Vector2.Lerp(k, f, r / 3f), 0.14f * w, brassDark);

            // One hydraulic ram actually does the knee's work: it's bolted to a bracket on the thigh and another on the
            // shin, bridging the joint on its outward side. Its span is the real hip-to-knee-to-ankle distance between
            // those two brackets, so it only ever reads as "working" when the knee angle it is driving changes — the
            // cylinder sleeve is a fixed length and the rod telescopes in and out of it to cover the rest.
            var thighBracket = Vector2.Lerp(a, k, 0.3f) + outUpper * 0.4f * w;
            var shinBracket = Vector2.Lerp(k, f, 0.4f) + outLower * 0.4f * w;
            Actuator(p2, thighBracket, shinBracket, w, brass, brassDark, dark, 1.65f * w);

            // Cog joints geared straight off the limb's own angles — not an independent spin — so they read as the
            // thing actually driving the leg: the hip cog turns with the thigh's swing angle, the knee cog (meshed to
            // it at a different ratio, hence the opposite turn direction) with the knee's own bend angle. A leg that
            // isn't moving leaves its gears exactly where the pose puts them.
            float hipAngle = Mathf.Atan2((k - a).y, (k - a).x);
            float kneeBendDeg = Mathf.DeltaAngle(hipAngle * Mathf.Rad2Deg, Mathf.Atan2((f - k).y, (f - k).x) * Mathf.Rad2Deg);
            Gear(p2, a, 1.3f * w, 0.6f * w, 10, hipAngle * 3.2f, iron, brass, dark);
            Gear(p2, k, 0.95f * w, 0.42f * w, 8, kneeBendDeg * Mathf.Deg2Rad * -4.5f, iron, brass, dark);

            // The hoof is the bottom of the leg itself — the last stretch of the exact same k→f line the shin is
            // already drawn on, just thicker and a shade darker, so it reads as the heavy part that actually bears the
            // weight. Built this way it can never detach, twist or taper to a point: it has no direction of its own to
            // get wrong, only a point along a line segment that was already correct.
            // Flat-edged, flaring slightly toward the sole like a real hoof capsule — plated to match the limb above it
            // rather than a rounded cap, so the whole leg reads as one fabricated assembly.
            // A heavy stomping pad, not a tapered hoof: it overshoots the ankle pivot (padTip, past f) instead of
            // tapering to a point at it, and flares wider at the tip than at the ankle — the blunt, heavy foot a
            // town this size should actually plant, not a horse's hoof. Its flat side (hn) always lies along the
            // ground plane — Vector2.right, never a direction derived from the shin — so the pad rests flush on
            // the ground regardless of how steeply the leg above it happens to be leaning, the way a real ankle
            // joint articulates to keep the sole flat instead of planting at the shin's own angle.
            var ankleJoint = Vector2.Lerp(k, f, 0.68f);
            var hoofDir = f - ankleJoint;
            float hoofLen = hoofDir.magnitude;
            var hu = hoofLen > 1e-4f ? hoofDir / hoofLen : Vector2.down;
            var hn = Vector2.right;
            var padTip = f + hu * (0.55f * w);
            float rNear = 0.66f * w, rFar = 1.05f * w;
            Vector2[] HoofQuad(float pad) => new[] { ankleJoint + hn * (rNear + pad), padTip + hn * (rFar + pad), padTip - hn * (rFar + pad), ankleJoint - hn * (rNear + pad) };
            Poly(p2, HoofQuad(0.75f), dark);
            Poly(p2, HoofQuad(0f), iron);
            Circle(p2, ankleJoint, 0.34f * w, brassDark); // the joint the pad flexes on, where it meets the shin
            Circle(p2, ankleJoint, 0.19f * w, brass);
            // Flat at rest — the same ground-plane baseline (Vector2.right) as the pad above — and only tilts
            // during swing, breaking over toward the shin as it clears the ground, never below flat.
            var solePerp = Vector2.right;
            if (Vector2.Dot(solePerp, hintScreen) < 0f) solePerp = -solePerp;
            float tilt = footAngle[i] * Mathf.Deg2Rad;
            float c = Mathf.Cos(tilt), s = Mathf.Sin(tilt);
            var sole = new Vector2(solePerp.x * c - solePerp.y * s, solePerp.x * s + solePerp.y * c);
            Line(p2, padTip + sole * 0.9f * w, padTip - sole * 0.9f * w, dark, 0.9f * w);
        }

        private static Vector3 Ik(Vector3 hip, Vector3 foot, float a, float b, Vector3 hint)
        {
            var d = foot - hip;
            float len = Mathf.Clamp(d.magnitude, 0.01f, a + b - 0.01f);
            var u = d.normalized;
            var perp = (hint - u * Vector3.Dot(hint, u)).normalized;
            float x = (a * a - b * b + len * len) / (2f * len);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            return hip + u * x + perp * h;
        }

        private static void Poly(Painter2D p, Vector2[] pts, Color c)
        {
            p.fillColor = c;
            p.BeginPath(); p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath(); p.Fill();
            p.strokeColor = new Color(0.08f, 0.07f, 0.08f, 0.85f); p.lineWidth = 1.6f;
            p.Stroke();
        }

        private static void Line(Painter2D p, Vector2 a, Vector2 b, Color c, float w)
        {
            p.strokeColor = c; p.lineWidth = w;
            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
        }

        private static void Circle(Painter2D p, Vector2 c, float r, Color col)
        {
            p.fillColor = col;
            p.BeginPath(); p.Arc(c, r, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath(); p.Fill();
        }

        /// <summary>Two flat, straight-edged armor plates bolted in sequence from `a` (radius `rA`) through a seam at
        /// `rMid` to `b` (radius `rB`) — built hardware with a visible joint between its own parts, not a smooth
        /// capsule with a soft, organic taper.</summary>
        private static void PlatedLimb(Painter2D p, Vector2 a, Vector2 b, float rA, float rMid, float rB, Color outline, Color fill, Color seam)
        {
            var full = b - a;
            if (full.sqrMagnitude < 1e-6f) return;
            var mid = Vector2.Lerp(a, b, 0.46f);
            void Panel(Vector2 p0, Vector2 p1, float r0, float r1)
            {
                var dir = p1 - p0;
                float len = dir.magnitude;
                if (len < 1e-4f) return;
                var u = dir / len;
                var n = new Vector2(-u.y, u.x);
                Vector2[] Quad(float pad) => new[] { p0 + n * (r0 + pad), p1 + n * (r1 + pad), p1 - n * (r1 + pad), p0 - n * (r0 + pad) };
                Poly(p, Quad(Mathf.Max(r0, r1) * 0.15f + 0.6f), outline);
                Poly(p, Quad(0f), fill);
            }
            Panel(a, mid, rA, rMid);
            Panel(mid, b, rMid, rB);
            // the seam where the two plates bolt together, with a rivet on each side
            var fn = new Vector2(-full.y, full.x).normalized;
            Line(p, mid + fn * (rMid + 0.3f), mid - fn * (rMid + 0.3f), seam, Mathf.Max(0.8f, rMid * 0.16f));
            Circle(p, mid + fn * rMid * 0.55f, rMid * 0.14f, seam);
            Circle(p, mid - fn * rMid * 0.55f, rMid * 0.14f, seam);
        }

        /// <summary>A toothed cog: rim with teeth, a bolted hub, and a centre pin. Spins with `rot`.</summary>
        private static void Gear(Painter2D p, Vector2 c, float outerR, float innerR, int teeth, float rot, Color rim, Color face, Color dark)
        {
            p.fillColor = rim;
            p.BeginPath();
            int n = teeth * 2;
            for (int i = 0; i <= n; i++)
            {
                float ang = rot + i / (float)n * Mathf.PI * 2f;
                float r = (i % 2 == 0) ? outerR : outerR * 0.76f;
                var q = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (i == 0) p.MoveTo(q); else p.LineTo(q);
            }
            p.ClosePath(); p.Fill();
            Circle(p, c, innerR, face);
            p.strokeColor = dark; p.lineWidth = Mathf.Max(0.6f, innerR * 0.16f);
            p.BeginPath(); p.Arc(c, innerR * 0.9f, Angle.Degrees(0), Angle.Degrees(360)); p.Stroke();
            const int bolts = 5;
            for (int b = 0; b < bolts; b++)
            {
                float ang = rot * 0.4f + b / (float)bolts * Mathf.PI * 2f;
                Circle(p, c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * innerR * 0.6f, innerR * 0.16f, dark);
            }
            Circle(p, c, innerR * 0.22f, dark);
        }

        /// <summary>A telescoping hydraulic ram between two pivots that actually move: the cylinder sleeve holds a fixed
        /// length (`cylLen`, clamped so it never exceeds the current span) and the rod makes up whatever span is left,
        /// so it visibly extends when the joint it crosses opens and retracts when the joint closes.</summary>
        private static void Actuator(Painter2D p, Vector2 from, Vector2 to, float w, Color cyl, Color rod, Color dark, float cylLen)
        {
            var d = to - from;
            float len = d.magnitude;
            if (len < 0.01f) return;
            float sleeve = Mathf.Min(cylLen, len * 0.85f);
            var cylEnd = from + d / len * sleeve;
            Line(p, from, cylEnd, dark, 0.62f * w);
            Line(p, from, cylEnd, cyl, 0.44f * w);
            Circle(p, Vector2.Lerp(from, cylEnd, 0.5f), 0.26f * w, dark);
            Line(p, cylEnd, to, dark, 0.34f * w);
            Line(p, cylEnd, to, rod, 0.2f * w);
            Circle(p, from, 0.34f * w, dark); Circle(p, from, 0.22f * w, cyl);
            Circle(p, to, 0.26f * w, dark); Circle(p, to, 0.16f * w, rod);
        }
    }
}
