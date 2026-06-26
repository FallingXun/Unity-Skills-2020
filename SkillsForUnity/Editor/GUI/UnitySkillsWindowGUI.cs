#if !UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using System;
namespace UnitySkills
{
    /// <summary>
    /// Unity Editor Window — UnitySkills IMGUI (OnGUI) layout.
    /// Layout: Topbar → Pending Banner → Tab Bar → Content (or split if drawer open) → Footer.
    ///
    /// Uses manual Begin/End (not using scopes) so we can draw all backgrounds
    /// inside each group. Never passes custom GUIStyles to layout groups.
    /// </summary>
    public class UnitySkillsWindowGUI : EditorWindow
    {
        private const string IconsFolder = "Packages/com.besty.unity-skills/Editor/UI/Icons";
        private const string PrefKeyFirstRunToast = "UnitySkills_FirstRunToastShown";

        [SerializeField] private int _selectedTab = 0;

        public class SkillInfo { public string Name, Description; public MethodInfo Method; }
        private Dictionary<string, List<SkillInfo>> _skillsByCategory;
        public Dictionary<string, List<SkillInfo>> SkillsByCategory => _skillsByCategory;

        private static readonly string[] TabKeys = { "tab_skills", "tab_ai_config", "tab_history" };
        private string[] _tabLabels;

        private TopbarControllerGUI         _topbar;
        private FooterControllerGUI         _footer;
        private SettingsDrawerControllerGUI _drawer;
        private PendingApprovalBannerControllerGUI _pendingBanner;
        private SkillsTabControllerGUI      _skillsController;
        private AIConfigTabControllerGUI    _configController;
        private HistoryTabControllerGUI     _historyController;

        private double _lastLiveUpdateTime;

        [MenuItem("Window/UnitySkills (IMGUI)")]
        public static void ShowWindow()
        {
            var w = GetWindow<UnitySkillsWindowGUI>("UnitySkills");
            w.minSize = new Vector2(420, 480);
        }

        private void OnEnable()
        {
            RefreshSkillsList();
            SkillsModeManager.OnChanged += Repaint;
            _topbar         = new TopbarControllerGUI(this);
            _footer         = new FooterControllerGUI(this);
            _drawer         = new SettingsDrawerControllerGUI(this);
            _pendingBanner  = new PendingApprovalBannerControllerGUI(this);
            _skillsController  = new SkillsTabControllerGUI(this);
            _configController  = new AIConfigTabControllerGUI(this);
            _historyController = new HistoryTabControllerGUI(this);
            _tabLabels = TabKeys.Select(k => SkillsLocalization.Get(k)).ToArray();
            MaybeShowFirstRunToast();
        }

        private void OnDisable() { SkillsModeManager.OnChanged -= Repaint; }

        private void Update()
        {
            if (EditorApplication.timeSinceStartup - _lastLiveUpdateTime > 0.5)
            { _lastLiveUpdateTime = EditorApplication.timeSinceStartup; Repaint(); }
        }

        // ── Main layout — manual Begin/End, no nesting hacks ──

        private void OnGUI()
        {
            var winW = position.width;
            var winH = position.height;

            // ── 1. Topbar ──
            float topbarH = 36;
            _topbar.OnGUI(new Rect(0, 0, winW, topbarH));

            // ── 1b. Pending Banner ──
            float bannerH = _pendingBanner.MeasureHeight();
            float bannerY = topbarH;
            _pendingBanner.OnGUI(new Rect(0, bannerY, winW, bannerH));

            // ── 2. Tab Bar ──
            float tabBarH = 30;
            float tabBarY = bannerY + bannerH;
            DrawTabBar(new Rect(0, tabBarY, winW, tabBarH));

            // ── 3. Content Area ──
            float contentY = tabBarY + tabBarH;
            float footerH = 26;
            float contentH = winH - contentY - footerH;

            if (_drawer.IsOpen)
            {
                // Split: 60% tabs left, 40% drawer right
                float leftW = winW * 0.58f;
                DrawTabContent(new Rect(0, contentY, leftW, contentH));
                _drawer.OnGUI(new Rect(leftW, contentY, winW - leftW, contentH));
            }
            else
            {
                DrawTabContent(new Rect(0, contentY, winW, contentH));
            }

            // ── 4. Footer ──
            _footer.OnGUI(new Rect(0, winH - footerH, winW, footerH));
        }

        // ── Tab Bar ──

        private void DrawTabBar(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.16f, 0.16f, 0.16f));

