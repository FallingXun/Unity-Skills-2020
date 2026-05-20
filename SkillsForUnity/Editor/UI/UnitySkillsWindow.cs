using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace UnitySkills
{
    /// <summary>
    /// Unity Editor Window — UnitySkills v2 layout.
    /// Topbar (server status + URL + toggle + settings) — persistent.
    /// 3 tabs: Skills / AI Config / History.
    /// Footer: version + live stats pill + segmented language switch.
    /// Settings panel: slide-in drawer from the right.
    /// </summary>
    public class UnitySkillsWindow : EditorWindow
    {
        private const string UxmlPath = "Packages/com.besty.unity-skills/Editor/UI/UnitySkillsWindow.uxml";
        private const string UssPath  = "Packages/com.besty.unity-skills/Editor/UI/UnitySkillsWindow.uss";

        [SerializeField] private int _selectedTab = 0;

        // ----- Skill catalog (unchanged data contract — Controllers consume it) -----
        public class SkillInfo
        {
            public string Name;
            public string Description;
            public MethodInfo Method;
        }
        private Dictionary<string, List<SkillInfo>> _skillsByCategory;
        public Dictionary<string, List<SkillInfo>> SkillsByCategory => _skillsByCategory;

        // ----- Sub-controllers -----
        private TopbarController         _topbar;
        private FooterController         _footer;
        private SettingsDrawerController _drawer;
        private SkillsTabController      _skillsController;
        private AIConfigTabController    _configController;
        private HistoryTabController     _historyController;

        // ----- Tab strip -----
        private VisualElement[] _tabContents;
        private Button[]        _tabButtons;
        private VisualElement[] _tabUnderlines;

        [MenuItem("Window/UnitySkills")]
        public static void ShowWindow()
        {
            var window = GetWindow<UnitySkillsWindow>("UnitySkills");
            window.minSize = new Vector2(420, 480);
        }

        private void OnEnable()
        {
            RefreshSkillsList();
        }

        public void CreateGUI()
        {
            // Load USS first so :root variables resolve when UXML clones
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null) rootVisualElement.styleSheets.Add(uss);
            else Debug.LogWarning($"[UnitySkills] Failed to load USS: {UssPath}");

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml == null)
            {
                Debug.LogError($"[UnitySkills] Failed to load UXML: {UxmlPath}");
                return;
            }
            uxml.CloneTree(rootVisualElement);

            CacheTabReferences();

            // --- Sub-controllers ---
            _topbar  = new TopbarController(rootVisualElement, this);
            _footer  = new FooterController(rootVisualElement, this);
            _drawer  = new SettingsDrawerController(rootVisualElement, this);

            _skillsController  = new SkillsTabController(_tabContents[0], this);
            _configController  = new AIConfigTabController(_tabContents[1], this);
            _historyController = new HistoryTabController(_tabContents[2], this);

            // --- Tab clicks ---
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int idx = i;
                if (_tabButtons[i] != null)
                    _tabButtons[i].clicked += () => SwitchTab(idx);
            }

            SwitchTab(_selectedTab);
            RefreshLocalization();

            // Live update tick — 500ms (server stats, status)
            rootVisualElement.schedule.Execute(OnLiveDataUpdate).Every(500).StartingIn(0);
        }

        private void CacheTabReferences()
        {
            _tabButtons    = new Button[3];
            _tabContents   = new VisualElement[3];
            _tabUnderlines = new VisualElement[3];
            for (int i = 0; i < 3; i++)
            {
                _tabButtons[i]    = rootVisualElement.Q<Button>($"tab-btn-{i}");
                _tabContents[i]   = rootVisualElement.Q<VisualElement>($"tab-content-{i}");
                _tabUnderlines[i] = rootVisualElement.Q<VisualElement>($"tab-underline-{i}");
            }
        }

        private void SwitchTab(int index)
        {
            if (index < 0 || index >= _tabContents.Length) return;
            _selectedTab = index;

            for (int i = 0; i < _tabContents.Length; i++)
            {
                if (_tabContents[i] != null)
                    _tabContents[i].style.display = (i == index) ? DisplayStyle.Flex : DisplayStyle.None;

                if (_tabButtons[i] != null)
                {
                    if (i == index) _tabButtons[i].AddToClassList("tab-active");
                    else            _tabButtons[i].RemoveFromClassList("tab-active");
                }

                if (_tabUnderlines[i] != null)
                {
                    if (i == index) _tabUnderlines[i].AddToClassList("active");
                    else            _tabUnderlines[i].RemoveFromClassList("active");
                }
            }

            if (_tabButtons[index] != null) _tabButtons[index].Blur();
        }

        /// <summary>
        /// Called when user clicks a skill in Skills Tab — now stays within the
        /// Skills tab (master-detail) instead of jumping to a separate "Test" tab.
        /// Tab switch ensured here so external callers (legacy code paths) still work.
        /// </summary>
        public void SelectTestSkill(string skillName, string defaultParams)
        {
            SwitchTab(0);
            _skillsController?.SelectSkillByName(skillName, defaultParams);
        }

        public void OpenSettings()  => _drawer?.Open();
        public void CloseSettings() => _drawer?.Close();

        // ----- Live tick — fanned out to controllers that care -----
        private void OnLiveDataUpdate()
        {
            _topbar?.UpdateLiveData();
            _footer?.UpdateLiveData();
        }

        // ----- Language switch (called by FooterController) -----
        public void SetLanguage(SkillsLocalization.Language lang)
        {
            if (SkillsLocalization.Current == lang) return;
            SkillsLocalization.Current = lang;
            RefreshLocalization();
        }

        public void RefreshLocalization()
        {
            // Main tabs
            if (_tabButtons[0] != null) _tabButtons[0].text = SkillsLocalization.Get("tab_skills");
            if (_tabButtons[1] != null) _tabButtons[1].text = SkillsLocalization.Get("tab_ai_config");
            if (_tabButtons[2] != null) _tabButtons[2].text = SkillsLocalization.Get("tab_history");

            _topbar?.RefreshLocalization();
            _footer?.RefreshLocalization();
            _drawer?.RefreshLocalization();
            _skillsController?.RefreshLocalization();
            _configController?.RefreshLocalization();
            _historyController?.RefreshLocalization();
        }

        // ===== Skill catalog (preserved API for controllers) =====

        public void RefreshSkillsList()
        {
            _skillsByCategory = new Dictionary<string, List<SkillInfo>>();
            var allTypes = SkillsCommon.GetAllLoadedTypes();

            foreach (var type in allTypes)
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    UnitySkillAttribute attr;
                    try { attr = method.GetCustomAttribute<UnitySkillAttribute>(); }
                    catch { continue; }
                    if (attr == null) continue;

                    var category = type.Name.Replace("Skills", "");
                    if (!_skillsByCategory.ContainsKey(category))
                        _skillsByCategory[category] = new List<SkillInfo>();

                    _skillsByCategory[category].Add(new SkillInfo
                    {
                        Name = attr.Name ?? method.Name,
                        Description = attr.Description ?? "",
                        Method = method
                    });
                }
            }
        }

        public string BuildDefaultParams(MethodInfo method)
        {
            var ps = method.GetParameters();
            if (ps.Length == 0) return "{}";

            var parts = ps.Select(p =>
            {
                var defaultVal = p.HasDefaultValue ? p.DefaultValue : GetDefaultForType(p.ParameterType);
                var valStr = defaultVal == null ? "null" :
                    p.ParameterType == typeof(string) ? $"\"{defaultVal}\"" :
                    defaultVal.ToString().ToLower();
                return $"\"{p.Name}\": {valStr}";
            });

            return "{\n  " + string.Join(",\n  ", parts) + "\n}";
        }

        private object GetDefaultForType(System.Type t)
        {
            if (t == typeof(string)) return "";
            if (t == typeof(int) || t == typeof(float)) return 0;
            if (t == typeof(bool)) return false;
            return null;
        }
    }
}
