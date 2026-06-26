#if !UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor;
using System;
namespace UnitySkills
{
    /// <summary>
    /// Topbar — server dot | URL pill | toggle | status | perm badge | gear.
    /// Rect-based — caller provides the bar area.
    /// </summary>
    public class TopbarControllerGUI
    {
        private const float CompactW = 380, NarrowW = 300;
        private readonly UnitySkillsWindowGUI _window;

        public TopbarControllerGUI(UnitySkillsWindowGUI w) { _window = w; }

        public void OnGUI(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.11f, 0.11f, 0.11f));
            bool running = SkillsHttpServer.IsRunning;
            string url = running ? (SkillsHttpServer.Url ?? "") : "";
            float w = r.width;
            bool compact = w < CompactW, narrow = w < NarrowW;

            float x = r.x + 8;
            float cy = r.y + (r.height - 10) / 2; // center y for 10px dot

            // Status dot
            if (!narrow)
            {
                var dr = new Rect(x, cy, 10, 10);
                EditorGUI.DrawRect(dr, running ? new Color(0.43f, 0.77f, 0.43f) : new Color(0.88f, 0.39f, 0.39f));
                x += 18;
            }

            // URL pill
            if (!narrow)
            {
                x += 4;
                var pw = compact ? 70 : r.xMax - x - 200; // rough: leave room for rest
                if (compact) pw = 70;
                else pw = Mathf.Max(80, r.xMax - x - 180);

                var pr = new Rect(x, r.y + 7, pw, 22);
                EditorGUI.DrawRect(pr, new Color(0.12f, 0.12f, 0.12f));

                if (!compact)
                {
                    var tf = new Rect(pr.x + 8, pr.y + 3, pr.width - 74, 16);
                    GUI.SetNextControlName("url-pill");
                    GUI.enabled = false;
                    GUI.TextField(tf, running ? url : "");
                    GUI.enabled = true;

                    var cb = new Rect(tf.xMax + 4, pr.y + 3, 58, 16);
                    if (GUI.Button(cb, "Copy", EditorStyles.miniButton))
                        if (!string.IsNullOrEmpty(SkillsHttpServer.Url))
                            EditorGUIUtility.systemCopyBuffer = SkillsHttpServer.Url;
                }
                else
                {
                    var cb = new Rect(pr.x + pr.width - 62, pr.y + 3, 58, 16);
                    if (GUI.Button(cb, "Copy", EditorStyles.miniButton))
                        if (!string.IsNullOrEmpty(SkillsHttpServer.Url))
                            EditorGUIUtility.systemCopyBuffer = SkillsHttpServer.Url;
                }
                x = pr.xMax + 6;
            }

            // Server toggle
            var sr = new Rect(x, r.y + 9, 32, 18);
            EditorGUI.DrawRect(sr, running ? new Color(0.29f, 0.62f, 1f) : new Color(0.24f, 0.24f, 0.24f));
            EditorGUI.DrawRect(new Rect(sr.x + (running ? 16 : 2), sr.y + 2, 14, 14), new Color(0.94f, 0.94f, 0.94f));
            if (Event.current.type == EventType.MouseDown && sr.Contains(Event.current.mousePosition))
            { if (running) SkillsHttpServer.StopPermanent(); else SkillsHttpServer.Start(SkillsHttpServer.PreferredPort); Event.current.Use(); }
            x = sr.xMax + 6;

            // Status text
            string st = SkillsLocalization.Get(running ? "topbar_running" : "topbar_stopped");
            GUI.color = running ? new Color(0.43f, 0.77f, 0.43f) : new Color(0.48f, 0.48f, 0.48f);
            GUI.Label(new Rect(x, r.y, compact ? 42 : 50, r.height), st, EditorStyles.miniLabel);
            GUI.color = Color.white;
            x += compact ? 44 : 52;

            // Flexible space → push badge + gear to right
            float badgeW = 100, gearW = 28;
            float rightEdge = r.xMax - 8;

            // Gear
            var gIcon = EditorGUIUtility.IconContent("d_SettingsIcon")?.image
                     ?? EditorGUIUtility.IconContent("SettingsIcon")?.image;
            if (GUI.Button(new Rect(rightEdge - gearW, r.y + 6, gearW, 24),
                gIcon != null ? new GUIContent(gIcon, SkillsLocalization.Get("topbar_settings_tooltip"))
                              : new GUIContent("⚙", SkillsLocalization.Get("topbar_settings_tooltip"))))
                _window.OpenSettings();

            rightEdge -= gearW + 4;

            // Perm badge
            var mode = SkillsModeManager.CurrentMode;
            string label = mode switch
            {
                SkillsOperatingMode.Approval => $"● Approval" + (SkillsModeManager.PendingGrantRequests.Count > 0 ? $" ({SkillsModeManager.PendingGrantRequests.Count})" : ""),
                SkillsOperatingMode.Auto     => "● Auto",
                SkillsOperatingMode.Bypass   => "● Bypass",
                _ => "● " + mode
            };
            if (GUI.Button(new Rect(rightEdge - badgeW, r.y + 7, badgeW, 22),
                new GUIContent(label, "Click to switch operating mode"), EditorStyles.miniButton))
                ShowModeDropdown();
        }

        private void ShowModeDropdown()
        {
            var m = new GenericMenu(); var c = SkillsModeManager.CurrentMode;
            Add(m, SkillsOperatingMode.Approval, c, "Approval（审批）");
            Add(m, SkillsOperatingMode.Auto,     c, "Auto（自动）");
            Add(m, SkillsOperatingMode.Bypass,   c, "Bypass（直接放行）");
            m.AddSeparator("");
            m.AddItem(new GUIContent("Open Permission Settings…"), false, () => _window.OpenSettings());
            m.ShowAsContext();
        }
        private static void Add(GenericMenu m, SkillsOperatingMode mo, SkillsOperatingMode cur, string l)
        { m.AddItem(new GUIContent(l), mo == cur, () => { if (SkillsModeManager.CurrentMode != mo) SkillsModeManager.CurrentMode = mo; }); }

        public void RefreshLocalization() { }
    }
}
#endif