            float w = r.width / _tabLabels.Length;
            for (int i = 0; i < _tabLabels.Length; i++)
            {
                var btnRect = new Rect(r.x + i * w, r.y, w, r.height);
                bool active = i == _selectedTab;

                var style = new GUIStyle(EditorStyles.miniButtonMid)
                {
                    fontStyle = FontStyle.Bold,
                    fontSize = 11,
                    normal = { textColor = active ? new Color(0.29f, 0.62f, 1f) : new Color(0.63f, 0.63f, 0.63f) },
                    alignment = TextAnchor.MiddleCenter,
                };

                if (GUI.Button(btnRect, _tabLabels[i], style))
                    SwitchTab(i);

                // Underline
                if (active)
                {
                    var ul = new Rect(btnRect.x + 12, btnRect.yMax - 2, btnRect.width - 24, 2);
                    EditorGUI.DrawRect(ul, new Color(0.29f, 0.62f, 1f));
                }
            }
        }

        // ── Tab Content ──

        private void DrawTabContent(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.14f, 0.14f, 0.14f)); // bg

            var inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            switch (_selectedTab)
            {
                case 0: _skillsController.OnGUI(inner); break;
                case 1: _configController.OnGUI(inner); break;
                case 2: _historyController.OnGUI(inner); break;
            }
        }

        private void SwitchTab(int index)
        {
            if (index == _selectedTab) return;
            _selectedTab = index;
            Repaint();
        }

        public void RefreshLocalization()
        {
            _tabLabels = TabKeys.Select(k => SkillsLocalization.Get(k)).ToArray();
            _topbar?.RefreshLocalization();
            _footer?.RefreshLocalization();
            _drawer?.RefreshLocalization();
            _pendingBanner?.RefreshLocalization();
            _skillsController?.RefreshLocalization();
            _configController?.RefreshLocalization();
            _historyController?.RefreshLocalization();
            Repaint();
        }

        public void SelectTestSkill(string skillName, string defaultParams)
        { SwitchTab(0); _skillsController?.SelectSkillByName(skillName, defaultParams); }

        public void OpenSettings()  { _drawer?.Open();  Repaint(); }
        public void CloseSettings() { _drawer?.Close(); Repaint(); }
        public void SetLanguage(SkillsLocalization.Language lang)
        { if (SkillsLocalization.Current != lang) { SkillsLocalization.Current = lang; RefreshLocalization(); } }

        // ── Skill catalog ──

        public void RefreshSkillsList()
        {
            _skillsByCategory = new Dictionary<string, List<SkillInfo>>();
            foreach (var type in SkillsCommon.GetAllLoadedTypes())
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                UnitySkillAttribute attr;
                try { attr = method.GetCustomAttribute<UnitySkillAttribute>(); }
                catch { continue; }
                if (attr == null) continue;
                var cat = type.Name.Replace("Skills", "");
                if (!_skillsByCategory.ContainsKey(cat)) _skillsByCategory[cat] = new List<SkillInfo>();
                _skillsByCategory[cat].Add(new SkillInfo { Name = attr.Name ?? method.Name, Description = attr.Description ?? "", Method = method });
            }
        }

        public string BuildDefaultParams(MethodInfo method)
        {
            var ps = method.GetParameters();
            if (ps.Length == 0) return "{}";
            var parts = ps.Select(p => {
                object dv = p.HasDefaultValue ? p.DefaultValue : (p.ParameterType == typeof(string) ? (object)"" : (p.ParameterType == typeof(bool) ? (object)false : (object)0));
                var vs = dv == null ? "null" : p.ParameterType == typeof(string) ? $"\"{dv}\"" : dv.ToString().ToLower();
                return $"\"{p.Name}\": {vs}";
            });
            return "{\n  " + string.Join(",\n  ", parts) + "\n}";
        }

        // ── First-run toast ──

        private void MaybeShowFirstRunToast()
        {
            if (EditorPrefs.HasKey(PrefKeyFirstRunToast) || EditorPrefs.HasKey("UnitySkills_OperatingMode") || PermissionUiHelpers.IsExistingInstall()) return;
            EditorPrefs.SetBool(PrefKeyFirstRunToast, true);
            EditorApplication.delayCall += () => {
                string t = PermissionUiHelpers.L("perm_first_run_toast_title", "UnitySkills v1.9", "UnitySkills v1.9");
                string m = PermissionUiHelpers.L("perm_first_run_toast_msg", "Auto mode is the default...", "新安装默认 Auto 自动模式...");
                if (EditorUtility.DisplayDialog(t, m,
                    PermissionUiHelpers.L("perm_first_run_toast_open", "Open Permissions", "打开权限面板"),
                    PermissionUiHelpers.L("perm_first_run_toast_dismiss", "OK", "知道了")))
                {
                    var w = GetWindow<UnitySkillsWindowGUI>("UnitySkills");
                    w.minSize = new Vector2(420, 480);
                    EditorApplication.delayCall += () => w.OpenSettings();
                }
            };
        }

        public Texture2D LoadIcon(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconsFolder}/{name}.png");
        internal static Texture2D MakeColorTex(byte r, byte g, byte b) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, new Color32(r, g, b, 255)); t.Apply(); t.hideFlags = HideFlags.HideAndDontSave; return t; }
    }
}
#endif

