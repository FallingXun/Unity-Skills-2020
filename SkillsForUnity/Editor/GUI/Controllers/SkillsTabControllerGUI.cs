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
    /// Skills Tab — master-detail. Rect-based — no layout groups.
    /// Left: 45% search + skill list. Right: 55% detail pane.
    /// </summary>
    public class SkillsTabControllerGUI
    {
        private readonly UnitySkillsWindowGUI _window;
        private string _filterText = "", _selectedSkillName, _paramsJson = "{}", _resultJson = "";
        private bool _resultError;
        private Vector2 _listScroll;

        public SkillsTabControllerGUI(UnitySkillsWindowGUI w) { _window = w; }

        public void OnGUI(Rect r)
        {
            float splitX = r.x + r.width * 0.45f;
            float leftW = splitX - r.x - 2;

            // Left pane
            var leftR = new Rect(r.x, r.y, leftW, r.height);
            DrawLeftPane(leftR);

            // Divider
            EditorGUI.DrawRect(new Rect(splitX - 1, r.y, 2, r.height), new Color(0.10f, 0.10f, 0.10f));

            // Right pane
            var rightR = new Rect(splitX + 4, r.y, r.xMax - splitX - 4, r.height);
            DrawDetailPane(rightR);
        }

        private void DrawLeftPane(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.16f, 0.16f, 0.16f));

            // Toolbar
            float ty = r.y + 2;
            var tbBg = new Rect(r.x, ty, r.width, 24);
            EditorGUI.DrawRect(tbBg, new Color(0.18f, 0.18f, 0.18f));

            EditorGUI.BeginChangeCheck();
            _filterText = GUI.TextField(new Rect(r.x + 4, ty + 2, r.width - 60, 20), _filterText, EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck()) Repaint();

            if (GUI.Button(new Rect(r.xMax - 52, ty + 2, 22, 20), "↻", EditorStyles.toolbarButton))
            { _window.RefreshSkillsList(); SkillRouter.Refresh(); Repaint(); }
            if (GUI.Button(new Rect(r.xMax - 28, ty + 2, 22, 20), "✓", EditorStyles.toolbarButton))
                ValidateSkills();

            ty += 26;

            // Count bar
            var dict = _window.SkillsByCategory;
            int total = 0, cats = 0;
            if (dict != null)
                foreach (var kvp in dict) { var v = kvp.Value.Where(MatchesFilter).ToList(); if (v.Count > 0) { cats++; total += v.Count; } }
            GUI.Label(new Rect(r.x + 4, ty, r.width - 8, 14),
                string.Format(SkillsLocalization.Get("skills_count_format"), total, cats), EditorStyles.miniLabel);
            ty += 16;

            // Skills list (scroll)
            var listR = new Rect(r.x, ty, r.width, r.yMax - ty);
            float contentH = 0;
            if (dict != null)
                foreach (var kvp in dict.OrderBy(k => k.Key))
                {
                    var f = kvp.Value.Where(MatchesFilter).ToList();
                    if (f.Count == 0) continue;
                    contentH += CategoryHeight(kvp.Key, f);
                }
            _listScroll = GUI.BeginScrollView(listR, _listScroll, new Rect(0, 0, r.width - 16, Mathf.Max(contentH, 10)));
            float cy = 0;
            if (dict != null)
                foreach (var kvp in dict.OrderBy(k => k.Key))
                {
                    var f = kvp.Value.Where(MatchesFilter).ToList();
                    if (f.Count == 0) continue;
                    cy = DrawCategory(cy, r.width - 16, kvp.Key, f);
                }
            GUI.EndScrollView();
        }

        private bool MatchesFilter(UnitySkillsWindowGUI.SkillInfo s)
        {
            if (string.IsNullOrEmpty(_filterText)) return true;
            var q = _filterText.ToLowerInvariant();
            return (s.Name?.ToLowerInvariant().Contains(q) ?? false)
                || (s.Description?.ToLowerInvariant().Contains(q) ?? false);
        }

        private static float CategoryHeight(string catName, List<UnitySkillsWindowGUI.SkillInfo> skills)
        {
            string fk = $"UnitySkillsGUI_Foldout_{catName}";
            if (EditorPrefs.GetBool(fk, false)) return 22; // collapsed
            return 22 + skills.Count * 26;
        }

        private float DrawCategory(float y, float w, string catName, List<UnitySkillsWindowGUI.SkillInfo> skills)
        {
            string fk = $"UnitySkillsGUI_Foldout_{catName}";
            bool collapsed = EditorPrefs.GetBool(fk, false);

            var hr = new Rect(0, y, w, 22);
            EditorGUI.DrawRect(hr, new Color(0.20f, 0.20f, 0.20f));
            GUI.Label(new Rect(4, y, 16, 22), collapsed ? "▶" : "▼", EditorStyles.miniLabel);
            GUI.Label(new Rect(20, y, w - 60, 22), catName, EditorStyles.boldLabel);
            GUI.Label(new Rect(w - 40, y, 36, 22), skills.Count.ToString(), EditorStyles.miniLabel);

            if (Event.current.type == EventType.MouseDown && hr.Contains(Event.current.mousePosition))
            { EditorPrefs.SetBool(fk, !collapsed); Event.current.Use(); Repaint(); }

            y += 22;
            if (!collapsed)
            {
                foreach (var s in skills)
                {
                    y = DrawSkillRow(y, w, s);
                }
            }
            return y;
        }

        private float DrawSkillRow(float y, float w, UnitySkillsWindowGUI.SkillInfo skill)
        {
            bool sel = skill.Name == _selectedSkillName;
            var hr = new Rect(0, y, w, 26);
            EditorGUI.DrawRect(hr, sel ? new Color(0.17f, 0.36f, 0.53f) : new Color(0.14f, 0.14f, 0.14f));

            GUI.Label(new Rect(22, y, w - 80, 26), skill.Name, EditorStyles.label);

            if (IsHighRisk(skill))
            {
                var rr = new Rect(w - 60, y + 5, 56, 16);
                EditorGUI.DrawRect(rr, new Color(0.88f, 0.39f, 0.39f));
                GUI.Label(rr, SkillsLocalization.Get("skills_tag_danger"),
                    new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter });
            }

            if (Event.current.type == EventType.MouseDown && hr.Contains(Event.current.mousePosition))
            { OnSkillSelected(skill); Event.current.Use(); }

            return y + 26;
        }

        private static bool IsHighRisk(UnitySkillsWindowGUI.SkillInfo s)
        {
            var a = s.Method?.GetCustomAttribute<UnitySkillAttribute>();
            return a != null && (a.RiskLevel == "high" || (a.Operation & SkillOperation.Delete) != 0);
        }

        private void OnSkillSelected(UnitySkillsWindowGUI.SkillInfo s)
        {
            _selectedSkillName = s.Name;
            _paramsJson = _window.BuildDefaultParams(s.Method);
            _resultJson = ""; _resultError = false;
            Repaint();
        }

        public void SelectSkillByName(string n, string dp)
        { _selectedSkillName = n; _paramsJson = dp; _resultJson = ""; _resultError = false; Repaint(); }

        // ── Detail Pane ──

        private void DrawDetailPane(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.16f, 0.16f, 0.16f));

            var skill = FindSkill(_selectedSkillName);
            if (skill == null)
            {
                GUI.Label(r, SkillsLocalization.Get("skills_detail_empty"),
                    new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.48f, 0.48f, 0.48f) } });
                return;
            }

            var attr = skill.Method?.GetCustomAttribute<UnitySkillAttribute>();

            // Header
            float hy = r.y + 8;
            GUI.Label(new Rect(r.x + 12, hy, r.width - 24, 20), skill.Name, EditorStyles.boldLabel);
            hy += 24;

            if (attr != null)
            {
                GUI.Box(new Rect(r.x + 12, hy, Mathf.Min(100, r.width - 24), 18), attr.Category.ToString());
                if (IsHighRisk(skill))
                {
                    var rr = new Rect(r.x + 120, hy, 56, 18);
                    EditorGUI.DrawRect(rr, new Color(0.88f, 0.39f, 0.39f));
                    GUI.Label(rr, SkillsLocalization.Get("skills_tag_danger"),
                        new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.white }, alignment = TextAnchor.MiddleCenter });
                }
                hy += 22;
            }

            string desc = SkillsLocalization.Get(skill.Name);
            if (desc == skill.Name) desc = skill.Description;
            if (!string.IsNullOrEmpty(desc))
            {
                var ds = new GUIStyle(EditorStyles.wordWrappedLabel) { normal = { textColor = new Color(0.63f, 0.63f, 0.63f) } };
                float dh = ds.CalcHeight(new GUIContent(desc), r.width - 24);
                GUI.Label(new Rect(r.x + 12, hy, r.width - 24, dh), desc, ds);
                hy += dh + 8;
            }
            else hy += 4;

            // Divider
            hy += 4;
            EditorGUI.DrawRect(new Rect(r.x + 8, hy, r.width - 16, 1), new Color(0.20f, 0.20f, 0.20f));
            hy += 8;

            // Params
            GUI.Label(new Rect(r.x + 12, hy, r.width - 24, 14), SkillsLocalization.Get("skills_detail_params_label"), EditorStyles.miniBoldLabel);
            hy += 16;
            float ph = Mathf.Max(80, r.yMax - hy - 120);
            _paramsJson = GUI.TextArea(new Rect(r.x + 12, hy, r.width - 24, ph), _paramsJson);
            hy += ph + 8;

            // Buttons
            var bb = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.29f, 0.62f, 1f);
            if (GUI.Button(new Rect(r.x + 12, hy, 100, 24), SkillsLocalization.Get("skills_detail_execute")))
                Execute(false);
            GUI.backgroundColor = bb;

            bool supportsDryRun = attr == null || attr.SupportsDryRun;
            GUI.enabled = supportsDryRun;
            if (GUI.Button(new Rect(r.x + 118, hy, 120, 24), SkillsLocalization.Get("skills_detail_dryrun")))
                Execute(true);
            GUI.enabled = true;

            if (GUI.Button(new Rect(r.xMax - 70, hy, 60, 24), SkillsLocalization.Get("skills_detail_clear")))
            { _paramsJson = ""; _resultJson = ""; _resultError = false; }
            hy += 30;

            // Result
            GUI.Label(new Rect(r.x + 12, hy, r.width - 24, 14), SkillsLocalization.Get("skills_detail_result_label"), EditorStyles.miniBoldLabel);
            hy += 16;
            float rh = r.yMax - hy - 4;
            GUI.color = _resultError ? new Color(0.88f, 0.39f, 0.39f) : new Color(0.43f, 0.77f, 0.43f);
            GUI.enabled = false;
            GUI.TextArea(new Rect(r.x + 12, hy, r.width - 24, rh), _resultJson);
            GUI.enabled = true;
            GUI.color = Color.white;
        }

        private UnitySkillsWindowGUI.SkillInfo FindSkill(string n)
        {
            var d = _window.SkillsByCategory;
            if (d == null) return null;
            foreach (var l in d.Values) foreach (var s in l) if (s.Name == n) return s;
            return null;
        }

        private void Execute(bool dryRun)
        {
            if (string.IsNullOrEmpty(_selectedSkillName)) return;
            string json = _paramsJson ?? "{}";
            if (dryRun) json = InjectDryRun(json);
            _resultJson = SkillRouter.Execute(_selectedSkillName, json) ?? "";
            _resultError = !string.IsNullOrEmpty(_resultJson) && (_resultJson.Contains("\"ok\": false") || _resultJson.Contains("\"error\""));
            Repaint();
        }

        private static string InjectDryRun(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return "{ \"dryRun\": true }";
            var t = json.TrimEnd(); int i = t.LastIndexOf('}');
            if (i < 0) return json;
            string b = t.Substring(0, i).TrimEnd();
            return b + (b.Length > 0 && b[b.Length - 1] != '{' ? "," : "") + "\n  \"dryRun\": true\n}";
        }

        private void ValidateSkills()
        {
            var issues = SkillRouter.ValidateMetadata();
            if (issues.Count == 0) SkillsLogger.Log(SkillsLocalization.Get("metadata_validation_passed"));
            else
            {
                SkillsLogger.Log(string.Format(SkillsLocalization.Get("metadata_validation_found"), issues.Count));
                foreach (var m in issues) { if (m.StartsWith("[ERROR]")) Debug.LogError($"[UnitySkills] {m}"); else Debug.LogWarning($"[UnitySkills] {m}"); }
            }
        }

        private void Repaint() { _window?.Repaint(); }
        public void RefreshLocalization() { }
    }
}
#endif

