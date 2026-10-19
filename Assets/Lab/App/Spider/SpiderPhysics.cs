using System;
using UnityEngine;

namespace Lab.App
{
    /// <summary>
    /// The spider town as a rigid body carried on N spring-damper legs (world frame: x, y ground plane, z up).
    /// Planted legs push (never pull) along the vertical with a damped spring, so the hull sags, rocks and tips with
    /// whatever its feet are standing on; flat foot pads resist a little tilt, capped by each pad's load × half-width.
    /// The legs drive the hull horizontally toward the core's town position (plus a CoM "sway" offset), and that
    /// traction, applied at the feet below the centre of mass, pitches the hull on acceleration.
    ///
    /// The gait is a free gait with a static-stability check: a leg lifts only when it's furthest from its neutral
    /// foothold, no neighbour is in the air, and the remaining feet still support the CoM. If they don't, the hull
    /// first shifts its CoM over them, then lifts. With 3 legs the support is a single line, so it's a lurching
    /// gait (shift, lift fast, catch). With 4 it's a crawl (one leg at a time, CoM shifted into the support
    /// triangle). With 5 it's a wave gait that can swing two non-adjacent legs at once. With 6, three alternate
    /// legs swing while the other three stand: the insect's alternating tripod.
    ///
    /// The hull is radially symmetric and has no front: <see cref="Yaw"/> stays fixed and it walks any direction.
    /// </summary>
    public sealed class SpiderPhysics
    {
        public sealed class Leg
        {
            public Vector3 HipLocal, NeutralLocal;
            public bool Planted = true;
            public Vector3 Foot, SwingFrom, SwingTo;
            public float SwingT, SwingDur, SwingPeak, Load, PlantedFor;
        }

        public const float BodyRadius = 4.5f, Coxa = 1.2f, Femur = 7.2f, Tibia = 9f, RideHeight = 4.6f;
        private const float Mass = 1f, G = 9.8f, Inertia = 14f, K = 16f, C = 2.0f;
        private const float PadHalfWidth = 0.55f, PadK = 40f, PadC = 6f;
        private const float DriveK = 20f, DriveC = 7.5f, ComHeight = 0.7f;
        private const float MaxDev = 6.4f, Clearance = 1.7f, SubStep = 1f / 240f;

        public readonly int N;
        public readonly Leg[] Legs;
        public Vector3 P, V;
        public readonly float Yaw;
        /// <summary>Small rotations about world x and y (radians) and their rates.</summary>
        public Vector2 Tilt, Omega;
        public Vector2 Sway;
        public event Action<Vector3> Footfall;

        private readonly Func<float, float, float> ground;
        private readonly int maxSwing;
        private readonly float swingDur, rest;
        private Vector2 swayTarget, townVel, lastTown;
        private bool hasTown;
        /// <summary>How far the hull trails the core's town position, deliberate CoM sway included.</summary>
        public float Lag => hasTown ? Vector2.Distance(XY(P), lastTown) : 0f;
        private float sinceLift = 1f;

        public SpiderPhysics(int legs, Func<float, float, float> groundAt, Vector2 at, float yaw)
        {
            N = legs;
            ground = groundAt;
            maxSwing = legs >= 6 ? 3 : legs == 5 ? 2 : 1;
            swingDur = legs == 3 ? 0.26f : legs == 4 ? 0.30f : legs == 5 ? 0.36f : 0.34f;
            rest = RideHeight + Mass * G / (N * K); // preload so the hull rides at RideHeight on all feet
            // A tripod's two-foot support line sits half its foot radius from the centre, and that's how far it
            // has to lurch, so its feet stand closer in.
            float footRadius = legs == 3 ? 8.5f : legs == 4 ? 10.5f : legs == 5 ? 11f : 11.5f;
            Yaw = yaw;
            Legs = new Leg[N];
            float gz = 0f;
            for (int i = 0; i < N; i++)
            {
                float a = Mathf.PI * 2f * i / N + Mathf.PI / N;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var l = new Leg { HipLocal = dir * BodyRadius, NeutralLocal = dir * footRadius, PlantedFor = 1f };
                var f = at + Rotate(dir * footRadius, yaw);
                l.Foot = new Vector3(f.x, f.y, ground(f.x, f.y));
                gz += l.Foot.z;
                Legs[i] = l;
            }
            P = new Vector3(at.x, at.y, gz / N + RideHeight);
        }

        public Quaternion Rotation =>
            Quaternion.AngleAxis(Tilt.x * Mathf.Rad2Deg, Vector3.right) *
            Quaternion.AngleAxis(Tilt.y * Mathf.Rad2Deg, Vector3.up) *
            Quaternion.AngleAxis(Yaw * Mathf.Rad2Deg, Vector3.forward);

