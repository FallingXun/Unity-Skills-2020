#if !UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
namespace UnitySkills
{
    /// <summary>
    /// AI Config Tab — agent install/uninstall cards. Rect-based.
    /// </summary>
    public class AIConfigTabControllerGUI
    {
        private const string IconsFolder = "Packages/com.besty.unity-skills/Editor/UI/Icons";
        private readonly UnitySkillsWindowGUI _window;
        private string _customPath = "", _customName = "Custom";
        private Vector2 _scroll;
        private List<AgentConfig> _configs;

        private class AgentConfig
        {
            public string id, nameDisplay;
            public Func<bool> isProjInstalled, isGlobInstalled;
            public Func<bool, (bool success, string message)> install, uninstall;
            public Func<bool, string, string> successMsg;
        }

        public AIConfigTabControllerGUI(UnitySkillsWindowGUI w)
        {
            _window = w;
            _configs = new List<AgentConfig>
            {
                new AgentConfig { id = "claudecode", nameDisplay = "Claude Code",   isProjInstalled = () => SkillInstaller.IsClaudeProjectInstalled,    isGlobInstalled = () => SkillInstaller.IsClaudeGlobalInstalled,    install = SkillInstaller.InstallClaude,    uninstall = SkillInstaller.UninstallClaude },
                new AgentConfig { id = "codex",      nameDisplay = "Codex",         isProjInstalled = () => SkillInstaller.IsCodexProjectInstalled,      isGlobInstalled = () => SkillInstaller.IsCodexGlobalInstalled,      install = SkillInstaller.InstallCodex,     uninstall = SkillInstaller.UninstallCodex,  successMsg = (g, m) => SkillsLocalization.Current == SkillsLocalization.Language.Chinese ? "安装成功！\n" + m + (g ? "" : "\n\n注意：Antigravity 和 Codex 工作区共享 .agents/skills 路径。") : "Install success!\n" + m + (g ? "" : "\n\nNote: Antigravity and Codex share .agents/skills in workspace mode.") },
                new AgentConfig { id = "antigravity", nameDisplay = "Antigravity",  isProjInstalled = () => SkillInstaller.IsAntigravityProjectInstalled, isGlobInstalled = () => SkillInstaller.IsAntigravityGlobalInstalled, install = SkillInstaller.InstallAntigravity, uninstall = SkillInstaller.UninstallAntigravity },
                new AgentConfig { id = "cursor",      nameDisplay = "Cursor",       isProjInstalled = () => SkillInstaller.IsCursorProjectInstalled,     isGlobInstalled = () => SkillInstaller.IsCursorGlobalInstalled,      install = SkillInstaller.InstallCursor,    uninstall = SkillInstaller.UninstallCursor },
            };
        }

        public void OnGUI(Rect r)
        {
            float cardH = 80, gap = 8, customCardH = 130, helpH = 60;
            float totalH = _configs.Count * (cardH + gap) + customCardH + gap + helpH + gap + 20;
            _scroll = GUI.BeginScrollView(r, _scroll, new Rect(0, 0, r.width - 16, totalH));
            float y = 10;

            foreach (var c in _configs)
            {
                DrawAgentCard(new Rect(4, y, r.width - 20, cardH), c);
                y += cardH + gap;
            }

            DrawCustomCard(new Rect(4, y, r.width - 20, customCardH));
            y += customCardH + gap;

            string ht = SkillsLocalization.Current == SkillsLocalization.Language.Chinese
                ? "项目安装：将 Skill 安装到当前 Unity 项目目录\n全局安装：将 Skill 安装到用户目录，所有项目可用\n\n注意：Antigravity 和 Codex 工作区都使用 .agents/skills，安装一次即两边可用"
                : "Project Install: install skill to current Unity project\nGlobal Install: install skill to user folder, available to all projects\n\nNote: Antigravity and Codex both use .agents/skills in workspace mode — install once works for both.";
            EditorGUI.HelpBox(new Rect(4, y, r.width - 20, helpH), ht, MessageType.Info);

            GUI.EndScrollView();
        }

        private void DrawAgentCard(Rect r, AgentConfig c)
        {
            EditorGUI.DrawRect(r, new Color(0.17f, 0.17f, 0.17f));
            float y = r.y + 8;

            // Head: icon + name + badge
            var iconTex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{IconsFolder}/{c.id}.png");
            var ir = new Rect(r.x + 8, y, 28, 28);
            if (iconTex != null) GUI.DrawTexture(ir, iconTex, ScaleMode.ScaleToFit);
            else EditorGUI.DrawRect(ir, new Color(0.3f, 0.3f, 0.3f));

            GUI.Label(new Rect(r.x + 44, y, 150, 20), c.nameDisplay, EditorStyles.boldLabel);

            bool pi = c.isProjInstalled(), gi = c.isGlobInstalled(), any = pi || gi;
            string st = SkillsLocalization.Get(any ? "agent_status_installed" : "agent_status_not_installed");
            var sbg = any ? new Color(0.43f, 0.77f, 0.43f) : new Color(0.24f, 0.24f, 0.24f);
            var sr = new Rect(r.xMax - 90, y, 80, 20);
            EditorGUI.DrawRect(sr, sbg);
            GUI.Label(sr, st, new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter,
                normal = { textColor = any ? new Color(0.1f, 0.23f, 0.1f) : new Color(0.48f, 0.48f, 0.48f) } });

