using System.Collections.Generic;
using Lab.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// PoC E — 3D Low-Poly Walker. A heightmap terrain mesh (vertex-coloured biomes, fog), the IK-legged walker climbing it,
    /// a follow camera you orbit with a one-finger drag; tap the ground to walk there, tap a site icon to choose it. Site
    /// icons are UI projected from 3D. No external assets. Tests: does 3D earn its cost (readability, frame time, effort)?
    /// </summary>
    public sealed class LowPoly3DDemo : Demo
    {
        public override string Id => "lowpoly3d";
        public override string Title => "3D Low-Poly Walker";
        public override string Pitch => "Full 3D: procedural terrain and an IK-legged walker climbing hills; tap to walk, drag to orbit. Is 3D worth it?";

        private const float HeightScale = 14f, Grid = 2f;
        private GameObject root;
        private WalkerRig3D walker;
        private Mesh terrain;
        private Color[] baseColors;
        private Vector3[] verts;
        private int n;
        private float yaw = 35f, pulse = -1f;
        private LineRenderer rangeRing, pulseRing;
        private readonly Dictionary<int, VisualElement> icons = new Dictionary<int, VisualElement>();
        private readonly List<(V2 c, float r)> clear = new List<(V2, float)>();
        private Vector2? pressAt, pendingTap; private float pressYaw; private bool dragged;
        private readonly List<Material> materials = new List<Material>();
        private V2 lastPos;

        private float H(float x, float z) => L.World.HeightAt(x, z) * HeightScale;
        private Vector3 W(V2 p) => new Vector3(p.X, H(p.X, p.Y), p.Y);

        public override Vector2 TownOnScreen => DesignOf(LabApp.I.Camera.WorldToScreenPoint(walker.transform.position));

        protected override void BuildView(VisualElement view)
        {
            view.style.backgroundColor = Color.clear;
            root = new GameObject("Lab3D");
            var cam = App.Camera;
            cam.cullingMask = ~0;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.62f, 0.72f);
            cam.fieldOfView = 42f;
            cam.farClipPlane = 600f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = cam.backgroundColor;
            RenderSettings.fogStartDistance = 70f; RenderSettings.fogEndDistance = 180f;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.5f);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform);
            sun.type = LightType.Directional; sun.intensity = 1.1f; sun.color = new Color(1f, 0.95f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.shadows = LightShadows.Soft;
            BuildTerrain();
            walker = WalkerRig3D.Create(root.transform);
            walker.GroundAt = H;
            walker.Place(W(L.Town.Pos) + Vector3.up * 7.4f, 0f);
            rangeRing = Ring("range", new Color(0.5f, 0.85f, 1f, 0.8f), 0.6f);
            pulseRing = Ring("pulse", new Color(0.3f, 1f, 0.75f, 1f), 1.2f);
            clear.Add((L.Town.Pos, 18f));
            RecolorFog();
            foreach (var s in L.World.Sites)
            {
                var ic = new VisualElement { name = "site-" + s.Id, pickingMode = PickingMode.Ignore };
                ic.style.position = Position.Absolute;
                ic.style.width = ic.style.height = 100;
                ic.style.backgroundImage = new StyleBackground(LabArt.SiteIcon(s.Kind));
                ic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
                view.Add(ic);
                icons[s.Id] = ic;
            }
            // one finger: drag orbits, a tap (no drag) walks or selects
            view.RegisterCallback<PointerDownEvent>(e => { pressAt = e.localPosition; pressYaw = yaw; dragged = false; }, TrickleDown.TrickleDown);
            view.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (pressAt == null || e.pressedButtons == 0) return;
                float dx = e.localPosition.x - pressAt.Value.x;
                if (Mathf.Abs(dx) > 24f) dragged = true;
                if (dragged) yaw = pressYaw + dx * 0.07f;
            });
            // Released anywhere over the demo (the view or the HUD above it): a press that never dragged was a tap.
            Root.RegisterCallback<PointerUpEvent>(_ =>
            {
                var tap = pendingTap;
                pendingTap = null; pressAt = null;
                if (tap != null && !dragged && !Busy) TapAt(tap.Value);
            });
            lastPos = L.Town.Pos;
            PlaceCamera(1f);
        }

        public override void Unmount()
        {
            Object.Destroy(root);
            // meshes and materials made with `new` aren't owned by the GameObjects: free them with the demo
            Object.Destroy(terrain);
            foreach (var m in materials) Object.Destroy(m);
            RenderSettings.fog = false;
            App.Camera.cullingMask = 0;
            App.Camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
        }

        private void BuildTerrain()
        {
            var w = L.World;
            n = (int)(w.Width / Grid) + 1;
            verts = new Vector3[n * n];
            baseColors = new Color[n * n];
            var tris = new List<int>();
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float wx = x * Grid, wz = z * Grid;
                    float h = w.HeightAt(wx, wz);
                    verts[z * n + x] = new Vector3(wx, h * HeightScale, wz);
                    var g = LabArt.Ground[w.BiomeAt(wx, wz)];
                    baseColors[z * n + x] = Color.Lerp(g * 0.8f, g * 1.12f, h) + new Color(0, 0, 0, 1);
                    if (x < n - 1 && z < n - 1)
                    {
                        int i = z * n + x;
                        tris.Add(i); tris.Add(i + n); tris.Add(i + 1);
                        tris.Add(i + 1); tris.Add(i + n); tris.Add(i + n + 1);
                    }
                }
            terrain = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            terrain.vertices = verts; terrain.SetTriangles(tris, 0); terrain.colors = baseColors;
            terrain.RecalculateNormals();
            var go = new GameObject("Terrain");
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = terrain;
            var mat = new Material(Shader.Find("Lab/VertexColorLit"));
            materials.Add(mat);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Unseen ground is darkened (fog of war) by re-tinting vertex colours.</summary>
        private void RecolorFog()
        {
            var cols = new Color[baseColors.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                bool seen = IsClear(new V2(verts[i].x, verts[i].z));
                cols[i] = seen ? baseColors[i] : Color.Lerp(baseColors[i], new Color(0.12f, 0.13f, 0.17f), 0.75f);
            }
            terrain.colors = cols;
        }

        public bool IsClear(V2 p) { foreach (var (c, r) in clear) if (V2.Distance(c, p) <= r) return true; return false; }

        private LineRenderer Ring(string name, Color c, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            var mat = new Material(Shader.Find("Lab/UnlitColor"));
            materials.Add(mat);
            lr.sharedMaterial = mat;
            lr.positionCount = 64; lr.loop = true; lr.widthMultiplier = width;
            lr.startColor = lr.endColor = c;
            return lr;
        }

        private void DrawRing(LineRenderer lr, V2 c, float r)
        {
            for (int i = 0; i < 64; i++)
            {
                float a = i / 64f * Mathf.PI * 2f;
                var p = new V2(c.X + Mathf.Cos(a) * r, c.Y + Mathf.Sin(a) * r);
                lr.SetPosition(i, W(p) + Vector3.up * 0.6f);
            }
        }

        private float CameraDistance => L.Phase == LoopPhase.Battle || L.Phase == LoopPhase.Riding ? 96f : 160f;

        private void PlaceCamera(float k)
        {
            var target = walker.transform.position + Vector3.up * 2f;
            var dir = Quaternion.Euler(52f, yaw, 0f) * Vector3.back;
            var want = target + dir * CameraDistance;
            var cam = App.Camera.transform;
            cam.position = Vector3.Lerp(cam.position, want, k);
            cam.LookAt(target);
        }

        // ── Screen ↔ design ↔ world ──

        private Vector2 DesignOf(Vector3 screen)
        {
            var root = App.Document.rootVisualElement;
            float k = root.worldBound.width / Mathf.Max(1f, Screen.width);
            var panel = new Vector2(screen.x * k, (Screen.height - screen.y) * k);
            return App.Stage.WorldToLocal(panel);
        }

        private Vector3 ScreenOf(Vector2 design)
        {
            var root = App.Document.rootVisualElement;
            var panel = App.Stage.LocalToWorld(design);
            float k = Screen.width / Mathf.Max(1f, root.worldBound.width);
            return new Vector3(panel.x * k, Screen.height - panel.y * k, 0f);
        }

        private Vector2 SiteOnScreen(Site s) => DesignOf(App.Camera.WorldToScreenPoint(W(s.Pos) + Vector3.up * 5f));

        protected override void OnTap(Vector2 design)
        {
            // A real press fires this on pointer-down, before it's known whether the finger will drag to orbit:
            // hold it until release. Taps with no press in flight (automation) act at once.
            if (pressAt != null) { pendingTap = design; return; }
            TapAt(design);
        }

        private void TapAt(Vector2 design)
        {
            var s = PickSite(design, SiteOnScreen, 90f);
            if (s != null) { Select(s); return; }
            // ray-march to the terrain
            var ray = App.Camera.ScreenPointToRay(ScreenOf(design));
            for (float t = 0; t < 400f; t += 0.5f)
            {
                var p = ray.GetPoint(t);
                if (p.y <= H(p.x, p.z))
                {
                    if (p.x < 0 || p.z < 0 || p.x > L.World.Width || p.z > L.World.Height || L.Town.Stranded) return;
                    L.WalkTo(new V2(p.x, p.z));
                    Select(null);
                    return;
                }
            }
        }

        protected override void OnScanned(int found) { clear.Add((L.Town.Pos, L.Town.ScanRadius)); RecolorFog(); pulse = 0f; }

        protected override void OnSiteCleared(Site s) { clear.Add((s.Pos, 10f)); RecolorFog(); }

        protected override void TickView(float dt)
        {
            var v = L.Town.Pos - lastPos;
            var vel = new Vector3(v.X, 0, v.Y) / Mathf.Max(dt, 0.0001f);
            walker.Velocity = L.Phase == LoopPhase.Walking ? vel : Vector3.zero;
            var p = walker.transform.position;
            walker.transform.position = new Vector3(L.Town.Pos.X, p.y, L.Town.Pos.Y);
            if (v.Length > 0.001f)
            {
                var want = Quaternion.Euler(0, -Mathf.Atan2(v.Y, v.X) * Mathf.Rad2Deg, 0);
                walker.transform.rotation = Quaternion.Slerp(walker.transform.rotation, want, 1f - Mathf.Exp(-dt * 2.85f));
            }
            if (L.Phase == LoopPhase.Walking && V2.Distance(L.Town.Pos, lastSeen) > 1.5f) { clear.Add((L.Town.Pos, 18f)); lastSeen = L.Town.Pos; RecolorFog(); }
            lastPos = L.Town.Pos;
            PlaceCamera(1f - Mathf.Exp(-dt * 3f));
            DrawRing(rangeRing, L.Town.Pos, L.Town.BikeRange);
            if (pulse >= 0f)
            {
                pulse += dt / 1.1f;
                pulseRing.enabled = pulse <= 1f;
                if (pulse > 1f) pulse = -1f; else { DrawRing(pulseRing, L.Town.Pos, L.Town.ScanRadius * pulse); pulseRing.startColor = pulseRing.endColor = new Color(0.3f, 1f, 0.75f, 1f - pulse); }
            }
            else pulseRing.enabled = false;
            foreach (var s in L.World.Sites)
            {
                var ic = icons[s.Id];
                var sp = App.Camera.WorldToScreenPoint(W(s.Pos) + Vector3.up * 5f);
                var dp = DesignOf(sp);
                bool on = s.Revealed && sp.z > 0 && dp.x > -100 && dp.x < 1270 && dp.y > 250 && dp.y < 2300;
                ic.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                float size = Mathf.Clamp(5200f / Mathf.Max(20f, sp.z), 56f, 130f) * (Selected == s ? 1.2f : 1f);
                ic.style.width = ic.style.height = size;
                ic.style.left = dp.x - size / 2; ic.style.top = dp.y - size;
                ic.style.opacity = s.Cleared ? 0.35f : 1f;
            }
        }

        private V2 lastSeen;
    }
}
