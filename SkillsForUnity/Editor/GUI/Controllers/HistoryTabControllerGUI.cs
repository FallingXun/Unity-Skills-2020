#if !UNITY_2021_2_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
namespace UnitySkills
{
    /// <summary>
    /// History Tab. Rect-based.
    /// </summary>
    public class HistoryTabControllerGUI
    {
        private readonly UnitySkillsWindowGUI _window;
        private Vector2 _scroll;

        public HistoryTabControllerGUI(UnitySkillsWindowGUI w) { _window = w; }

        public void OnGUI(Rect r)
        {
            // Toolbar
            var tb = new Rect(r.x, r.y, r.width, 24);
            EditorGUI.DrawRect(tb, new Color(0.18f, 0.18f, 0.18f));
            string t = SkillsLocalization.Current == SkillsLocalization.Language.Chinese ? "工作流历史" : "Workflow History";
            GUI.Label(new Rect(r.x + 8, r.y, 120, 24), t, EditorStyles.boldLabel);

            if (GUI.Button(new Rect(r.xMax - 80, r.y + 2, 22, 20), "↻", EditorStyles.toolbarButton))
                WorkflowManager.LoadHistory();

            GUI.color = new Color(0.88f, 0.39f, 0.39f);
            if (GUI.Button(new Rect(r.xMax - 54, r.y + 2, 50, 20), SkillsLocalization.Get("history_clear_all"), EditorStyles.toolbarButton))
                ClearHistory();
            GUI.color = Color.white;

            float hy = r.y + 28;

            // Warning
            string wText = SkillsLocalization.Current == SkillsLocalization.Language.Chinese
                ? "工作流缓存警告：撤销操作仅恢复场景状态和文件快照，不会撤销如包管理器操作或外部系统的副作用。"
                : "Workflow Cache Warning: undo restores scene hierarchies and asset snapshots. External side effects (e.g. Package Manager) cannot be reverted.";
            var hs = EditorStyles.helpBox;
            float hh = hs.CalcHeight(new GUIContent(wText), r.width - 8);
            EditorGUI.HelpBox(new Rect(r.x + 4, hy, r.width - 8, hh), wText, MessageType.Warning);
            hy += hh + 8;

            // Content
            var history = WorkflowManager.History;
            if (history == null) { WorkflowManager.LoadHistory(); history = WorkflowManager.History; }
            float cardH = 64, gap = 6;
            float totalH = CalculateSectionH(history?.tasks) + 30 + CalculateSectionH(history?.undoneStack) + 30;
            _scroll = GUI.BeginScrollView(new Rect(r.x, hy, r.width, r.yMax - hy), _scroll, new Rect(0, 0, r.width - 16, totalH));
            float sy = 0;
            sy = DrawSection(sy, r.width - 16, history?.tasks, true, "history_active_format", "history_no_active");
            sy += 20;
            sy = DrawSection(sy, r.width - 16, history?.undoneStack, false, "history_undone_format", "history_no_undone");
            GUI.EndScrollView();
        }

        private static float CalculateSectionH(List<WorkflowTask> tasks)
        {
            if (tasks == null || tasks.Count == 0) return 30;
            return 22 + tasks.Count * 70;
        }

        private static float DrawSection(float y, float w, List<WorkflowTask> tasks, bool active, string fmtKey, string emptyKey)
        {
            int n = tasks?.Count ?? 0;
            GUI.color = active ? new Color(0.29f, 0.62f, 1f) : new Color(0.48f, 0.48f, 0.48f);
            GUI.Label(new Rect(4, y, w - 8, 18), string.Format(SkillsLocalization.Get(fmtKey), n), EditorStyles.boldLabel);
            GUI.color = Color.white;
            y += 22;

            if (n == 0)
            {
                GUI.Label(new Rect(8, y, w - 16, 18), SkillsLocalization.Get(emptyKey), EditorStyles.miniLabel);
                return y + 20;
            }

            for (int i = tasks.Count - 1; i >= 0; i--)
            {
                y = DrawCard(y, w, tasks[i], active);
            }
            return y;
        }

        private static float DrawCard(float y, float w, WorkflowTask task, bool active)
        {
            var r = new Rect(4, y, w - 8, 64);
            EditorGUI.DrawRect(r, active ? new Color(0.17f, 0.17f, 0.17f) : new Color(0.17f, 0.17f, 0.17f, 0.55f));

            string name = task.tag ?? task.id ?? "(unnamed)";
            GUI.Label(new Rect(r.x + 8, r.y + 6, 200, 18), name, EditorStyles.boldLabel);

            int ch = task.snapshots?.Count ?? 0;
            if (ch > 0)
                GUI.Label(new Rect(r.x + 210, r.y + 6, 100, 18), $"({ch} {SkillsLocalization.Get("history_changes_suffix")})", EditorStyles.miniLabel);

            GUI.Label(new Rect(r.xMax - 70, r.y + 6, 66, 18), task.GetFormattedTime(), EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(task.description))
                GUI.Label(new Rect(r.x + 8, r.y + 26, r.width - 16, 16), task.description, EditorStyles.wordWrappedMiniLabel);

            if (active)
            {
                if (GUI.Button(new Rect(r.x + 8, r.yMax - 24, 50, 18), "Undo", EditorStyles.miniButton))
                    WorkflowManager.UndoTask(task.id);
            }
            else
            {
                var bb = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.29f, 0.62f, 1f);
                if (GUI.Button(new Rect(r.x + 8, r.yMax - 24, 50, 18), "Redo", EditorStyles.miniButton))
                    WorkflowManager.RedoTask(task.id);
                GUI.backgroundColor = bb;
            }

            GUI.color = new Color(0.88f, 0.39f, 0.39f);
            if (GUI.Button(new Rect(r.x + 64, r.yMax - 24, 24, 18), "×", EditorStyles.miniButton))
                WorkflowManager.DeleteTask(task.id);
            GUI.color = Color.white;

            return y + 70;
        }

        private void ClearHistory()
        {
            string t = SkillsLocalization.Current == SkillsLocalization.Language.Chinese ? "清除历史" : "Clear History";
            string m = SkillsLocalization.Current == SkillsLocalization.Language.Chinese
                ? "确定要清除所有历史记录吗？这也会删除磁盘上的工作流缓存快照。"
                : "Are you sure you want to clear all history? This will also delete workflow cached snapshots on disk.";
            if (EditorUtility.DisplayDialog(t, m, "Yes", "No")) WorkflowManager.ClearHistory();
        }

        public void RefreshLocalization() { }
    }
}
#endif

