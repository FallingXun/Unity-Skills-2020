#if !UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System;
namespace UnitySkills
{
    /// <summary>
    /// Settings Drawer — slide-in right panel. Rect-based.
    /// </summary>
    public class SettingsDrawerControllerGUI
    {
        private static readonly SkillsOperatingMode[] ModeOrder = { SkillsOperatingMode.Approval, SkillsOperatingMode.Auto, SkillsOperatingMode.Bypass };
        private static readonly string[] ModeNames = { "Approval", "Auto", "Bypass" };
        private static readonly string[] PortOptions = { "Auto", "8090", "8091", "8092", "8093", "8094", "8095", "8096", "8097", "8098", "8099", "8100" };
        private static readonly string[] LogOptions = { "Off", "Error", "Warning", "Info", "Agent", "Verbose" };

        private readonly UnitySkillsWindowGUI _window;
        private Vector2 _scroll;
        private bool _isOpen, _allowlistFoldout;

        public bool IsOpen => _isOpen;
        public SettingsDrawerControllerGUI(UnitySkillsWindowGUI w) { _window = w; }
        public void Open() => _isOpen = true;
        public void Close() => _isOpen = false;

        public void OnGUI(Rect r)
        {
            if (!_isOpen) return;
            EditorGUI.DrawRect(r, new Color(0.15f, 0.15f, 0.15f));

            // Header
            var hdr = new Rect(r.x, r.y, r.width, 28);
            EditorGUI.DrawRect(hdr, new Color(0.11f, 0.11f, 0.11f));
            GUI.Label(new Rect(r.x + 8, r.y + 4, r.width - 40, 20), SkillsLocalization.Get("drawer_settings_title"), EditorStyles.boldLabel);
            if (GUI.Button(new Rect(r.xMax - 32, r.y + 2, 28, 24), "✕", EditorStyles.miniButton)) Close();

            // Body
            float by = r.y + 30;
            float bh = r.yMax - by;
            float cw = r.width - 16;

            float totalH = PermissionsGroupH() + ServerGroupH() + RuntimeGroupH() + StatsGroupH() + 40;
            _scroll = GUI.BeginScrollView(new Rect(r.x, by, r.width, bh), _scroll, new Rect(0, 0, cw, totalH));
            float sy = 4;
            sy = DrawPermissions(0, cw, sy);
            sy = DrawServer(0, cw, sy);
            sy = DrawRuntime(0, cw, sy);
            sy = DrawStats(0, cw, sy);
            GUI.EndScrollView();
        }

        // ── Height estimation helpers ──

        private static float SectionH(string title, float contentH) => 28 + contentH;
        private float PermissionsGroupH()
        {
            float h = 28;
            h += 24; // mode row
            h += 30; // hint
            var mode = SkillsModeManager.CurrentMode;
            if (mode == SkillsOperatingMode.Approval) h += 60;
            var p = SkillsModeManager.PendingGrantRequests;
            if (mode == SkillsOperatingMode.Approval && p.Count > 0) h += 26 + p.Count * 70;
            if (mode != SkillsOperatingMode.Bypass)
            {
                var al = SkillsModeManager.AllowlistSkills;
                h += 30;
                if (_allowlistFoldout && al.Count > 0) h += al.Count * 22;
            }
            h += 30; // audit btn
            return h + 8;
        }
        private float ServerGroupH() => 28 + 60 + 24 * 3 + 20 + 8;
        private float RuntimeGroupH() => 28 + 24 + 40 + 8;
        private float StatsGroupH() => 28 + 20 + 26 + 8;

        // ── Permissions ──