        public Vector3 HipWorld(int i) => P + Rotation * Legs[i].HipLocal;

        private static Vector2 Rotate(Vector3 v, float yaw)
        {
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static Vector2 XY(Vector3 v) => new Vector2(v.x, v.y);

        public void Step(float dt, Vector2 town, bool walking)
        {
            if (dt <= 0f) return;
            townVel = hasTown ? Vector2.Lerp(townVel, (town - lastTown) / dt, 0.3f) : Vector2.zero;
            lastTown = town; hasTown = true;
            Plan(dt, town, walking);
            Sway = Vector2.MoveTowards(Sway, swayTarget, dt * 10f);
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / SubStep));
            float h = dt / n;
            for (int s = 0; s < n; s++) Integrate(h, town);        }

        private void Integrate(float h, Vector2 town)
        {
            var R = Rotation;
            var force = new Vector3(0f, 0f, -Mass * G);
            var torque = Vector2.zero;
            int planted = 0;
            for (int i = 0; i < N; i++)
            {
                var l = Legs[i];
                if (!l.Planted) { AdvanceSwing(l, h); continue; }
                planted++;
                l.PlantedFor += h;
                var r = R * l.HipLocal;
                float ext = P.z + r.z - l.Foot.z;
                float vz = V.z + Omega.x * r.y - Omega.y * r.x;
                float f = Mathf.Max(0f, K * (rest - ext) - C * vz);
                l.Load = f;
                force.z += f;
                torque.x += r.y * f;
                torque.y -= r.x * f;
                float cap = f * PadHalfWidth;
                torque.x += Mathf.Clamp(-PadK * Tilt.x - PadC * Omega.x, -cap, cap);
                torque.y += Mathf.Clamp(-PadK * Tilt.y - PadC * Omega.y, -cap, cap);
            }
            if (planted > 0)
            {
                var want = town + Sway;
                var fh = DriveK * (want - XY(P)) - DriveC * (XY(V) - townVel);
                force.x += fh.x; force.y += fh.y;
                // traction acts at the feet, below the CoM: accelerating pitches the nose up, braking dips it
                torque.x += ComHeight * fh.y;
                torque.y -= ComHeight * fh.x;
            }
            V += force / Mass * h;
            P += V * h;
            Omega += torque / Inertia * h;
            Tilt += Omega * h;
            Tilt = Vector2.ClampMagnitude(Tilt, 0.55f);
            float floor = ground(P.x, P.y) + 1.6f;
            if (P.z < floor) { P.z = floor; if (V.z < 0f) V.z = 0f; }
        }