            y += 36;

            // Buttons
            float bw = (r.width - 36) / 3f;
            string pTxt = SkillsLocalization.Get(pi ? "agent_update_project" : "agent_install_project");
            var pBg = GUI.backgroundColor;
            if (!pi) GUI.backgroundColor = new Color(0.29f, 0.62f, 1f);
            if (GUI.Button(new Rect(r.x + 8, y, bw, 22), pTxt))
            { var r1 = c.install(false); EditorUtility.DisplayDialog(r1.success ? "Success" : "Error",
                r1.success ? (c.successMsg?.Invoke(false, r1.message) ?? SkillsLocalization.Get("install_success") + "\n" + r1.message)
                           : string.Format(SkillsLocalization.Get("install_failed"), r1.message), "OK"); }
            GUI.backgroundColor = pBg;

            if (!gi) GUI.backgroundColor = new Color(0.29f, 0.62f, 1f);
            string gTxt = SkillsLocalization.Get(gi ? "agent_update_global" : "agent_install_global");
            if (GUI.Button(new Rect(r.x + 12 + bw, y, bw, 22), gTxt))
            { var r2 = c.install(true); EditorUtility.DisplayDialog(r2.success ? "Success" : "Error",
                r2.success ? (c.successMsg?.Invoke(true, r2.message) ?? SkillsLocalization.Get("install_success") + "\n" + r2.message)
                           : string.Format(SkillsLocalization.Get("install_failed"), r2.message), "OK"); }
            GUI.backgroundColor = pBg;

            string uTxt = SkillsLocalization.Get("uninstall");
            if (pi && gi) uTxt += " ▾";
            GUI.enabled = any;
            if (GUI.Button(new Rect(r.x + 16 + bw * 2, y, bw, 22), uTxt))
            {
                if (pi && gi) { var m = new GenericMenu();
                    m.AddItem(new GUIContent(SkillsLocalization.Get("uninstall") + " " + SkillsLocalization.Get("agent_install_project")), false, () => DoUninstall(c, false));
                    m.AddItem(new GUIContent(SkillsLocalization.Get("uninstall") + " " + SkillsLocalization.Get("agent_install_global")), false, () => DoUninstall(c, true));
                    m.ShowAsContext(); }
                else DoUninstall(c, gi);
            }
            GUI.enabled = true;
        }

        private void DrawCustomCard(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.17f, 0.17f, 0.17f));
            float y = r.y + 8;

            // Head
            var ir = new Rect(r.x + 8, y, 28, 28);
            EditorGUI.DrawRect(ir, new Color(0.24f, 0.24f, 0.24f));
            GUI.Label(ir, "+", new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 18, normal = { textColor = new Color(0.63f, 0.63f, 0.63f) } });
            GUI.Label(new Rect(r.x + 44, y, 200, 20), SkillsLocalization.Get("agent_custom_title"), EditorStyles.boldLabel);
            y += 36;

            // Path
            _customPath = GUI.TextField(new Rect(r.x + 8, y, r.width - 80, 20), _customPath);
            if (GUI.Button(new Rect(r.xMax - 68, y, 60, 20), SkillsLocalization.Get("agent_custom_browse"), EditorStyles.miniButton))
            {
                string p = EditorUtility.OpenFolderPanel(SkillsLocalization.Current == SkillsLocalization.Language.Chinese ? "选择安装目录" : "Select Install Directory", _customPath, "");
                if (!string.IsNullOrEmpty(p)) _customPath = p;
            }
            y += 26;

            // Name + Install
            _customName = GUI.TextField(new Rect(r.x + 8, y, r.width - 96, 22), _customName);
            var bb = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.29f, 0.62f, 1f);
            if (GUI.Button(new Rect(r.xMax - 80, y, 72, 22), SkillsLocalization.Get("agent_custom_install")))
            {
                if (string.IsNullOrEmpty(_customPath)) EditorUtility.DisplayDialog("Error", SkillsLocalization.Current == SkillsLocalization.Language.Chinese ? "路径不能为空" : "Path cannot be empty", "OK");
                else { var r3 = SkillInstaller.InstallCustom(_customPath, _customName); EditorUtility.DisplayDialog(r3.success ? "Success" : "Error", r3.success ? SkillsLocalization.Get("install_success") : string.Format(SkillsLocalization.Get("install_failed"), r3.message), "OK"); }
            }
            GUI.backgroundColor = bb;
        }

        private static void DoUninstall(AgentConfig c, bool global)
        {
            string s = " (" + SkillsLocalization.Get(global ? "agent_install_global" : "agent_install_project") + ")";
            if (EditorUtility.DisplayDialog(SkillsLocalization.Get("uninstall"), string.Format(SkillsLocalization.Get("uninstall_confirm"), c.nameDisplay + s), "OK", "Cancel"))
            { var r = c.uninstall(global); EditorUtility.DisplayDialog(r.success ? "Success" : "Error", r.success ? SkillsLocalization.Get("uninstall_success") : string.Format(SkillsLocalization.Get("uninstall_failed"), r.message), "OK"); }
        }

        public void RefreshLocalization() { }
    }
}
#endif