        private float DrawPermissions(float x, float w, float y)
        {
            y = DrawSectionTitle(x, w, y, PermissionUiHelpers.L("drawer_section_permissions", "Permissions", "权限"));
            var mode = SkillsModeManager.CurrentMode;

            // Mode dropdown
            int mi = Array.IndexOf(ModeOrder, mode); if (mi < 0) mi = 1;
            GUI.Label(new Rect(x + 4, y, 110, 20), "Operating Mode", EditorStyles.label);
            int nmi = EditorGUI.Popup(new Rect(x + 116, y, 120, 20), mi, ModeNames);
            if (nmi != mi && nmi >= 0 && nmi < ModeOrder.Length) SkillsModeManager.CurrentMode = ModeOrder[nmi];
            y += 24;

            string hint = mode switch {
                SkillsOperatingMode.Approval => "AI must ask before invoking FullAuto (per-skill grant).",
                SkillsOperatingMode.Auto     => "AI decides on its own. Server blocks high-risk only.",
                SkillsOperatingMode.Bypass   => "All skills pass through.",
                _ => ""
            };
            GUI.Label(new Rect(x + 4, y, w - 8, 24), hint, EditorStyles.wordWrappedMiniLabel);
            y += 24;

            // Panel Approval
            if (mode == SkillsOperatingMode.Approval)
            {
                bool pa = SkillsModeManager.PanelApprovalRequired;
                bool np = EditorGUI.Toggle(new Rect(x + 4, y, w - 8, 16), "Require Panel Approval", pa);
                if (np != pa) SkillsModeManager.PanelApprovalRequired = np;
                y += 20;
                GUI.Label(new Rect(x + 20, y, w - 24, 30), "When checked, grant tokens must be Approved here; otherwise verbal consent is enough.", EditorStyles.wordWrappedMiniLabel);
                y += 34;
            }

            // Pending
            var pending = SkillsModeManager.PendingGrantRequests;
            if (mode == SkillsOperatingMode.Approval && pending.Count > 0)
            {
                GUI.Label(new Rect(x + 4, y, w - 8, 20),
                    string.Format("Pending Grant Requests ({0})", pending.Count), EditorStyles.boldLabel);
                y += 22;
                foreach (var req in pending) { y = DrawPendingCard(x, w, y, req); }
            }

            // Allowlist
            if (mode != SkillsOperatingMode.Bypass)
            {
                var al = SkillsModeManager.AllowlistSkills;
                _allowlistFoldout = EditorGUI.Foldout(new Rect(x + 4, y, w - 8, 18), _allowlistFoldout,
                    string.Format("Allowlist Skills ({0})", al.Count), true);
                y += 22;
                if (_allowlistFoldout)
                {
                    if (GUI.Button(new Rect(x + 4, y, w / 2 - 6, 20), "+ Add Skill", EditorStyles.miniButton))
                        AllowlistPickerWindowGUI.Open();
                    GUI.enabled = al.Count > 0;
                    if (GUI.Button(new Rect(x + w / 2 + 2, y, w / 2 - 6, 20), "Clear All", EditorStyles.miniButton))
                        SkillsModeManager.ClearAllowlist();
                    GUI.enabled = true;
                    y += 24;

                    if (al.Count == 0)
                        GUI.Label(new Rect(x + 4, y, w - 8, 16), "No allowlisted skills.", EditorStyles.miniLabel);
                    else
                        y = DrawAllowlist(x, w, y, al);
                }
            }

            y += 4;
            if (GUI.Button(new Rect(x + 4, y, w - 8, 22), "View Audit Log", EditorStyles.miniButton))
                UnitySkillsAuditWindowGUI.ShowWindow();
            return y + 30;
        }

