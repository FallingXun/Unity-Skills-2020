#if !UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System;
namespace UnitySkills
{
    /// <summary>
    /// Multi-select popup to add skills to the allowlist.
    /// Search field, category foldouts with toggles, Coding Assist preset,
    /// high-risk confirmation dialog on submit.
    /// </summary>
    public class AllowlistPickerWindowGUI : EditorWindow
    {
        private string _search = "";
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private List<IGrouping<string, SkillRouter.SkillInfo>> _grouped;
        private Vector2 _scrollPos;

        public static void Open()
        {
            var w = GetWindow<AllowlistPickerWindowGUI>(true,
                PermissionUiHelpers.L("perm_picker_title", "Add Skills to Allowlist", "添加 Skill 到白名单"),
                true);
            w.minSize = new Vector2(460, 520);
            w.Show();
            w.Focus();
        }

        private void OnEnable()
        {
            LoadCandidates();
        }

        private void OnGUI()
        {
            // ── Search ──
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(24));
            EditorGUI.BeginChangeCheck();
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck()) Repaint();
            EditorGUILayout.EndHorizontal();

            // ── Preset bar ──
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(8);
            string presetText = string.Format(
                PermissionUiHelpers.L("perm_picker_preset_coding", "+ Coding Assist pack ({0})", "+ 辅助代码编写包 ({0})"),
                AllowlistPresets.CodingAssist.Length);
            if (GUILayout.Button(presetText, EditorStyles.miniButton))
            {
                SelectCodingAssistPreset();
            }
            GUILayout.Space(8);
            EditorGUILayout.EndHorizontal();

