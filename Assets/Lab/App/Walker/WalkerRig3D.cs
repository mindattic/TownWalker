using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lab.App
{
    /// <summary>
    /// The walking town in 3D, built in code from primitive meshes (no external assets): a brass platform and keel,
    /// a ring of stone houses with pyramid roofs, a keep, chimneys and a radar mast — on four legs with two-bone IK.
    /// Feet stay planted on the terrain and step (diagonal pairs) when they lag too far behind their rest spot; the body
    /// rides on its feet (height and tilt follow the ground). The owner's mood board is the style reference.
    /// </summary>
    public sealed class WalkerRig3D : MonoBehaviour
    {
        public Func<float, float, float> GroundAt = (x, z) => 0f;
        public Vector3 Velocity;

        private static readonly Vector3[] HipLocal = { new Vector3(3.6f, -0.6f, 3.2f), new Vector3(3.6f, -0.6f, -3.2f), new Vector3(-3.6f, -0.6f, 3.2f), new Vector3(-3.6f, -0.6f, -3.2f) };
        private const float UpperLeg = 4.9f, LowerLeg = 4.9f, RideHeight = 7.4f, StepDistance = 2.4f, StepTime = 0.5f, Lift = 1.6f;
        private readonly Vector3[] foot = new Vector3[4];
        private readonly Vector3[] stepFrom = new Vector3[4], stepTo = new Vector3[4];
        private readonly float[] stepT = { -1, -1, -1, -1 };
        private int pairToMove;
        private Transform body;
        private readonly Transform[] upper = new Transform[4], lower = new Transform[4], knee = new Transform[4], pad = new Transform[4];
        private readonly List<(Transform t, float age)> smoke = new List<(Transform, float)>();
        private float smokeTimer;
        private static Material baseMat, smokeMat;
        private static readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
        private readonly List<Mesh> roofMeshes = new List<Mesh>();

        /// <summary>Flat-lit materials per colour (Lab/FlatLit lives in Resources, so builds keep it).</summary>
        private static Material Mat(Color c)
        {
            if (mats.TryGetValue(c, out var m)) return m;
            baseMat ??= new Material(Shader.Find("Lab/FlatLit"));
            m = new Material(baseMat) { color = c };
            return mats[c] = m;
        }

        public static WalkerRig3D Create(Transform parent)
        {
            var go = new GameObject("Walker3D");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<WalkerRig3D>();
            w.Build();
            return w;
        }

        /// <summary>Unity's built-in primitive meshes, without CreatePrimitive's collider (physics is stripped from WebGL builds).</summary>
        private static Mesh MeshOf(PrimitiveType t) => Resources.GetBuiltinResource<Mesh>(t switch
        {
            PrimitiveType.Cube => "Cube.fbx",
            PrimitiveType.Cylinder => "Cylinder.fbx",
            PrimitiveType.Sphere => "Sphere.fbx",
            _ => "Cube.fbx",
        });

        private static GameObject Primitive(PrimitiveType t, Material m, string name)
        {
            var g = new GameObject(name);
            g.AddComponent<MeshFilter>().sharedMesh = MeshOf(t);
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        private GameObject Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Color c, string name)
        {
            var g = Primitive(t, Mat(c), name);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            return g;
        }

        private void Roof(Transform parent, Vector3 pos, Vector3 size, Color c)
        {
            var g = new GameObject("roof");
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            var mf = g.AddComponent<MeshFilter>();
            g.AddComponent<MeshRenderer>().sharedMaterial = Mat(c);
            float hx = size.x / 2, hz = size.z / 2, h = Mathf.Min(size.x, size.z) * 0.6f;
            var apex = new Vector3(0, h, 0);
            Vector3[] b = { new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz) };
            var v = new List<Vector3>(); var tri = new List<int>(); var col = new List<Color>();
            for (int i = 0; i < 4; i++) { int s = v.Count; v.Add(b[i]); v.Add(apex); v.Add(b[(i + 1) % 4]); tri.Add(s); tri.Add(s + 1); tri.Add(s + 2); col.Add(Color.white); col.Add(Color.white); col.Add(Color.white); }
            var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.SetColors(col); mesh.RecalculateNormals();
            mf.sharedMesh = mesh;
            roofMeshes.Add(mesh);
        }

        private void Build()
        {
            var stone = new Color(0.66f, 0.62f, 0.55f); var brass = new Color(0.58f, 0.45f, 0.28f); var iron = new Color(0.32f, 0.31f, 0.33f);
            var red = new Color(0.62f, 0.27f, 0.20f); var slate = new Color(0.32f, 0.37f, 0.48f); var steel = new Color(0.36f, 0.35f, 0.37f);
            body = new GameObject("body").transform;
            body.SetParent(transform, false);
            Part(PrimitiveType.Cube, body, new Vector3(0, -1.0f, 0), new Vector3(7.4f, 1.4f, 5.6f), iron, "keel");
            Part(PrimitiveType.Cube, body, new Vector3(0, 0.5f, 0), new Vector3(10.6f, 1.8f, 8.0f), brass, "platform");
            Part(PrimitiveType.Cube, body, new Vector3(0, 1.6f, 0), new Vector3(10.2f, 0.4f, 7.6f), stone, "deck");
            (Vector3 p, Vector3 s, Color r)[] houses =
            {
                (new Vector3(3.6f, 3.0f, 2.6f), new Vector3(2.4f, 2.4f, 2.2f), red), (new Vector3(3.8f, 2.6f, -2.4f), new Vector3(2.2f, 1.8f, 2.4f), slate),
                (new Vector3(1.0f, 3.2f, 3.0f), new Vector3(2.6f, 2.8f, 1.8f), slate), (new Vector3(-1.6f, 2.8f, 3.0f), new Vector3(2.4f, 2.0f, 1.8f), red),
                (new Vector3(-4.0f, 3.0f, 2.4f), new Vector3(2.2f, 2.4f, 2.6f), red), (new Vector3(-4.0f, 2.8f, -2.4f), new Vector3(2.2f, 2.0f, 2.6f), slate),
                (new Vector3(-1.2f, 2.9f, -3.0f), new Vector3(3.0f, 2.2f, 1.6f), red), (new Vector3(1.4f, 2.7f, -3.0f), new Vector3(2.0f, 1.8f, 1.6f), slate),
            };
            foreach (var (p, s, r) in houses)
            {
                Part(PrimitiveType.Cube, body, p, s, stone, "house");
                Roof(body, p + new Vector3(0, s.y / 2, 0), s, r);
            }
            Part(PrimitiveType.Cube, body, new Vector3(0, 4.8f, 0), new Vector3(4.4f, 6.0f, 4.2f), stone, "keep");
            Part(PrimitiveType.Cube, body, new Vector3(0.2f, 8.6f, 0.2f), new Vector3(2.4f, 2.4f, 2.4f), stone, "tower");
            Roof(body, new Vector3(0.2f, 9.8f, 0.2f), new Vector3(2.4f, 2.4f, 2.4f), slate);
            Part(PrimitiveType.Cylinder, body, new Vector3(-3.6f, 6.0f, 1.6f), new Vector3(0.8f, 2.2f, 0.8f), iron, "chimney");
            Part(PrimitiveType.Cylinder, body, new Vector3(-2.4f, 5.4f, -2.2f), new Vector3(0.7f, 1.8f, 0.7f), iron, "chimney");
            Part(PrimitiveType.Cylinder, body, new Vector3(4.8f, 4.6f, 0f), new Vector3(0.3f, 1.8f, 0.3f), iron, "mast");
            Part(PrimitiveType.Cylinder, body, new Vector3(4.8f, 6.6f, 0f), new Vector3(1.6f, 0.08f, 1.6f), new Color(0.3f, 0.75f, 0.62f), "radar");
            for (int i = 0; i < 4; i++)
            {
                upper[i] = Part(PrimitiveType.Cylinder, transform, Vector3.zero, new Vector3(1.2f, UpperLeg / 2f, 1.2f), steel, "upper" + i).transform;
                lower[i] = Part(PrimitiveType.Cylinder, transform, Vector3.zero, new Vector3(0.95f, LowerLeg / 2f, 0.95f), steel, "lower" + i).transform;
                knee[i] = Part(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.one * 1.5f, brass, "knee" + i).transform;
                pad[i] = Part(PrimitiveType.Cylinder, transform, Vector3.zero, new Vector3(1.8f, 0.25f, 1.8f), iron, "foot" + i).transform;
            }
        }

        /// <summary>Puts the walker down at a world position with every foot planted.</summary>
        public void Place(Vector3 pos, float yaw)
        {
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < 4; i++) { foot[i] = RestSpot(i); stepT[i] = -1; }
        }

        private Vector3 RestSpot(int i)
        {
            var w = transform.TransformPoint(new Vector3(HipLocal[i].x, 0, HipLocal[i].z * 1.18f));
            return new Vector3(w.x, GroundAt(w.x, w.z), w.z);
        }

        public float[][] FootSnapshot()
        {
            var r = new float[4][];
            for (int i = 0; i < 4; i++) r[i] = new[] { Mathf.Round(foot[i].x * 10) / 10, Mathf.Round(foot[i].y * 10) / 10, Mathf.Round(foot[i].z * 10) / 10 };
            return r;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // Steps: the lagging diagonal pair steps toward its rest spot, led a little by the velocity.
            int[][] pairs = { new[] { 0, 3 }, new[] { 1, 2 } };
            bool anyStepping = false;
            for (int i = 0; i < 4; i++) if (stepT[i] >= 0) anyStepping = true;
            if (!anyStepping)
            {
                var pr = pairs[pairToMove];
                float lag = Mathf.Max(Vector3.Distance(foot[pr[0]], RestSpot(pr[0])), Vector3.Distance(foot[pr[1]], RestSpot(pr[1])));
                if (lag > StepDistance || (Velocity.sqrMagnitude < 0.01f && lag > 0.4f))
                {
                    foreach (var i in pr) { stepFrom[i] = foot[i]; stepT[i] = 0f; }
                    pairToMove = 1 - pairToMove;
                }
            }
            for (int i = 0; i < 4; i++)
            {
                if (stepT[i] < 0) continue;
                stepT[i] += dt / StepTime;
                var target = RestSpot(i) + Velocity * (StepTime * 0.6f);
                target.y = GroundAt(target.x, target.z);
                float k = Mathf.Clamp01(stepT[i]);
                var p = Vector3.Lerp(stepFrom[i], target, Mathf.SmoothStep(0, 1, k));
                p.y += Mathf.Sin(k * Mathf.PI) * Lift;
                foot[i] = p;
                if (stepT[i] >= 1f) { foot[i] = target; stepT[i] = -1f; }
            }
            // The body rides on its feet: height from the average foot, pitch/roll from the slope under them.
            float avg = (foot[0].y + foot[1].y + foot[2].y + foot[3].y) / 4f;
            float front = (foot[0].y + foot[1].y) / 2f, back = (foot[2].y + foot[3].y) / 2f;
            float left = (foot[0].y + foot[2].y) / 2f, right = (foot[1].y + foot[3].y) / 2f;
            float bob = Velocity.sqrMagnitude > 0.01f ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.6f)) * 0.3f : Mathf.Sin(Time.unscaledTime * 1.3f) * 0.05f;
            var pos = transform.position;
            transform.position = new Vector3(pos.x, Mathf.Lerp(pos.y, avg + RideHeight + bob, 1f - Mathf.Exp(-dt * 6f)), pos.z);
            body.localRotation = Quaternion.Euler(Mathf.Atan2(left - right, 6.4f) * Mathf.Rad2Deg, 0, Mathf.Atan2(front - back, 7.2f) * -Mathf.Rad2Deg);
            for (int i = 0; i < 4; i++) PoseLeg(i);
            Smoke(dt);
        }

        private void PoseLeg(int i)
        {
            var hip = body.TransformPoint(HipLocal[i]);
            var f = foot[i];
            // All four knees kink the same way — rearward and up — like a real quadruped's legs, not mirrored
            // front-to-back (Mathf.Sign(HipLocal[i].x) was bending the front legs forward instead).
            var outward = transform.TransformDirection(new Vector3(-1f, 0.6f, 0f));
            var k = Ik(hip, f, UpperLeg, LowerLeg, outward);
            Segment(upper[i], hip, k);
            Segment(lower[i], k, f);
            knee[i].position = k;
            pad[i].position = f + Vector3.up * 0.12f;
            pad[i].rotation = transform.rotation;
        }

        private static void Segment(Transform cyl, Vector3 a, Vector3 b)
        {
            cyl.position = (a + b) / 2f;
            cyl.up = (b - a).normalized;
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

        private void Smoke(float dt)
        {
            smokeTimer -= dt;
            if (smokeTimer <= 0f)
            {
                smokeTimer = 0.35f;
                smokeMat ??= Mat(new Color(0.6f, 0.6f, 0.63f));
                foreach (var c in new[] { new Vector3(-3.6f, 8.4f, 1.6f), new Vector3(-2.4f, 7.4f, -2.2f) })
                {
                    var g = Primitive(PrimitiveType.Sphere, smokeMat, "smoke");
                    g.transform.position = body.TransformPoint(c);
                    g.transform.localScale = Vector3.one * 0.8f;
                    smoke.Add((g.transform, 0f));
                }
            }
            for (int i = smoke.Count - 1; i >= 0; i--)
            {
                var (t, age) = smoke[i];
                age += dt;
                if (age > 2.4f) { Destroy(t.gameObject); smoke.RemoveAt(i); continue; }
                t.position += new Vector3(-0.8f, 1.8f, 0.3f) * dt - Velocity * dt * 0.5f;
                t.localScale = Vector3.one * Mathf.Lerp(0.8f, 2.6f, age / 2.4f) * (1f - Mathf.Pow(age / 2.4f, 3f));
                smoke[i] = (t, age);
            }
        }

        private void OnDestroy()
        {
            foreach (var (t, _) in smoke) if (t != null) Destroy(t.gameObject);
            foreach (var m in roofMeshes) if (m != null) Destroy(m); // runtime meshes aren't freed with their GameObject
        }
    }
}
