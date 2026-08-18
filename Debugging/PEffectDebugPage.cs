using System;
using System.Globalization;
using UnityEngine;
using XX;
using m2d;

namespace Polaris.Particles.Debugging
{
    internal static class PEffectDebugPage
    {
        private const string InputFlag = "__PEFFECT_DBG";
        private const int WindowId = 0x50454658; // PEFX
        private const float ReplayDelaySeconds = 0.6f;

        private static readonly string[] Tabs = { "Preview", "Source" };

        private static GameObject _host;
        private static bool _stylesReady;
        private static bool _open;
        private static bool _inputHeld;
        private static Rect _window = new Rect(36f, 36f, 1000f, 660f);
        private static Vector2 _fileScroll;
        private static Vector2 _definitionScroll;
        private static Vector2 _sourceScroll;
        private static int _tab;
        private static int _selectedFile;
        private static int _selectedSection;
        private static string _notice = string.Empty;
        private static string _x = "0";
        private static string _y = "0";
        private static string _z = "0";
        private static string _time = "240";
        private static float _zoom = 1f;
        private static bool _autoReplay = true;
        private static bool _loopPreview = true;
        private static bool _loopArmed;
        private static bool _loopObservedActive;
        private static float _replayAt = -1f;

        /// <summary>文件列表与右侧面板共用的高度。</summary>
        private static float PanelHeight => Mathf.Max(120f, _window.height - 86f);

        internal static void Toggle()
        {
            if (_open)
                Close();
            else
                Open();
        }

        internal static void Open()
        {
            if (_open || !PEffectDebugRuntime.IsEnabled)
                return;
            _open = true;
            EnsureHost();
            PEffectPreviewRenderer.Instance?.SetVisible(true);
            ClampWindow();
            HoldInput(true);
            if (_autoReplay && SelectedSection(PEffectDebugStore.Current) != null)
                PlaySelected();
        }

        internal static void Close()
        {
            if (!_open)
                return;
            StopPreview();
            PEffectPreviewRenderer.Instance?.SetVisible(false);
            _open = false;
            HoldInput(false);
        }

        internal static void Shutdown()
        {
            StopPreview();
            Close();
            if (_host != null)
            {
                UnityEngine.Object.Destroy(_host);
                _host = null;
            }
        }

        internal static void OnSnapshotApplied(PEffectDebugSnapshot snapshot)
        {
            _selectedFile = ClampIndex(_selectedFile, snapshot.Documents.Count);
            PEffectDebugDocument document = SelectedDocument(snapshot);
            _selectedSection = document == null ? 0 : ClampIndex(_selectedSection, document.Sections.Count);
            _notice = $"Generation {snapshot.Generation} loaded ({snapshot.Documents.Count} files).";
            if (_autoReplay && SelectedSection(snapshot) != null && CanPreview())
                PlaySelected();
        }

        internal static void StopPreview()
        {
            _loopArmed = false;
            _loopObservedActive = false;
            _replayAt = -1f;
            try
            {
                PEffectPreviewRenderer.Instance?.Stop();
            }
            catch (Exception ex)
            {
                PolarisAPI.Errors.Report(ex, "stopping .peffect debug preview", typeof(PEffectDebugPage).Assembly);
            }
        }

        /// <summary>开启 Loop 时，等效果自然结束再延迟重播一次。</summary>
        internal static void Tick()
        {
            if (!_open || !_loopPreview || !_loopArmed)
                return;

            PEffectPreviewRenderer preview = PEffectPreviewRenderer.Instance;
            if (preview == null)
                return;
            if (preview.IsPlaying)
            {
                _loopObservedActive = true;
                _replayAt = -1f;
                return;
            }
            if (!_loopObservedActive)
                return;
            if (_replayAt < 0f)
            {
                _replayAt = Time.unscaledTime + ReplayDelaySeconds;
                return;
            }
            if (Time.unscaledTime < _replayAt)
                return;

            _loopArmed = false;
            PlaySelected();
        }

