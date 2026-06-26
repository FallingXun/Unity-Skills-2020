#if !UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor;
using System;
namespace UnitySkills
{
    /// <summary>
    /// Pending Approval Banner — shown between topbar and tabs.
    /// Rect-based — caller provides the bar area. Returns measured height.
    /// </summary>
    public class PendingApprovalBannerControllerGUI
    {
        private const int MaxRows = 3;
        private const float RowH = 60f;
        private readonly UnitySkillsWindowGUI _window;
        public PendingApprovalBannerControllerGUI(UnitySkillsWindowGUI w) { _window = w; }

        /// <summary>Returns the banner height needed this frame (0 if hidden).</summary>
        public float MeasureHeight()
        {
            var p = SkillsModeManager.PendingGrantRequests;
            if (p.Count == 0 || SkillsModeManager.CurrentMode != SkillsOperatingMode.Approval) return 0;
            return 28 + Math.Min(p.Count, MaxRows) * (RowH + 2) + 6;
        }

        public void OnGUI(Rect r)
        {
            var pending = SkillsModeManager.PendingGrantRequests;
            bool show = pending.Count > 0 && SkillsModeManager.CurrentMode == SkillsOperatingMode.Approval;
            if (!show) return;

            EditorGUI.DrawRect(r, new Color(0.31f, 0.24f, 0.10f));

            float y = r.y + 4;

            // Header
            GUI.color = new Color(1f, 0.86f, 0.55f);
            GUI.Label(new Rect(r.x + 8, y, r.width - 120, 20),
                string.Format("🔐  {0} pending approval(s)", pending.Count), EditorStyles.boldLabel);
            GUI.color = Color.white;

            if (GUI.Button(new Rect(r.xMax - 120, y, 112, 20),
                PermissionUiHelpers.L("pending_banner_open_settings", "Open Permissions", "打开权限设置"),
                EditorStyles.miniButton))
                _window.OpenSettings();
            y += 24;

            // Cards
            int shown = Math.Min(pending.Count, MaxRows);
            for (int i = 0; i < shown; i++)
            {
                var cr = new Rect(r.x + 8, y, r.width - 16, RowH);
                DrawCard(cr, pending[i]);
                y += RowH + 2;
            }

            if (pending.Count > shown)
            {
                GUI.color = new Color(0.78f, 0.71f, 0.55f);
                GUI.Label(new Rect(r.x, y, r.width, 16),
                    string.Format("+{0} more — open Permissions", pending.Count - shown),
                    EditorStyles.centeredGreyMiniLabel);
                GUI.color = Color.white;
            }
        }

        private static void DrawCard(Rect r, GrantRequest req)
        {
            EditorGUI.DrawRect(r, new Color(0.22f, 0.16f, 0.09f));
            EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), new Color(0.86f, 0.67f, 0.24f));

            float y = r.y + 4;

            GUI.Label(new Rect(r.x + 12, y, r.width - 80, 18),
                $"{req.SkillName}  ({req.Channel})  #{PermissionUiHelpers.ShortToken(req.Token)}",
                EditorStyles.boldLabel);

            GUI.color = new Color(0.78f, 0.71f, 0.55f);
            GUI.Label(new Rect(r.xMax - 70, y, 60, 18),
                PermissionUiHelpers.FormatCountdown(req.ExpiresAtUtc), EditorStyles.miniLabel);
            GUI.color = Color.white;
            y += 20;

            if (!string.IsNullOrEmpty(req.ArgsSummary))
            {
                GUI.Label(new Rect(r.x + 12, y, r.width - 20, 14),
                    $"args: {req.ArgsSummary}", EditorStyles.miniLabel);
                y += 16;
            }

            bool isPanel = req.Channel == "panel";
            if (isPanel && req.ApprovedByPanel)
                GUI.Label(new Rect(r.x + 12, y, r.width - 20, 14),
                    PermissionUiHelpers.L("perm_approved_waiting", "Approved · waiting for AI to execute", "已批准 · 等待 AI 执行"), EditorStyles.miniLabel);
            else if (!isPanel)
                GUI.Label(new Rect(r.x + 12, y, r.width - 20, 14),
                    PermissionUiHelpers.L("perm_approve_in_chat", "Dialog channel — approve in the AI chat", "对话渠道 · 请在 AI 对话中批准"), EditorStyles.miniLabel);

            // Actions — bottom-right
            float ay = r.yMax - 22;
            if (GUI.Button(new Rect(r.xMax - 120, ay, 55, 18),
                PermissionUiHelpers.L("perm_approve", "Approve", "批准"), EditorStyles.miniButton))
                SkillsModeManager.Approve(req.Token);
            if (GUI.Button(new Rect(r.xMax - 58, ay, 48, 18),
                PermissionUiHelpers.L("perm_deny", "Deny", "拒绝"), EditorStyles.miniButton))
                SkillsModeManager.Deny(req.Token);
        }

        public void RefreshLocalization() { }
    }
}
#endif