        private void AdvanceSwing(Leg l, float h)
        {
            l.SwingT += h / l.SwingDur;
            float s = Mathf.Clamp01(l.SwingT);
            // lift first, travel, then set down: the foot never drags across a step edge
            float sh = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((s - 0.12f) / 0.76f));
            var xy = Vector2.Lerp(XY(l.SwingFrom), XY(l.SwingTo), sh);
            float z = s < 0.45f
                ? Mathf.Lerp(l.SwingFrom.z, l.SwingPeak, 1f - (1f - s / 0.45f) * (1f - s / 0.45f))
                : Mathf.Lerp(l.SwingPeak, l.SwingTo.z, Mathf.Pow((s - 0.45f) / 0.55f, 2f));
            l.Foot = new Vector3(xy.x, xy.y, z);
            if (l.SwingT >= 1f) Plant(l, XY(l.SwingTo));
        }

        private void Plant(Leg l, Vector2 at)
        {
            l.Foot = new Vector3(at.x, at.y, ground(at.x, at.y));
            l.Planted = true;
            l.PlantedFor = 0f;
            Footfall?.Invoke(l.Foot);
        }

        private Vector2 NeutralWorld(int i, Vector2 body, float yaw) => body + Rotate(Legs[i].NeutralLocal, yaw);

        private void Plan(float dt, Vector2 town, bool walking)
        {
            sinceLift += dt;
            int swinging = 0;
            for (int i = 0; i < N; i++) if (!Legs[i].Planted) swinging++;

            // Catch reflex: a hull tipping hard plants any swinging foot where it is.
            if (Tilt.magnitude > 0.3f)
                for (int i = 0; i < N; i++)
                    if (!Legs[i].Planted && Legs[i].SwingT > 0.35f) { Plant(Legs[i], XY(Legs[i].Foot)); swinging--; }

            if (swinging == 0) swayTarget = Vector2.zero;
            if (swinging >= maxSwing || sinceLift < 0.06f) return;

            var vel = XY(V);
            var body = XY(P) + vel * 0.25f;
            int best = -1; float bestErr = walking ? 1.1f : 0.6f;
            for (int i = 0; i < N; i++)
            {
                var l = Legs[i];
                if (!l.Planted || l.PlantedFor < 0.12f) continue;
                if (!Legs[(i + 1) % N].Planted || !Legs[(i + N - 1) % N].Planted) continue;
                float err = Vector2.Distance(XY(l.Foot), NeutralWorld(i, body, Yaw));
                if (err > bestErr) { bestErr = err; best = i; }
            }
            if (best < 0) return;

            // a tripod lifting off-balance just falls over, so it waits far longer before forcing a step
            bool forced = bestErr > (N == 3 ? MaxDev * 1.4f : MaxDev);
            var com = XY(P) + vel * swingDur * 0.5f;
            // a forced step may only go when no other leg is already in the air
            if ((!forced || swinging > 0) && !Supported(best, com, out var safe))
            {
                // shift the CoM over the feet that will stay down, then try again next frame
                swayTarget = Vector2.ClampMagnitude(safe - town, 9f);
                return;
            }
            Lift(best, vel);
            // six legs: the alternate legs go with it, the insect's tripod (the other three stand in a wide triangle)
            if (N == 6)
                for (int k = 2; k <= 4; k += 2)
                {
                    var o = Legs[(best + k) % N];
                    if (o.Planted && o.PlantedFor >= 0.12f) Lift((best + k) % N, vel);
                }
        }

        /// <summary>Is <paramref name="com"/> held up by the planted feet other than <paramref name="lifting"/>?
        /// <paramref name="safe"/> is where the CoM should go if it isn't.</summary>
        private bool Supported(int lifting, Vector2 com, out Vector2 safe)
        {
            var pts = new Vector2[N];
            int m = 0;
            for (int i = 0; i < N; i++) if (i != lifting && Legs[i].Planted) pts[m++] = XY(Legs[i].Foot);
            safe = com;
            if (m < 2) return false;
            if (m == 2)
            {
                // a two-foot support is a line: the pads give it only a hand's width, so lurch onto it
                var a = pts[0]; var b = pts[1];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(com - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
                var closest = a + ab * t;
                safe = Vector2.Lerp(closest, (a + b) * 0.5f, 0.15f);
                return Vector2.Distance(com, closest) < 0.7f;
            }
            // m >= 3: legs are radial, so angular order around the CoM is the polygon order
            var centroid = Vector2.zero;
            for (int i = 0; i < m; i++) centroid += pts[i];
            centroid /= m;
            Array.Sort(pts, 0, m, System.Collections.Generic.Comparer<Vector2>.Create((p, q) =>
                Mathf.Atan2(p.y - centroid.y, p.x - centroid.x).CompareTo(Mathf.Atan2(q.y - centroid.y, q.x - centroid.x))));
            float margin = float.MaxValue;
            for (int i = 0; i < m; i++)
            {
                var a = pts[i]; var b = pts[(i + 1) % m];
                var e = (b - a).normalized;
                var inward = new Vector2(-e.y, e.x);
                margin = Mathf.Min(margin, Vector2.Dot(com - a, inward));
            }
            safe = Vector2.Lerp(com, centroid, 0.7f);
            return margin > 0.9f;
        }

        private void Lift(int i, Vector2 vel)
        {
            var l = Legs[i];
            // Raibert-style foothold: where neutral will be after the swing, plus half a stance of travel ahead
            float stance = (N - 1) * (swingDur + 0.1f) / maxSwing;
            var body = XY(P) + vel * swingDur;
            var neutral = NeutralWorld(i, body, Yaw);
            var target = neutral + Vector2.ClampMagnitude(vel * stance * 0.5f, MaxDev * 0.75f);
            float top = Mathf.Max(l.Foot.z, ground(target.x, target.y));
            var from = XY(l.Foot);
            for (int k = 1; k < 8; k++)
            {
                var q = Vector2.Lerp(from, target, k / 8f);
                top = Mathf.Max(top, ground(q.x, q.y));
            }
            l.SwingFrom = l.Foot;
            l.SwingTo = new Vector3(target.x, target.y, ground(target.x, target.y));
            l.SwingPeak = top + Clearance;
            l.SwingDur = swingDur;
            l.SwingT = 0f;
            l.Planted = false;
            l.Load = 0f;
            sinceLift = 0f;
        }
    }
}