        internal static void Draw()
        {
            if (!_open)
                return;

            EnsureStyles();
            GUI.depth = -1100;
            _window = GUI.Window(WindowId, _window, DrawWindow, ".peffect Debug", Styles.Window);
        }

        private static void DrawWindow(int id)
        {
            PEffectDebugSnapshot snapshot = PEffectDebugStore.Current;
            bool empty = snapshot.Documents.Count == 0;

            GUILayout.BeginHorizontal();
            GUILayout.Label(
                empty
                    ? "No snapshot received — click Debug in PolarisTools."
                    : $"Generation {snapshot.Generation} · {snapshot.Documents.Count} file(s) · {snapshot.UpdatedAt:HH:mm:ss}",
                empty ? Styles.Warning : Styles.Dim);
            GUILayout.FlexibleSpace();
            _tab = GUILayout.Toolbar(_tab, Tabs, Styles.Button, GUILayout.Width(220f));
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            DrawFiles(snapshot);
            GUILayout.Space(6f);
            if (_tab == 0)
                DrawPreview(snapshot);
            else
                DrawSource(snapshot);
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);
            DrawFooter();
            GUI.DragWindow(new Rect(0f, 0f, _window.width, 22f));
        }

        private static void DrawFiles(PEffectDebugSnapshot snapshot)
        {
            GUILayout.BeginVertical(Styles.Panel, GUILayout.Width(245f), GUILayout.Height(PanelHeight));
            GUILayout.Label("Files", Styles.Header);
            _fileScroll = GUILayout.BeginScrollView(_fileScroll);
            for (int i = 0; i < snapshot.Documents.Count; i++)
            {
                PEffectDebugDocument document = snapshot.Documents[i];
                GUIStyle style = i == _selectedFile ? Styles.SelectedButton : Styles.Button;
                if (GUILayout.Button(document.VirtualName + PEffectSyntax.Extension, style))
                {
                    _selectedFile = i;
                    _selectedSection = 0;
                    _sourceScroll = Vector2.zero;
                }
                GUILayout.Label($"{document.Sections.Count} definitions", Styles.Dim);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void DrawPreview(PEffectDebugSnapshot snapshot)
        {
            GUILayout.BeginVertical(Styles.Panel, GUILayout.ExpandWidth(true), GUILayout.Height(PanelHeight));
            PEffectDebugDocument document = SelectedDocument(snapshot);
            if (document == null)
            {
                GUILayout.Label("Push a .peffect snapshot from PolarisTools first.", Styles.Dim);
                GUILayout.EndVertical();
                return;
            }

            DrawDefinitions(document);

            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Isolated preview", Styles.Header);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Rendered off-screen — the map is not modified", Styles.Dim);
            GUILayout.EndHorizontal();
            DrawPreviewCanvas();

            GUILayout.Space(5f);
            GUILayout.Label("Local preview controls", Styles.Header);
            GUILayout.BeginHorizontal();
            Field("X", ref _x, 16f);
            Field("Y", ref _y, 16f);
            Field("Z", ref _z, 16f);
            Field("Life(fr)", ref _time, 48f);
            GUILayout.Label("Zoom", Styles.Dim, GUILayout.Width(38f));
            _zoom = GUILayout.HorizontalSlider(_zoom, 0.25f, 4f, GUILayout.Width(105f));
            GUILayout.Label(_zoom.ToString("0.00", CultureInfo.InvariantCulture) + "×", Styles.Dim, GUILayout.Width(42f));
            PEffectPreviewRenderer.Instance?.SetZoom(_zoom);
            GUILayout.EndHorizontal();

            DrawPlaybackControls(snapshot);
            GUILayout.EndVertical();
        }

        private static void DrawDefinitions(PEffectDebugDocument document)
        {
            GUILayout.Label("Definitions", Styles.Header);
            _definitionScroll = GUILayout.BeginScrollView(_definitionScroll, GUILayout.Height(112f));
            for (int i = 0; i < document.Sections.Count; i++)
            {
                PEffectSection section = document.Sections[i];
                GUILayout.BeginHorizontal();
                GUILayout.Label(section.Label, Styles.Kind, GUILayout.Width(72f));
                GUIStyle style = i == _selectedSection ? Styles.SelectedButton : Styles.Button;
                if (GUILayout.Button(section.Key, style))
                    _selectedSection = i;
                GUILayout.Label("line " + section.Line, Styles.Dim, GUILayout.Width(70f));
                GUILayout.EndHorizontal();
            }
            if (document.Sections.Count == 0)
                GUILayout.Label("This file contains no recognised section headers.", Styles.Warning);
            GUILayout.EndScrollView();
        }

        private static void DrawPlaybackControls(PEffectDebugSnapshot snapshot)
        {
            GUILayout.BeginHorizontal();
            GUI.enabled = SelectedSection(snapshot) != null && CanPreview();
            if (GUILayout.Button("Play / restart", Styles.PrimaryButton, GUILayout.Width(140f)))
                PlaySelected();
            GUI.enabled = true;
            if (GUILayout.Button("Stop", Styles.Button, GUILayout.Width(90f)))
            {
                StopPreview();
                _notice = "Preview stopped.";
            }
            bool loop = GUILayout.Toggle(_loopPreview, "Loop", Styles.Toggle, GUILayout.Width(62f));
            if (loop != _loopPreview)
            {
                _loopPreview = loop;
                _loopArmed = loop && PEffectPreviewRenderer.Instance?.IsPlaying == true;
                _loopObservedActive = _loopArmed;
                _replayAt = -1f;
            }
            _autoReplay = GUILayout.Toggle(_autoReplay, "Replay after editor push", Styles.Toggle);
            GUILayout.EndHorizontal();
        }

        private static void DrawPreviewCanvas()
        {
            float height = Mathf.Max(170f, _window.height - 390f);
            Rect rect = GUILayoutUtility.GetRect(200f, height, GUILayout.ExpandWidth(true));
            GUI.Box(rect, GUIContent.none, Styles.PreviewCanvas);

            Texture texture = PEffectPreviewRenderer.Instance?.Texture;
            if (texture != null)
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);

            Color previous = GUI.color;
            GUI.color = new Color(0.45f, 0.65f, 0.75f, 0.32f);
            GUI.DrawTexture(new Rect(rect.center.x, rect.y + 8f, 1f, rect.height - 16f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.x + 8f, rect.center.y, rect.width - 16f, 1f), Texture2D.whiteTexture);
            GUI.color = previous;

            if (texture == null)
                GUI.Label(rect, "Preview renderer is not ready.", Styles.PreviewMessage);
        }