            // Divider
            var divRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.Height(1));
            EditorGUI.DrawRect(divRect, new Color(0.20f, 0.20f, 0.20f));

            // ── Skill list ──
            if (_grouped == null || _grouped.Count == 0)
            {
                GUILayout.FlexibleSpace();
                var emptyStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.48f, 0.48f, 0.48f) },
                };
                EditorGUILayout.LabelField(
                    PermissionUiHelpers.L("perm_picker_all_in_allowlist",
                        "All skills already in allowlist", "全部 skill 已在白名单中"),
                    emptyStyle);
                GUILayout.FlexibleSpace();
            }
            else
            {
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));

                string q = _search?.Trim() ?? "";
                bool hasFilter = q.Length > 0;
                int totalShown = 0;

                foreach (var group in _grouped)
                {
                    var visible = group
                        .Where(s => !hasFilter
                                    || s.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                                    || group.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                    if (visible.Count == 0) continue;
                    totalShown += visible.Count;

                    int selectedInGroup = visible.Count(s => _selected.Contains(s.Name));
                    string headerSuffix = selectedInGroup > 0
                        ? string.Format(PermissionUiHelpers.L("perm_picker_selected_suffix",
                            "  [{0} selected]", "  [已选 {0}]"), selectedInGroup)
                        : "";

                    string foldKey = $"AllowlistPicker_Fold_{group.Key}";
                    bool collapsed = !EditorPrefs.GetBool(foldKey, !hasFilter);

                    var foldoutStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        fontSize = 11,
                        normal = { textColor = new Color(0.87f, 0.87f, 0.87f) },
                    };
                    bool newCollapsed = !EditorGUILayout.Foldout(!collapsed,
                        $"{group.Key}  ({visible.Count}){headerSuffix}", true, foldoutStyle);
                    bool wasCollapsed = collapsed;
                    collapsed = newCollapsed;
                    if (collapsed != wasCollapsed)
                        EditorPrefs.SetBool(foldKey, collapsed);

                    if (!collapsed)
                    {
                        // Group ops
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        if (GUILayout.Button(
                            PermissionUiHelpers.L("perm_picker_select_all", "Select all in group", "全选本组"),
                            EditorStyles.miniButton, GUILayout.Width(120)))
                        {
                            ToggleGroup(visible, true);
                        }
                        if (GUILayout.Button(
                            PermissionUiHelpers.L("perm_picker_clear_group", "Clear", "清空"),
                            EditorStyles.miniButton, GUILayout.Width(60)))
                        {
                            ToggleGroup(visible, false);
                        }
                        EditorGUILayout.EndHorizontal();

                        foreach (var skill in visible.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            DrawSkillRow(skill);
                        }
                    }
                }

                if (totalShown == 0)
                {
                    GUILayout.Space(20);
                    var emptyStyle = new GUIStyle(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.48f, 0.48f, 0.48f) },
                    };
                    EditorGUILayout.LabelField(
                        string.Format(PermissionUiHelpers.L("perm_picker_no_match",
                            "No skills match '{0}'", "没有匹配 '{0}' 的 skill"), q),
                        emptyStyle);
                }

                EditorGUILayout.EndScrollView();
            }

            // ── Footer: dark tint via GUI.backgroundColor ──
            var prevBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.ExpandWidth(true)))
            {
                GUI.backgroundColor = prevBg;
                int n = _selected.Count;
                var summaryStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 10,
                    normal = { textColor = new Color(0.63f, 0.63f, 0.63f) },
                };
                string summaryText = n == 0
                    ? PermissionUiHelpers.L("perm_picker_none_selected", "No skills selected", "未选中任何 skill")
                    : string.Format(PermissionUiHelpers.L("perm_picker_n_selected",
                        "{0} skill(s) selected", "已选中 {0} 个 skill"), n);
                EditorGUILayout.LabelField(summaryText, summaryStyle, GUILayout.ExpandWidth(true));

                GUI.enabled = n > 0;
                string addText = n == 0
                    ? PermissionUiHelpers.L("perm_picker_add_selected", "Add Selected", "添加所选")
                    : string.Format(PermissionUiHelpers.L("perm_picker_add_selected_n",
                        "Add Selected ({0})", "添加所选 ({0})"), n);
                if (GUILayout.Button(addText, EditorStyles.miniButton, GUILayout.Width(140)))
                {
                    OnConfirmAdd();
                }
                GUI.enabled = true;

                if (GUILayout.Button(
                    PermissionUiHelpers.L("perm_picker_cancel", "Cancel", "取消"),
                    EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    Close();
                }

                GUILayout.Space(8);
                // using scope auto-closes the HorizontalScope
            }
        }

        private void DrawSkillRow(SkillRouter.SkillInfo skill)
        {
            bool highRisk = IsHighRisk(skill);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(28);

            bool wasSelected = _selected.Contains(skill.Name);
            bool isSelected = EditorGUILayout.Toggle(wasSelected, GUILayout.Width(18));
            if (isSelected != wasSelected)
            {
                if (isSelected) _selected.Add(skill.Name);
                else _selected.Remove(skill.Name);
            }

            var nameStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.87f, 0.87f, 0.87f) },
            };
            EditorGUILayout.LabelField(skill.Name, nameStyle, GUILayout.ExpandWidth(true));

            if (highRisk)
            {
                var tagStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 9,
                    normal = { textColor = Color.white },
                    alignment = TextAnchor.MiddleCenter,
                };
                var tagRect = GUILayoutUtility.GetRect(
                    new GUIContent(PermissionUiHelpers.L("perm_picker_high_risk_tag", "HIGH RISK", "高危")),
                    tagStyle, GUILayout.ExpandWidth(false));
                tagRect.width += 10;
                EditorGUI.DrawRect(tagRect, new Color(0.88f, 0.39f, 0.39f));
                EditorGUI.LabelField(tagRect,
                    PermissionUiHelpers.L("perm_picker_high_risk_tag", "HIGH RISK", "高危"),
                    tagStyle);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void ToggleGroup(List<SkillRouter.SkillInfo> skills, bool select)
        {
            foreach (var s in skills)
            {
                if (select) _selected.Add(s.Name);
                else _selected.Remove(s.Name);
            }
            Repaint();
        }

        private void OnConfirmAdd()
        {
            if (_selected.Count == 0) return;

            var lookup = (_grouped ?? new List<IGrouping<string, SkillRouter.SkillInfo>>())
                .SelectMany(g => g)
                .ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);

            var highRiskNames = _selected
                .Where(n => lookup.TryGetValue(n, out var s) && IsHighRisk(s))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (highRiskNames.Count > 0)
            {
                string list = string.Join("\n  • ", highRiskNames);
                string title = PermissionUiHelpers.L("perm_picker_confirm_title", "Add high-risk skills?", "添加高危 Skill？");
                string msg = string.Format(
                    PermissionUiHelpers.L("perm_picker_confirm_msg",
                        "The following {0} skill(s) are HIGH RISK and would bypass all approval gates:\n\n  • {1}\n\nContinue adding all {2} selected skills?",
                        "以下 {0} 个 skill 属于高危，加入白名单后将绕过所有审批拦截：\n\n  • {1}\n\n继续添加全部 {2} 个所选 skill？"),
                    highRiskNames.Count, list, _selected.Count);
                string ok = PermissionUiHelpers.L("perm_picker_confirm_ok", "Add All", "全部添加");
                string cancel = PermissionUiHelpers.L("perm_picker_confirm_cancel", "Cancel", "取消");
                if (!EditorUtility.DisplayDialog(title, msg, ok, cancel))
                    return;
            }

            foreach (var name in _selected)
                SkillsModeManager.AddToAllowlist(name);

            Close();
        }

        private void SelectCodingAssistPreset()
        {
            var candidateNames = new HashSet<string>(
                (_grouped ?? new List<IGrouping<string, SkillRouter.SkillInfo>>())
                    .SelectMany(g => g).Select(s => s.Name),
                StringComparer.OrdinalIgnoreCase);

            foreach (var name in AllowlistPresets.CodingAssist)
            {
                if (candidateNames.Contains(name)) _selected.Add(name);
            }
            Repaint();
        }

        private void LoadCandidates()
        {
            SkillRouter.SkillInfo[] all = null;
            try { all = SkillRouter.GetAllSkillsSnapshot(); }
            catch (Exception ex) { Debug.LogWarning($"[UnitySkills] Picker snapshot failed: {ex.Message}"); }
            all = all ?? Array.Empty<SkillRouter.SkillInfo>();

            _grouped = all
                .Where(s => s != null && !string.IsNullOrEmpty(s.Name) && !SkillsModeManager.IsInAllowlist(s.Name))
                .GroupBy(s => s.Category.ToString())
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsHighRisk(SkillRouter.SkillInfo s)
        {
            if (s == null) return false;
            return s.Operation.HasFlag(SkillOperation.Delete)
                || s.MayEnterPlayMode
                || s.MayTriggerReload
                || string.Equals(s.RiskLevel, "high", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif

