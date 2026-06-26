#if !UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System;
namespace UnitySkills
{
    /// <summary>
    /// Audit Log Viewer — IMGUI console-style list.
    /// Toolbar (path + Reveal + Refresh) → Filter (search + type dropdown + count)
    /// → ScrollList → Detail (raw JSON).
    /// Entry point: Settings Drawer → Permissions → [View Audit Log].
    /// </summary>
    public sealed class UnitySkillsAuditWindowGUI : EditorWindow
    {
        private const int MaxEntries = 500;

        private static readonly string[] TypeOptions = new[]
        {
            "All",
            "call", "mode_restricted_hit", "mode_changed",
            "grant", "grant_executed", "approve", "deny",
            "allowlist_add", "allowlist_remove", "allowlist_clear", "allowlist_migrated",
            "audit_deleted", "audit_cleared",
            "revoke", "revoke_all",
        };

        private string _logPath = "";
        private string _searchText = "";
        private int _typeFilterIndex = 0;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private int _selectedIdx = -1;

        private readonly List<AuditEntry> _all = new List<AuditEntry>();
        private List<AuditEntry> _filtered = new List<AuditEntry>();

        // Cached styles
        private bool _stylesReady;
        private GUIStyle _iconStyle;
        private GUIStyle _timeStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _summaryNormalStyle;
        private GUIStyle _summarySelectedStyle;
        private GUIStyle _suffixStyle;

        public static void ShowWindow()
        {
            var w = GetWindow<UnitySkillsAuditWindowGUI>(
                PermissionUiHelpers.L("perm_audit_window_title",
                    "UnitySkills Audit Log", "UnitySkills 审计日志"));
            w.minSize = new Vector2(720, 480);
            w.Focus();
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _iconStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10, fontStyle = FontStyle.Bold,
                fixedWidth = 20, alignment = TextAnchor.MiddleCenter,
            };