        private static void DrawSource(PEffectDebugSnapshot snapshot)
        {
            GUILayout.BeginVertical(Styles.Panel, GUILayout.ExpandWidth(true), GUILayout.Height(PanelHeight));
            PEffectDebugDocument document = SelectedDocument(snapshot);
            if (document == null)
            {
                GUILayout.Label("No source loaded.", Styles.Dim);
                GUILayout.EndVertical();
                return;
            }

            GUILayout.Label(document.DisplayPath, Styles.Header);
            _sourceScroll = GUILayout.BeginScrollView(_sourceScroll);
            GUILayout.TextArea(document.Text, Styles.Source, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private static void DrawFooter()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.IsNullOrEmpty(_notice) ? "F9 closes this page" : _notice,
                string.IsNullOrEmpty(_notice) ? Styles.Dim : Styles.Warning);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close (F9)", Styles.Button, GUILayout.Width(100f)))
                Close();
            GUILayout.EndHorizontal();
        }

        private static void Field(string label, ref string value, float labelWidth)
        {
            GUILayout.Label(label, Styles.Dim, GUILayout.Width(labelWidth));
            value = GUILayout.TextField(value, Styles.Field, GUILayout.Width(62f));
        }

        private static void PlaySelected()
        {
            PEffectSection section = SelectedSection(PEffectDebugStore.Current);
            if (section == null)
                return;

            if (!TryNumber(_x, out float x) || !TryNumber(_y, out float y)
                || !TryNumber(_z, out float z) || !int.TryParse(_time, NumberStyles.Integer, CultureInfo.InvariantCulture, out int time)
                || time < 1)
            {
                _notice = "X, Y and Z must be numbers; Life must be at least 1 frame.";
                return;
            }

            PEffectPreviewRenderer preview = PEffectPreviewRenderer.Instance;
            if (preview == null)
            {
                _notice = "The isolated preview renderer is not ready.";
                return;
            }

            try
            {
                StopPreview();
                Play(preview, section, x, y, z, time);
                _notice = $"Playing {section.Label} {section.Key} in the isolated IMGUI preview.";
                _loopArmed = _loopPreview;
                _loopObservedActive = preview.IsPlaying;
                _replayAt = -1f;
            }
            catch (Exception ex)
            {
                StopPreview();
                _notice = ex.Message;
            }
        }

