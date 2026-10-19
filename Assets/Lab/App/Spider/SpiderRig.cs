using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// Paints a <see cref="SpiderPhysics"/> town in the host demo's projection (world x, y, z-up → design point):
    /// a lobed, radially symmetric steampunk hull (iron keel, brass platform, stone deck) with a ring of houses and
    /// a keep crowned by the radar mast; each leg is an insect leg (a short coxa off the hull, a femur rising to a high knee, a tibia
    /// dropping to a flat pad), solved with two-bone IK with the knee pushed up, so it reads as /\.
    /// </summary>
    public sealed class SpiderRig : VisualElement
    {
        public SpiderPhysics Body;
        /// <summary>World point (x, y ground, z up) → design point.</summary>
        public Func<Vector3, Vector2> Project;
        /// <summary>Design pixels per world unit, for line widths and puff sizes.</summary>
        public float Px = 16f;

        private static readonly Color Stone = new Color(0.62f, 0.58f, 0.52f), Brass = new Color(0.58f, 0.45f, 0.27f),
            Iron = new Color(0.33f, 0.32f, 0.34f), RedRoof = new Color(0.62f, 0.27f, 0.20f), Slate = new Color(0.32f, 0.37f, 0.48f),
            Dark = new Color(0.07f, 0.06f, 0.06f), Window = new Color(1f, 0.82f, 0.42f);
        private static readonly Vector3 ToViewer = new Vector3(1f, 1f, 1.15f).normalized;
        private static readonly Vector3 Light = new Vector3(-0.45f, 0.25f, 0.86f).normalized;

        private struct Item { public int Layer; public float Depth; public Vector2[] Poly; public Color Fill; public int Leg; }
        private struct Puff { public Vector3 P; public float Age, Life, Size; }
        private readonly List<Item> items = new List<Item>();
        private readonly List<Puff> dust = new List<Puff>(), smoke = new List<Puff>();
        private float smokeTimer;
        private SpiderPhysics subscribed;

        public SpiderRig()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Tick(float dt)
        {
            if (subscribed != Body)
            {
                if (subscribed != null) subscribed.Footfall -= OnFootfall;
                subscribed = Body;
                if (Body != null) Body.Footfall += OnFootfall;
                dust.Clear(); smoke.Clear();
            }
            smokeTimer -= dt;
            if (smokeTimer <= 0f && Body != null)
            {
                smokeTimer = 0.3f;
                smoke.Add(new Puff { P = Body.P + Body.Rotation * new Vector3(-1.6f, 0.9f, 7.4f), Life = 2.4f, Size = 0.7f });
            }
            Age(dust, dt, new Vector3(0f, 0f, 0.5f));
            Age(smoke, dt, new Vector3(-0.8f, 0.4f, 1.6f));
            MarkDirtyRepaint();
        }

        private void OnFootfall(Vector3 at)
        {
            for (int d = 0; d < 3; d++)
                dust.Add(new Puff { P = at + new Vector3(UnityEngine.Random.Range(-0.7f, 0.7f), UnityEngine.Random.Range(-0.7f, 0.7f), 0.1f), Life = 0.8f, Size = 0.5f });
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

        /// <summary>Foot positions on screen (for automation: they must change between frames while walking).</summary>
        public float[][] FootSnapshot()
        {
            var r = new float[Body.N][];
            for (int i = 0; i < Body.N; i++) { var p = Project(Body.Legs[i].Foot); r[i] = new[] { Mathf.Round(p.x * 10f) / 10f, Mathf.Round(p.y * 10f) / 10f }; }
            return r;
        }

        private static float Depth(Vector3 w) => w.x + w.y + w.z * 0.02f;

        private Color Shade(Color c, Vector3 n) => Color.Lerp(c * 0.48f, c * 1.12f, Mathf.Clamp01(Vector3.Dot(n, Light) * 0.5f + 0.5f)) + new Color(0, 0, 0, 1);

        // ── Drawing ─────────────────────────────────────────────────────────────

        private void Draw(MeshGenerationContext ctx)
        {
            if (Body == null || Project == null) return;
            var p2 = ctx.painter2D;
            var b = Body;
            var R = b.Rotation;
            items.Clear();

            // ground shadow under the hull
            var sc = b.P; sc.z -= SpiderPhysics.RideHeight;
            var c0 = Project(sc);
            Ellipse(p2, c0, 7.5f * Px, 3.8f * Px, new Color(0f, 0f, 0f, 0.26f));

            Vector3 W(Vector3 local) => b.P + R * local;
            float centreDepth = Depth(b.P);

            // Hull: three lobed prisms stacked bottom-up, each its own layer so the deck always paints over the keel.
            int sides = b.N * 4;
            Prism(W, R, sides, 0.72f, -1.7f, -0.6f, Iron, 10);
            Prism(W, R, sides, 1.0f, -0.6f, 0.5f, Brass, 11);
            Prism(W, R, sides, 0.9f, 0.5f, 1.3f, Stone, 12);

            // Superstructure: a house between each pair of legs, the keep, a chimney and the bow mast.
            for (int i = 0; i < b.N; i++)
            {
                float a = Mathf.PI * 2f * i / b.N;
                var c = new Vector3(Mathf.Cos(a) * 2.7f, Mathf.Sin(a) * 2.7f, 2.2f);
                Box(W, R, c, new Vector3(1.9f, 1.9f, 1.8f), a, Stone, i % 2 == 0 ? RedRoof : Slate, true, true);
            }
            Box(W, R, new Vector3(0f, 0f, 3.2f), new Vector3(2.6f, 2.6f, 3.8f), Mathf.PI / 4f, Stone, Slate, true, true);
            Box(W, R, new Vector3(-1.6f, 0.9f, 5.2f), new Vector3(0.5f, 0.5f, 4.4f), 0f, Iron, Iron, false, false);
            // the radar mast crowns the keep: the hull has no bow, it walks any direction
            Box(W, R, new Vector3(0f, 0f, 7.6f), new Vector3(0.25f, 0.25f, 2.4f), 0f, Iron, Iron, false, false);
            Box(W, R, new Vector3(0f, 0f, 8.9f), new Vector3(1.4f, 1.4f, 0.25f), 0f, new Color(0.3f, 0.75f, 0.62f), Iron, false, false);

            for (int i = 0; i < b.N; i++)
            {
                var mid = (b.HipWorld(i) + b.Legs[i].Foot) * 0.5f;
                items.Add(new Item { Layer = Depth(mid) < centreDepth ? 0 : 30, Depth = Depth(mid), Leg = i });
            }
            items.Sort((x, y) => x.Layer != y.Layer ? x.Layer.CompareTo(y.Layer) : x.Depth.CompareTo(y.Depth));
            foreach (var it in items)
            {
                if (it.Poly == null) DrawLeg(p2, it.Leg);
                else Poly(p2, it.Poly, it.Fill);
            }
            foreach (var d in dust) Circle(p2, Project(d.P), d.Size * Px, new Color(0.78f, 0.7f, 0.58f, 0.5f * (1f - d.Age / d.Life)));
            foreach (var s in smoke) Circle(p2, Project(s.P), s.Size * Px, new Color(0.55f, 0.55f, 0.58f, 0.5f * (1f - s.Age / s.Life)));
        }

        /// <summary>Hull outline: lobes bulge out between the legs so the hips sit in the waist.</summary>
        private Vector2 HullPoint(int k, int sides, float scale)
        {
            float a = Mathf.PI * 2f * k / sides;
            float r = (SpiderPhysics.BodyRadius + 0.5f + 0.7f * Mathf.Cos(Body.N * a)) * scale;
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        private void Prism(Func<Vector3, Vector3> W, Quaternion R, int sides, float scale, float z0, float z1, Color col, int layer)
        {
            var top = new Vector2[sides];
            var topW = Vector3.zero;
            for (int k = 0; k < sides; k++)
            {
                var q = HullPoint(k, sides, scale);
                var w = W(new Vector3(q.x, q.y, z1));
                top[k] = Project(w);
                topW += w;
            }
            for (int k = 0; k < sides; k++)
            {
                var a = HullPoint(k, sides, scale); var bb = HullPoint((k + 1) % sides, sides, scale);
                var e = bb - a;
                var n = R * new Vector3(e.y, -e.x, 0f).normalized;
                if (Vector3.Dot(n, ToViewer) <= 0.02f) continue;
                var w0 = W(new Vector3(a.x, a.y, z0)); var w1 = W(new Vector3(bb.x, bb.y, z0));
                var w2 = W(new Vector3(bb.x, bb.y, z1)); var w3 = W(new Vector3(a.x, a.y, z1));
                items.Add(new Item { Layer = layer, Depth = Depth((w0 + w1 + w2 + w3) / 4f), Poly = new[] { Project(w0), Project(w1), Project(w2), Project(w3) }, Fill = Shade(col, n) });
            }
            items.Add(new Item { Layer = layer, Depth = float.MaxValue, Poly = top, Fill = Shade(col, R * Vector3.forward) });
        }

        private void Box(Func<Vector3, Vector3> W, Quaternion R, Vector3 c, Vector3 size, float yaw, Color col, Color roof, bool hasRoof, bool windows)
        {
            var h = size * 0.5f;
            float cy = Mathf.Cos(yaw), sy = Mathf.Sin(yaw);
            Vector3 L(float x, float y, float z) => c + new Vector3(x * cy - y * sy, x * sy + y * cy, z);
            var v = new[]
            {
                L(-h.x, -h.y, -h.z), L(h.x, -h.y, -h.z), L(h.x, h.y, -h.z), L(-h.x, h.y, -h.z),
                L(-h.x, -h.y, h.z), L(h.x, -h.y, h.z), L(h.x, h.y, h.z), L(-h.x, h.y, h.z),
            };
            int[][] faces = { new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 }, new[] { 4, 5, 6, 7 } };
            const int layer = 20;
            for (int f = 0; f < faces.Length; f++)
            {
                var fc = faces[f];
                var n = R * Vector3.Cross(v[fc[1]] - v[fc[0]], v[fc[3]] - v[fc[0]]).normalized;
                if (Vector3.Dot(n, ToViewer) <= 0.02f) continue;
                var poly = new Vector2[4];
                var centre = Vector3.zero;
                for (int k = 0; k < 4; k++) { var w = W(v[fc[k]]); poly[k] = Project(w); centre += w; }
                items.Add(new Item { Layer = layer, Depth = Depth(centre / 4f), Poly = poly, Fill = Shade(col, n) });
                if (windows && f < 4)
                {
                    var a = v[fc[0]]; var bb = v[fc[1]]; var d = v[fc[3]];
                    for (int wi = 0; wi < 2; wi++)
                    {
                        float u0 = 0.2f + wi * 0.42f, u1 = u0 + 0.18f;
                        Vector3 P(float u, float vv) => a + (bb - a) * u + (d - a) * vv;
                        var q = new[] { P(u0, 0.35f), P(u1, 0.35f), P(u1, 0.65f), P(u0, 0.65f) };
                        var wp = new Vector2[4];
                        for (int k = 0; k < 4; k++) wp[k] = Project(W(q[k]));
                        items.Add(new Item { Layer = layer, Depth = Depth(centre / 4f) + 0.001f, Poly = wp, Fill = Window });
                    }
                }
            }
            if (!hasRoof) return;
            var apex = c + new Vector3(0f, 0f, h.z + Mathf.Min(size.x, size.y) * 0.55f);
            int[][] tris = { new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }, new[] { 7, 4 } };
            foreach (var t in tris)
            {
                var a = v[t[0]]; var bb = v[t[1]];
                var n = R * Vector3.Cross(bb - a, apex - a).normalized;
                if (Vector3.Dot(n, ToViewer) <= 0.02f) continue;
                var wa = W(a); var wb = W(bb); var wx = W(apex);
                items.Add(new Item { Layer = layer, Depth = Depth((wa + wb + wx) / 3f) + 0.002f, Poly = new[] { Project(wa), Project(wb), Project(wx) }, Fill = Shade(roof, n) });
            }
        }

        private void DrawLeg(Painter2D p2, int i)
        {
            var b = Body;
            var l = b.Legs[i];
            var hip = b.HipWorld(i);
            var foot = l.Foot;
            var flat = new Vector3(foot.x - hip.x, foot.y - hip.y, 0f);
            var outDir = flat.sqrMagnitude > 1e-4f ? flat.normalized : b.Rotation * l.HipLocal.normalized;
            var coxaEnd = hip + outDir * SpiderPhysics.Coxa + new Vector3(0f, 0f, 0.4f);
            // knee pushed up and slightly outward: the high-kneed /\ of an insect leg
            var knee = Ik(coxaEnd, foot + new Vector3(0f, 0f, 0.35f), SpiderPhysics.Femur, SpiderPhysics.Tibia, Vector3.forward + outDir * 0.25f);
            var ankle = foot + new Vector3(0f, 0f, 0.35f);
            float w = Px;
            var a = Project(hip); var cx = Project(coxaEnd); var k = Project(knee); var an = Project(ankle); var f = Project(foot);
            var steel = new Color(0.36f, 0.35f, 0.36f);
            var brass = new Color(0.66f, 0.51f, 0.27f);
            var brassDark = new Color(0.42f, 0.32f, 0.16f);

            Limb(p2, a, cx, 0.8f * w, 0.7f * w, brass);
            Limb(p2, cx, k, 0.75f * w, 0.55f * w, steel);
            Limb(p2, k, an, 0.52f * w, 0.3f * w, steel);
            // a hydraulic ram along the femur's underside, and rivet bands
            var femurDir = (k - cx).normalized;
            var under = new Vector2(femurDir.y, -femurDir.x);
            if (under.y < 0f) under = -under;
            Line(p2, Vector2.Lerp(cx, k, 0.15f) + under * 0.45f * w, Vector2.Lerp(cx, k, 0.8f) + under * 0.45f * w, Dark, 0.32f * w);
            Line(p2, Vector2.Lerp(cx, k, 0.15f) + under * 0.45f * w, Vector2.Lerp(cx, k, 0.5f) + under * 0.45f * w, brass, 0.2f * w);
            for (int r = 1; r <= 2; r++) Circle(p2, Vector2.Lerp(cx, k, r / 3f), 0.12f * w, brassDark);
            Circle(p2, Vector2.Lerp(k, an, 0.5f), 0.1f * w, brassDark);
            Joint(p2, a, 0.55f * w, brass, brassDark);
            Joint(p2, cx, 0.5f * w, brass, brassDark);
            Joint(p2, k, 0.48f * w, brass, brassDark);
            // flat pad on the ground plane, whatever the tibia's lean: an ellipse in the iso projection
            var padC = Project(foot);
            var load = l.Planted ? Mathf.Clamp01(l.Load / 6f) : 0f;
            Ellipse(p2, padC, 0.95f * w, 0.48f * w, Dark);
            Ellipse(p2, padC + new Vector2(0f, -0.06f * w), 0.8f * w, 0.38f * w, Color.Lerp(new Color(0.3f, 0.29f, 0.3f), new Color(0.42f, 0.34f, 0.2f), load));
            Line(p2, an, f, Dark, 0.3f * w);
        }

        private static Vector3 Ik(Vector3 root, Vector3 tip, float a, float b, Vector3 hint)
        {
            var d = tip - root;
            float len = Mathf.Clamp(d.magnitude, 0.01f, a + b - 0.01f);
            var u = d.normalized;
            var perp = (hint - u * Vector3.Dot(hint, u)).normalized;
            float x = (a * a - b * b + len * len) / (2f * len);
            float h = Mathf.Sqrt(Mathf.Max(0f, a * a - x * x));
            return root + u * x + perp * h;
        }

        private static void Limb(Painter2D p, Vector2 a, Vector2 b, float rA, float rB, Color fill)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            var n = new Vector2(-d.y, d.x).normalized;
            Poly(p, new[] { a + n * (rA + 1.4f), b + n * (rB + 1.4f), b - n * (rB + 1.4f), a - n * (rA + 1.4f) }, Dark);
            Poly(p, new[] { a + n * rA, b + n * rB, b - n * rB, a - n * rA }, fill);
            Line(p, a + n * rA * 0.45f, b + n * rB * 0.45f, fill * 1.25f + new Color(0, 0, 0, 1), Mathf.Max(1f, rB * 0.35f));
        }

        private static void Joint(Painter2D p, Vector2 c, float r, Color face, Color dark)
        {
            Circle(p, c, r + 1.4f, Dark);
            Circle(p, c, r, face);
            Circle(p, c, r * 0.4f, dark);
        }

        private static void Poly(Painter2D p, Vector2[] pts, Color c)
        {
            p.fillColor = c;
            p.BeginPath(); p.MoveTo(pts[0]);
            for (int i = 1; i < pts.Length; i++) p.LineTo(pts[i]);
            p.ClosePath(); p.Fill();
            p.strokeColor = new Color(0.08f, 0.07f, 0.08f, 0.85f); p.lineWidth = 1.4f;
            p.Stroke();
        }

        private static void Line(Painter2D p, Vector2 a, Vector2 b, Color c, float w)
        {
            p.strokeColor = c; p.lineWidth = w; p.lineCap = LineCap.Round;
            p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke();
        }

        private static void Circle(Painter2D p, Vector2 c, float r, Color col)
        {
            p.fillColor = col;
            p.BeginPath(); p.Arc(c, r, Angle.Degrees(0), Angle.Degrees(360)); p.ClosePath(); p.Fill();
        }

        private static void Ellipse(Painter2D p, Vector2 c, float rx, float ry, Color col)
        {
            p.fillColor = col;
            p.BeginPath();
            for (int i = 0; i <= 24; i++) { float a = i / 24f * Mathf.PI * 2f; var q = c + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry); if (i == 0) p.MoveTo(q); else p.LineTo(q); }
            p.ClosePath(); p.Fill();
        }
    }
}