            _timeStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.63f, 0.63f, 0.63f) },
                fixedWidth = 55, alignment = TextAnchor.MiddleLeft,
            };

            _badgeStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 9,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 16,
            };

            _summaryNormalStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.87f, 0.87f, 0.87f) },
            };

            _summarySelectedStyle = new GUIStyle(_summaryNormalStyle)
            {
                normal = { textColor = Color.white },
            };

            _suffixStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 9,
                normal = { textColor = new Color(0.48f, 0.48f, 0.48f) },
            };
        }

        private void OnEnable()
        {
            Reload();
        }

        private void OnGUI()
        {
            EnsureStyles();

            // ── Toolbar ──
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(24));

            EditorGUILayout.LabelField(
                PermissionUiHelpers.L("perm_log_path_label", "Log:", "日志："),
                GUILayout.Width(35));
            GUI.enabled = false;
            EditorGUILayout.TextField(_logPath, EditorStyles.toolbarTextField, GUILayout.ExpandWidth(true));
            GUI.enabled = true;

            if (GUILayout.Button(
                PermissionUiHelpers.L("perm_open_in_explorer", "Reveal", "在资源管理器中打开"),
                EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_logPath))
                    EditorUtility.RevealInFinder(_logPath);
            }

            if (GUILayout.Button(
                PermissionUiHelpers.L("perm_refresh", "Refresh", "刷新"),
                EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                Reload();
            }

            EditorGUILayout.EndHorizontal();

            // ── Filter bar ──
            EditorGUILayout.BeginHorizontal(GUILayout.Height(22));
            GUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            _searchText = EditorGUILayout.TextField(_searchText, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            if (EditorGUI.EndChangeCheck()) ApplyFilter();

            GUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            _typeFilterIndex = EditorGUILayout.Popup(_typeFilterIndex, TypeOptions, GUILayout.Width(100));
            if (EditorGUI.EndChangeCheck()) ApplyFilter();

            GUILayout.FlexibleSpace();

            var countStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 10,
                normal = { textColor = new Color(0.48f, 0.48f, 0.48f) },
            };
            EditorGUILayout.LabelField(
                string.Format(PermissionUiHelpers.L("perm_audit_count_fmt",
                    "{0} / {1} entries", "{0} / {1} 条"),
                    _filtered.Count, _all.Count),
                countStyle);

            GUILayout.Space(4);

            if (GUILayout.Button(
                PermissionUiHelpers.L("perm_audit_clear_all", "Clear All", "清空全部"),
                EditorStyles.miniButton, GUILayout.Width(80)))
            {
                OnClearAllClicked();
            }

            GUILayout.Space(4);
            EditorGUILayout.EndHorizontal();

            // Divider
            GUILayout.Box("", GUILayout.Height(1), GUILayout.ExpandWidth(true));

            // ── List + Detail split ──
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandHeight(true)))
            {
                // Left: List
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.55f), GUILayout.ExpandHeight(true)))
                {
                    DrawList();
                }

                GUILayout.Box("", GUILayout.Width(1), GUILayout.ExpandHeight(true));

                // Right: Detail
                using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                {
                    DrawDetail();
                }
            }
        }

        private void DrawList()
        {
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll, GUILayout.ExpandHeight(true));

            for (int i = 0; i < _filtered.Count; i++)
            {
                var entry = _filtered[i];
                bool isSelected = i == _selectedIdx;

                var c = GUI.backgroundColor;
                GUI.backgroundColor = isSelected
                    ? new Color(0.17f, 0.36f, 0.53f)
                    : new Color(0.14f, 0.14f, 0.14f);
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox, GUILayout.ExpandWidth(true)))
                {
                    GUI.backgroundColor = c;
                    // Icon
                    var iconStyle = new GUIStyle(_iconStyle)
                    {
                        normal = { textColor = GetBadgeColor(entry.BadgeClass) },
                    };
                    GUILayout.Label(entry.Icon, iconStyle);

                    // Time
                    GUILayout.Label(entry.ShortTime, _timeStyle);

                    // Badge
                    var bgTex = UnitySkillsWindowGUI.MakeColorTex(
                        (byte)(GetBadgeColor(entry.BadgeClass).r * 255),
                        (byte)(GetBadgeColor(entry.BadgeClass).g * 255),
                        (byte)(GetBadgeColor(entry.BadgeClass).b * 255));
                    var bStyle = new GUIStyle(_badgeStyle) { normal = { background = bgTex } };
                    var size = bStyle.CalcSize(new GUIContent(entry.BadgeText));
                    GUILayout.Box(entry.BadgeText, bStyle, GUILayout.Width(size.x + 10));

                    // Summary
                    var sumStyle = isSelected ? _summarySelectedStyle : _summaryNormalStyle;
                    GUILayout.Label(entry.Summary, sumStyle, GUILayout.ExpandWidth(true));

                    // Suffix
                    GUILayout.Label(entry.Suffix, _suffixStyle);
                }

                // Click to select
                var lastRect = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.MouseDown && lastRect.Contains(Event.current.mousePosition))
                {
                    _selectedIdx = i;
                    Repaint();
                    Event.current.Use();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawDetail()
        {
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll, GUILayout.ExpandHeight(true));

            if (_selectedIdx < 0 || _selectedIdx >= _filtered.Count)
            {
                GUILayout.FlexibleSpace();
                var hintStyle = new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(0.48f, 0.48f, 0.48f) },
                    wordWrap = true,
                };
                EditorGUILayout.LabelField(
                    PermissionUiHelpers.L("perm_audit_select_hint",
                        "Select an entry to view raw JSON",
                        "选择一行查看原始 JSON"),
                    hintStyle);
                GUILayout.FlexibleSpace();
            }
            else
            {
                var entry = _filtered[_selectedIdx];
                var titleStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    normal = { textColor = new Color(0.87f, 0.87f, 0.87f) },
                };
                EditorGUILayout.LabelField($"[{entry.ShortTime}]  {entry.Type}", titleStyle);
                GUILayout.Space(8);

                var jsonText = PrettifyJson(entry.RawJson);
                var jsonStyle = new GUIStyle(EditorStyles.textArea)
                {
                    fontSize = 11,
                    wordWrap = true,
                };
                EditorGUILayout.TextArea(jsonText, jsonStyle, GUILayout.ExpandHeight(true));
            }

            EditorGUILayout.EndScrollView();
        }

        // ── Data ──

        private void Reload()
        {
            try { _logPath = SkillsAuditLog.GetLogPath() ?? ""; }
            catch (Exception ex) { _logPath = $"<{ex.Message}>"; }

            _all.Clear();
            try
            {
                var raw = SkillsAuditLog.ReadRecent(MaxEntries);
                if (raw != null)
                {
                    foreach (var item in raw)
                    {
                        var entry = ParseEntry(item as Newtonsoft.Json.Linq.JObject);
                        if (entry != null) _all.Add(entry);
                    }
                }
                _all.Reverse(); // newest first
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"AuditLog UI reload failed: {ex.Message}");
            }
            _selectedIdx = -1;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string q = _searchText.Trim();
            string type = TypeOptions[_typeFilterIndex];
            bool typeAll = string.Equals(type, "All", StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(q) && typeAll)
            {
                _filtered = new List<AuditEntry>(_all);
            }
            else
            {
                var qLower = q.ToLowerInvariant();
                _filtered = _all.Where(e =>
                {
                    if (!typeAll && !string.Equals(e.Type, type, StringComparison.OrdinalIgnoreCase)) return false;
                    if (qLower.Length == 0) return true;
                    return ContainsIgnoreCase(e.Skill, qLower)
                        || ContainsIgnoreCase(e.GrantToken, qLower)
                        || ContainsIgnoreCase(e.Token, qLower)
                        || ContainsIgnoreCase(e.ArgsSummary, qLower)
                        || ContainsIgnoreCase(e.RawJson, qLower);
                }).ToList();
            }

            _selectedIdx = -1;
            Repaint();
        }

        private static bool ContainsIgnoreCase(string s, string qLower)
        {
            return !string.IsNullOrEmpty(s) && s.ToLowerInvariant().Contains(qLower);
        }

        private static string PrettifyJson(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            try
            {
                var tok = Newtonsoft.Json.Linq.JToken.Parse(raw);
                return tok.ToString(Newtonsoft.Json.Formatting.Indented);
            }
            catch { return raw; }
        }

        // ── Entry parsing (same logic as UI Toolkit version) ──

        private sealed class AuditEntry
        {
            public string Ts;
            public string ShortTime;
            public string Type;
            public string Skill;
            public string Mode;
            public string SkillMode;
            public string Result;
            public string GrantToken;
            public string Token;
            public string Channel;
            public string Source;
            public string ArgsSummary;
            public int? TokenAgeSec;
            public int? Count;
            public string RawJson;
            public string Icon;
            public string BadgeText;
            public string BadgeClass;
            public string Summary;
            public string Suffix;
        }

        private static AuditEntry ParseEntry(Newtonsoft.Json.Linq.JObject obj)
        {
            if (obj == null) return null;
            var e = new AuditEntry
            {
                Ts          = obj["ts"]?.ToString(),
                Type        = obj["type"]?.ToString(),
                Skill       = obj["skill"]?.ToString(),
                Mode        = obj["mode"]?.ToString(),
                SkillMode   = obj["skillMode"]?.ToString(),
                Result      = obj["result"]?.ToString(),
                GrantToken  = obj["grantToken"]?.ToString(),
                Token       = obj["token"]?.ToString(),
                Channel     = obj["channel"]?.ToString(),
                Source      = obj["source"]?.ToString(),
                ArgsSummary = obj["argsSummary"]?.ToString(),
                TokenAgeSec = (int?)obj["tokenAgeSec"],
                Count       = (int?)obj["count"],
                RawJson     = obj.ToString(Newtonsoft.Json.Formatting.None),
            };
            e.ShortTime = FormatShortTime(e.Ts);
            ApplyTypeStyle(e);
            e.Summary = BuildSummary(e);
            e.Suffix = BuildSuffix(e);
            return e;
        }

        private static string FormatShortTime(string isoTs)
        {
            if (string.IsNullOrEmpty(isoTs)) return "";
            if (DateTime.TryParse(isoTs, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            {
                return dt.ToLocalTime().ToString("HH:mm:ss");
            }
            return isoTs.Length >= 19 ? isoTs.Substring(11, 8) : isoTs;
        }

        private static void ApplyTypeStyle(AuditEntry e)
        {
            switch (e.Type)
            {
                case "call":
                    if (e.Result == "allowed")
                    { e.Icon = ">"; e.BadgeText = "CALL ALLOW";    e.BadgeClass = "allow"; }
                    else if (e.Result == "restricted")
                    { e.Icon = "!"; e.BadgeText = "CALL RESTRICT"; e.BadgeClass = "restricted"; }
                    else if (e.Result == "forbidden")
                    { e.Icon = "x"; e.BadgeText = "CALL FORBID";   e.BadgeClass = "deny"; }
                    else
                    { e.Icon = "*"; e.BadgeText = "CALL";          e.BadgeClass = "other"; }
                    break;
                case "mode_restricted_hit": e.Icon = "!"; e.BadgeText = "RESTRICTED"; e.BadgeClass = "restricted"; break;
                case "mode_changed":        e.Icon = "M"; e.BadgeText = "MODE";       e.BadgeClass = "mode";       break;
                case "grant":               e.Icon = "+"; e.BadgeText = "GRANT";      e.BadgeClass = "grant";      break;
                case "grant_executed":      e.Icon = ">"; e.BadgeText = "GRANT EXEC"; e.BadgeClass = "grant";      break;
                case "approve":             e.Icon = "+"; e.BadgeText = "APPROVE";    e.BadgeClass = "grant";      break;
                case "deny":                e.Icon = "x"; e.BadgeText = "DENY";       e.BadgeClass = "deny";       break;
                case "allowlist_add":       e.Icon = "+"; e.BadgeText = "ALLOW +";    e.BadgeClass = "allow";      break;
                case "allowlist_remove":    e.Icon = "-"; e.BadgeText = "ALLOW -";    e.BadgeClass = "revoke";     break;
                case "allowlist_clear":     e.Icon = "C"; e.BadgeText = "ALLOW CLR";  e.BadgeClass = "revoke";     break;
                case "allowlist_migrated":  e.Icon = "^"; e.BadgeText = "MIGRATED";   e.BadgeClass = "mode";       break;
                case "audit_deleted":       e.Icon = "x"; e.BadgeText = "AUDIT DEL";  e.BadgeClass = "revoke";     break;
                case "audit_cleared":       e.Icon = "X"; e.BadgeText = "AUDIT CLR";  e.BadgeClass = "deny";       break;
                case "revoke":              e.Icon = "<"; e.BadgeText = "REVOKE";     e.BadgeClass = "revoke";     break;
                case "revoke_all":          e.Icon = "<<";e.BadgeText = "REVOKE ALL"; e.BadgeClass = "revoke";     break;
                default:
                    e.Icon = "*";
                    e.BadgeText = e.Type?.ToUpperInvariant() ?? "?";
                    e.BadgeClass = "other";
                    break;
            }
        }

        private static string BuildSummary(AuditEntry e)
        {
            switch (e.Type)
            {
                case "mode_changed": return $"-> {e.Mode ?? "?"}";
                case "revoke_all":   return $"{(e.Count?.ToString() ?? "?")} skills";
                default:             return string.IsNullOrEmpty(e.Skill) ? "" : e.Skill;
            }
        }

        private static string BuildSuffix(AuditEntry e)
        {
            var parts = new List<string>();
            if (e.Type == "call" && !string.IsNullOrEmpty(e.Mode))
                parts.Add($"{e.Mode}/{e.SkillMode ?? "?"}");
            if (!string.IsNullOrEmpty(e.GrantToken))
                parts.Add($"#{ShortTokenLocal(e.GrantToken)}");
            if (!string.IsNullOrEmpty(e.Token))
                parts.Add($"#{ShortTokenLocal(e.Token)}");
            if (!string.IsNullOrEmpty(e.Channel))
                parts.Add(e.Channel);
            if (!string.IsNullOrEmpty(e.Source))
                parts.Add(e.Source);
            if (e.TokenAgeSec.HasValue)
                parts.Add($"{e.TokenAgeSec}s");
            return string.Join(" · ", parts);
        }

        private static string ShortTokenLocal(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            return t.Length <= 8 ? t : t.Substring(0, 8) + "…";
        }

        private static Color GetBadgeColor(string badgeClass)
        {
            switch (badgeClass)
            {
                case "allow":      return new Color(0.43f, 0.77f, 0.43f);
                case "restricted": return new Color(0.88f, 0.69f, 0.29f);
                case "deny":       return new Color(0.88f, 0.39f, 0.39f);
                case "mode":       return new Color(0.29f, 0.62f, 1f);
                case "grant":      return new Color(0.43f, 0.77f, 0.43f);
                case "revoke":     return new Color(0.84f, 0.50f, 0.50f);
                default:           return new Color(0.48f, 0.48f, 0.48f);
            }
        }

        // ── Clear actions ──

        private void OnClearAllClicked()
        {
            string title = PermissionUiHelpers.L("perm_audit_clear_all", "Clear All", "清空全部");
            string msg = PermissionUiHelpers.L("perm_audit_clear_all_confirm",
                "Permanently delete the entire audit log (including rotated history files)?\n\nThis cannot be undone. The wipe itself will be recorded as a fresh 'audit_cleared' entry.",
                "确定永久删除整个审计日志（含历史滚动文件）吗？\n\n该操作不可撤销。清空动作本身会作为新的 audit_cleared 事件留痕。");
            if (!EditorUtility.DisplayDialog(title, msg,
                    PermissionUiHelpers.L("perm_audit_clear_ok", "Clear All", "清空"),
                    PermissionUiHelpers.L("perm_audit_delete_cancel", "Cancel", "取消")))
                return;

            try
            {
                SkillsAuditLog.ClearAll();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Error", ex.Message, "OK");
            }
            Reload();
        }

        private static Texture2D MakeTex(byte r, byte g, byte b)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, new Color32(r, g, b, 255));
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
#endif