        private static void Play(
            PEffectPreviewRenderer preview,
            PEffectSection section,
            float x,
            float y,
            float z,
            int time)
        {
            switch (section.Kind)
            {
                case PEffectSectionKind.Timeline:
                {
                    M2DBase game = M2DBase.Instance;
                    if (game == null)
                        throw new InvalidOperationException("The game event listener is not ready for SETTER preview.");
                    var variables = new VariableP(8)
                        .Add("x", x).Add("y", y)
                        .Add("cx", x).Add("cy", y)
                        .Add("z", z)
                        .Add("time", time).Add("maxt", time);
                    if (preview.PlayTimeline(section.Key, game, variables) == null)
                        throw new InvalidOperationException("The timeline could not be started (unknown key or thread pool full).");
                    return;
                }
                case PEffectSectionKind.Particle:
                    if (EfParticleManager.Get(section.Key, true, true) == null)
                        throw new InvalidOperationException("The particle key is not present in the original particle registry after reload.");
                    if (preview.PlayParticle(section.Key, x, y, z, time) == null)
                        throw new InvalidOperationException("The isolated preview pool could not allocate the particle.");
                    return;
                default: // PEffectSectionKind.AttackGhost
                {
                    AttackGhostDrawer drawer = EfParticleManager.GetAGD(section.Key);
                    if (drawer == null)
                        throw new InvalidOperationException("The attack ghost definition was not found after reload.");
                    if (preview.PlayAttackGhost(section.Key, x, y, z, time, drawer) == null)
                        throw new InvalidOperationException("The isolated preview pool could not allocate the attack ghost.");
                    return;
                }
            }
        }

        private static bool CanPreview() => _open && PEffectPreviewRenderer.Instance != null;

