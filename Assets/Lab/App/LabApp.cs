using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Lab.App
{
    /// <summary>
    /// GridGameCaravan traversal lab (owner direction 2026-10-10): several ways for the walking town to cross the
    /// surface, each a playable proof of concept of the same loop. Built entirely in code like GridGameGaiden: the
    /// scene is an empty shell, the UI is a 1170×2532 UI Toolkit stage, the 3D demo renders to the camera beneath it.
    /// </summary>
    public sealed class LabApp : MonoBehaviour
    {
        public const int DesignWidth = 1170, DesignHeight = 2532;
        public static LabApp I { get; private set; }

        public UIDocument Document { get; private set; }
        public VisualElement Stage { get; private set; }
        public VisualElement Screens { get; private set; }
        public VisualElement Overlay { get; private set; }
        public Camera Camera { get; private set; }
        public Demo Current { get; private set; }
        public int Seed = 20261010;

        public static readonly Func<Demo>[] Catalog =
        {
            () => new RouteMapDemo(),
            () => new HorizonDemo(),
            () => new IsoDemo(),
            () => new SpiderDemo(),
            () => new LowPoly3DDemo(),
            () => new HexDemo(),
        };

        private VisualElement letterbox;
        private Vector2 lastScreen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("LabApp");
            DontDestroyOnLoad(go);
            go.AddComponent<LabApp>();
        }

        private void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Camera = new GameObject("Camera").AddComponent<Camera>();
            Camera.transform.SetParent(transform, false);
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
            Camera.cullingMask = 0;
            BuildPanel();
            ShowMenu();
            LabDemoMode.TryStart(this);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            LabAutomation.TryStart(this);
#endif
        }

        private void BuildPanel()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/LabTheme");
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(DesignWidth, DesignHeight);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.clearColor = false; // the 3D demo shows through
            var go = new GameObject("UI");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            Document = go.AddComponent<UIDocument>();
            Document.panelSettings = settings;
            go.SetActive(true);
            var root = Document.rootVisualElement;
            root.styleSheets.Add(Resources.Load<StyleSheet>("UI/Theme"));
            Fonts.UseBody(root);
            letterbox = new VisualElement();
            letterbox.AddToClassList("fill");
            root.Add(letterbox);
            Stage = new VisualElement { name = "stage" };
            Stage.AddToClassList("stage");
            Stage.style.backgroundColor = Color.clear;
            letterbox.Add(Stage);
            Screens = new VisualElement { name = "screens" };
            Ui.Fill(Screens);
            Stage.Add(Screens);
            Overlay = new VisualElement { name = "overlay", pickingMode = PickingMode.Ignore };
            Ui.Fill(Overlay);
            Stage.Add(Overlay);
        }

        private void Update()
        {
            var size = new Vector2(Screen.width, Screen.height);
            if (size != lastScreen) { lastScreen = size; FitStage(); }
            Current?.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }

        /// <summary>Letterbox the portrait stage inside any window (never stretch).</summary>
        private void FitStage()
        {
            var panel = Document.rootVisualElement.panel;
            if (panel == null) return;
            float aspect = (float)DesignWidth / DesignHeight;
            bool wide = Screen.width / (float)Screen.height > aspect;
            Document.panelSettings.match = wide ? 1f : 0f;
            Stage.style.left = 0; Stage.style.top = 0;
            letterbox.style.alignItems = Align.Center;
            letterbox.style.justifyContent = Justify.Center;
            Stage.style.position = Position.Relative;
        }

        public void ShowMenu()
        {
            Current?.Unmount();
            Current = null;
            Camera.cullingMask = 0;
            Screens.Clear();
            Overlay.Clear();
            LabMenu.Build(Screens, this);
        }

        public void Open(string id)
        {
            foreach (var make in Catalog)
            {
                var d = make();
                if (d.Id != id) continue;
                Open(d);
                return;
            }
            Debug.LogWarning("[Lab] unknown demo " + id);
        }

        public void Open(Demo d)
        {
            Current?.Unmount();
            Screens.Clear();
            Overlay.Clear();
            Current = d;
            d.Mount(this, Screens, Seed);
            Debug.Log($"[Lab] opened {d.Id}");
        }
    }
}