        private static float DrawPendingCard(float x, float w, float y, GrantRequest req)
        {
            var r = new Rect(x, y, w, 64);
            EditorGUI.DrawRect(r, new Color(0.20f, 0.20f, 0.20f));

            GUI.Label(new Rect(x + 8, y + 4, w - 40, 18),
                $"{req.SkillName}  ({req.Channel})  #{PermissionUiHelpers.ShortToken(req.Token)}", EditorStyles.boldLabel);
            GUI.Label(new Rect(x + w - 70, y + 4, 62, 18), PermissionUiHelpers.FormatCountdown(req.ExpiresAtUtc), EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(req.ArgsSummary))
                GUI.Label(new Rect(x + 8, y + 22, w - 16, 14), $"args: {req.ArgsSummary}", EditorStyles.miniLabel);

            bool isPanel = req.Channel == "panel";
            if (isPanel && req.ApprovedByPanel)
                GUI.Label(new Rect(x + 8, y + 34, w - 16, 14), "Approved · waiting for AI to execute", EditorStyles.miniLabel);
            else if (!isPanel)
                GUI.Label(new Rect(x + 8, y + 34, w - 16, 14), "Dialog channel — approve in the AI chat", EditorStyles.miniLabel);

            GUI.enabled = isPanel && !req.ApprovedByPanel;
            if (GUI.Button(new Rect(x + w - 124, y + 44, 55, 16), "Approve", EditorStyles.miniButton))
                SkillsModeManager.Approve(req.Token);
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + w - 64, y + 44, 55, 16), "Deny", EditorStyles.miniButton))
                SkillsModeManager.Deny(req.Token);

            return y + 68;
        }

        private static float DrawAllowlist(float x, float w, float y, IReadOnlyCollection<string> al)
        {
            var nc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try { foreach (var s in SkillRouter.GetAllSkillsSnapshot() ?? Array.Empty<SkillRouter.SkillInfo>()) if (s != null && !string.IsNullOrEmpty(s.Name)) nc[s.Name] = s.Category.ToString(); } catch { }
            foreach (var g in al.GroupBy(n => nc.TryGetValue(n, out var c) ? c : "(Unknown)").OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                GUI.Label(new Rect(x + 12, y, w - 16, 18), $"{g.Key}  ({g.Count()})", EditorStyles.boldLabel);
                y += 20;
                foreach (var n in g.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                {
                    GUI.Label(new Rect(x + 20, y, w - 80, 18), n, EditorStyles.label);
                    if (GUI.Button(new Rect(x + w - 60, y, 54, 16), "Remove", EditorStyles.miniButton))
                        SkillsModeManager.RemoveFromAllowlist(n);
                    y += 20;
                }
            }
            return y + 4;
        }

        // ── Server ──

        private float DrawServer(float x, float w, float y)
        {
            y = DrawSectionTitle(x, w, y, SkillsLocalization.Get("drawer_section_server"));

            bool a = SkillsHttpServer.AutoStart;
            bool na = EditorGUI.Toggle(new Rect(x + 4, y, w - 8, 16), SkillsLocalization.Get("auto_restart"), a);
            if (na != a) SkillsHttpServer.AutoStart = na;
            y += 22;
            GUI.Label(new Rect(x + 20, y, w - 24, 16), SkillsLocalization.Get("auto_restart_hint"), EditorStyles.miniLabel);
            y += 22;

            // Port
            GUI.Label(new Rect(x + 4, y, 80, 20), SkillsLocalization.Get("drawer_port_label"));
            int cp = SkillsHttpServer.PreferredPort, pi = cp == 0 ? 0 : cp - 8089;
            if (pi < 0 || pi >= PortOptions.Length) pi = 0;
            int np = EditorGUI.Popup(new Rect(x + 86, y, 90, 20), pi, PortOptions);
            if (np != pi) SkillsHttpServer.PreferredPort = np <= 0 ? 0 : 8089 + np;
            y += 24;

            // Timeout
            GUI.Label(new Rect(x + 4, y, 80, 20), SkillsLocalization.Get("drawer_timeout_label"));
            int t = SkillsHttpServer.RequestTimeoutMinutes;
            int nt = EditorGUI.IntField(new Rect(x + 86, y, 50, 20), t);
            if (nt != t) SkillsHttpServer.RequestTimeoutMinutes = nt;
            GUI.Label(new Rect(x + 140, y, 30, 20), SkillsLocalization.Get("timeout_unit"), EditorStyles.miniLabel);
            y += 24;

            // KeepAlive
            GUI.Label(new Rect(x + 4, y, 80, 20), SkillsLocalization.Get("drawer_keepalive_label"));
            int k = SkillsHttpServer.KeepAliveIntervalSeconds;
            int nk = EditorGUI.IntField(new Rect(x + 86, y, 50, 20), k);
            if (nk != k) SkillsHttpServer.KeepAliveIntervalSeconds = nk;
            GUI.Label(new Rect(x + 140, y, 30, 20), SkillsLocalization.Get("keepalive_unit"), EditorStyles.miniLabel);
            y += 24;
            GUI.Label(new Rect(x + 4, y, w - 8, 30), SkillsLocalization.Get("keepalive_hint"), EditorStyles.wordWrappedMiniLabel);
            return y + 34;
        }

        // ── Runtime ──

        private float DrawRuntime(float x, float w, float y)
        {
            y = DrawSectionTitle(x, w, y, SkillsLocalization.Get("drawer_section_runtime"));

            GUI.Label(new Rect(x + 4, y, 80, 20), SkillsLocalization.Get("drawer_loglevel_label"));
            int ll = (int)SkillsLogger.Level; if (ll < 0 || ll >= LogOptions.Length) ll = 0;
            int nl = EditorGUI.Popup(new Rect(x + 86, y, 90, 20), ll, LogOptions);
            if (nl != ll) SkillsLogger.Level = (LogLevel)nl;
            y += 24;

            bool cf = ConfirmationTokenService.RequireConfirmation;
            bool nc = EditorGUI.Toggle(new Rect(x + 4, y, w - 8, 16), SkillsLocalization.Get("drawer_confirm_label"), cf);
            if (nc != cf) ConfirmationTokenService.RequireConfirmation = nc;
            y += 20;
            string ch = SkillsLocalization.Current == SkillsLocalization.Language.Chinese
                ? "开启后：删除类高风险技能首次调用返回 _confirm token + dryRun 预览。"
                : "When ON: delete/high-risk skills return _confirm + dryRun; re-call with token to execute.";
            GUI.Label(new Rect(x + 20, y, w - 24, 30), ch, EditorStyles.wordWrappedMiniLabel);
            return y + 34;
        }

        // ── Stats ──

        private float DrawStats(float x, float w, float y)
        {
            y = DrawSectionTitle(x, w, y, SkillsLocalization.Get("drawer_section_stats"));
            GUI.Label(new Rect(x + 4, y, w - 8, 16), SkillsLocalization.Get("drawer_stats_hint"), EditorStyles.miniLabel);
            y += 20;
            if (GUI.Button(new Rect(x + 4, y, w - 8, 22), SkillsLocalization.Get("drawer_reset_stats_btn"), EditorStyles.miniButton))
                SkillsHttpServer.ResetStatistics();
            return y + 26;
        }

        private static float DrawSectionTitle(float x, float w, float y, string title)
        {
            y += 4;
            GUI.Label(new Rect(x + 2, y, w - 4, 16), title, EditorStyles.boldLabel);
            y += 18;
            EditorGUI.DrawRect(new Rect(x, y, w, 1), new Color(0.20f, 0.20f, 0.20f));
            return y + 6;
        }

        public void RefreshLocalization() { }
    }
}
#endif