        private static bool TryNumber(string value, out float number) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);

        private static int ClampIndex(int index, int count) => Math.Max(0, Math.Min(index, count - 1));

        private static PEffectDebugDocument SelectedDocument(PEffectDebugSnapshot snapshot)
        {
            if (snapshot.Documents.Count == 0)
                return null;
            _selectedFile = ClampIndex(_selectedFile, snapshot.Documents.Count);
            return snapshot.Documents[_selectedFile];
        }

        private static PEffectSection SelectedSection(PEffectDebugSnapshot snapshot)
        {
            PEffectDebugDocument document = SelectedDocument(snapshot);
            if (document == null || document.Sections.Count == 0)
                return null;
            _selectedSection = ClampIndex(_selectedSection, document.Sections.Count);
            return document.Sections[_selectedSection];
        }

        private static void HoldInput(bool hold)
        {
            if (_inputHeld == hold)
                return;
            _inputHeld = hold;
            if (hold)
                IN.FlgUiUse.Add(InputFlag);
            else
                IN.FlgUiUse.Rem(InputFlag);
        }

        private static void EnsureHost()
        {
            if (_host != null)
                return;
            _host = new GameObject("PolarisParticles Debug Overlay");
            UnityEngine.Object.DontDestroyOnLoad(_host);
            _host.AddComponent<PEffectDebugOverlay>();
            _host.AddComponent<PEffectPreviewRenderer>();
        }

        private static void ClampWindow()
        {
            float width = Mathf.Min(_window.width, Screen.width - 20f);
            float height = Mathf.Min(_window.height, Screen.height - 20f);
            _window = new Rect(
                Mathf.Clamp(_window.x, 0f, Math.Max(0f, Screen.width - width)),
                Mathf.Clamp(_window.y, 0f, Math.Max(0f, Screen.height - height)),
                width,
                height);
        }

        private static void EnsureStyles()
        {
            if (_stylesReady)
                return;
            _stylesReady = true;

            Font font = CreateUiFont();

            Styles.Window = new GUIStyle(GUI.skin.window) { padding = new RectOffset(8, 8, 22, 8) };
            Styles.Panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(7, 7, 7, 7) };
            Styles.Button = new GUIStyle(GUI.skin.button);
            Styles.SelectedButton = WithTextColor(new GUIStyle(Styles.Button), new Color(1f, 0.85f, 0.35f));
            Styles.PrimaryButton = WithTextColor(new GUIStyle(Styles.Button), new Color(0.62f, 0.9f, 1f));
            Styles.Header = WithTextColor(
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold }, new Color(0.62f, 0.82f, 1f));
            Styles.Dim = WithTextColor(new GUIStyle(GUI.skin.label), new Color(0.62f, 0.62f, 0.62f));
            Styles.Warning = WithTextColor(
                new GUIStyle(GUI.skin.label) { wordWrap = true }, new Color(1f, 0.55f, 0.4f));
            Styles.Kind = WithTextColor(
                new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold }, new Color(0.72f, 0.82f, 0.92f));
            Styles.Field = new GUIStyle(GUI.skin.textField);
            Styles.Toggle = new GUIStyle(GUI.skin.toggle);
            Styles.Source = new GUIStyle(GUI.skin.textArea) { wordWrap = false };
            Styles.PreviewCanvas = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(1, 1, 1, 1),
                normal = { background = Texture2D.blackTexture },
            };
            Styles.PreviewMessage = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Italic,
            };

            if (font == null)
                return;

            // PreviewCanvas 只画底色，不带文字，所以不必换字体。
            foreach (GUIStyle style in new[]
                     {
                         Styles.Window, Styles.Panel, Styles.Button, Styles.SelectedButton, Styles.PrimaryButton,
                         Styles.Header, Styles.Dim, Styles.Warning, Styles.Kind, Styles.Field, Styles.Toggle, Styles.Source,
                         Styles.PreviewMessage,
                     })
                style.font = font;
        }

        /// <summary>调试页会显示中文说明，优先挑一款带中日字形的系统字体。</summary>
        private static Font CreateUiFont()
        {
            try
            {
                Font font = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Meiryo", "Segoe UI" }, 13);
                if (font != null)
                    font.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return font;
            }
            catch
            {
                return null;
            }
        }

        private static GUIStyle WithTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            return style;
        }

        private static class Styles
        {
            internal static GUIStyle Window;
            internal static GUIStyle Panel;
            internal static GUIStyle Button;
            internal static GUIStyle SelectedButton;
            internal static GUIStyle PrimaryButton;
            internal static GUIStyle Header;
            internal static GUIStyle Dim;
            internal static GUIStyle Warning;
            internal static GUIStyle Kind;
            internal static GUIStyle Field;
            internal static GUIStyle Toggle;
            internal static GUIStyle Source;
            internal static GUIStyle PreviewCanvas;
            internal static GUIStyle PreviewMessage;
        }
    }
}
