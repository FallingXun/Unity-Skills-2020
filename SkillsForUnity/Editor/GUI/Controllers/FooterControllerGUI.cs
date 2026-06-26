#if !UNITY_2021_2_OR_NEWER
using UnityEditor;
using UnityEngine;
namespace UnitySkills
{
    /// <summary>
    /// Footer — version | stats pill | language seg. Rect-based.
    /// </summary>
    public class FooterControllerGUI
    {
        private readonly UnitySkillsWindowGUI _window;
        public FooterControllerGUI(UnitySkillsWindowGUI w) { _window = w; }

        public void OnGUI(Rect r)
        {
            EditorGUI.DrawRect(r, new Color(0.11f, 0.11f, 0.11f));

            float x = r.x + 8;
            float cy = r.y + (r.height - 16) / 2;

            // Version
            GUI.Label(new Rect(x, r.y, 50, r.height), "v" + SkillsLogger.Version, EditorStyles.miniLabel);
            x += 56;

            // Stats pill
            int queue = SkillsHttpServer.QueuedRequests;
            long done = SkillsHttpServer.TotalProcessed;

            var pr = new Rect(x, cy, 150, 16);
            EditorGUI.DrawRect(pr, new Color(0.18f, 0.18f, 0.18f));

            GUI.color = queue > 0 ? new Color(0.88f, 0.69f, 0.29f) : Color.white;
            GUI.Label(new Rect(pr.x + 4, pr.y, 14, 16), "▣", EditorStyles.miniLabel);
            GUI.Label(new Rect(pr.x + 16, pr.y, 22, 16), queue.ToString(), EditorStyles.boldLabel);
            GUI.color = Color.white;
            GUI.Label(new Rect(pr.x + 38, pr.y, 40, 16), SkillsLocalization.Get("footer_queue"), EditorStyles.miniLabel);

            GUI.color = new Color(0.43f, 0.77f, 0.43f);
            GUI.Label(new Rect(pr.x + 78, pr.y, 14, 16), "✓", EditorStyles.miniLabel);
            GUI.Label(new Rect(pr.x + 90, pr.y, 30, 16), done.ToString(), EditorStyles.boldLabel);
            GUI.color = Color.white;
            GUI.Label(new Rect(pr.x + 120, pr.y, 30, 16), SkillsLocalization.Get("footer_done"), EditorStyles.miniLabel);

            // Language seg — right-aligned
            bool cn = SkillsLocalization.Current == SkillsLocalization.Language.Chinese;
            float srX = r.xMax - 80;
            var segBg = new Rect(srX, cy, 70, 16);
            EditorGUI.DrawRect(segBg, new Color(0.16f, 0.16f, 0.16f));

            if (GUI.Button(new Rect(srX, cy, 32, 16), "EN", EditorStyles.miniButtonMid))
                _window.SetLanguage(SkillsLocalization.Language.English);
            if (!cn)
                EditorGUI.DrawRect(new Rect(srX, cy, 32, 16), new Color(0.29f, 0.62f, 1f));

            if (GUI.Button(new Rect(srX + 32, cy, 38, 16), "中文", EditorStyles.miniButtonMid))
                _window.SetLanguage(SkillsLocalization.Language.Chinese);
            if (cn)
                EditorGUI.DrawRect(new Rect(srX + 32, cy, 38, 16), new Color(0.29f, 0.62f, 1f));
        }

        public void RefreshLocalization() { }
    }
}
#endif

