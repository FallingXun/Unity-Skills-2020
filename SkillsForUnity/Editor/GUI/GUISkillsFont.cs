#if !UNITY_2021_2_OR_NEWER
using UnityEngine;
using UnityEditor;
using System;
namespace UnitySkills
{
    /// <summary>
    /// CJK font helper for IMGUI editor windows.
    /// In IMGUI, the built-in editor font typically handles CJK glyphs fine on
    /// all platforms, so this is a lightweight wrapper that loads the bundled
    /// font for cases where we want to override the default.
    /// Safe to call; returns null gracefully if the font is not available.
    /// </summary>
    internal static class GUISkillsFont
    {
        private const string TtfPath =
            "Packages/com.besty.unity-skills/Editor/UI/Fonts/UnitySkillsCN-Regular.ttf";

        private static Font _cjkFont;
        private static bool _attempted;

        /// <summary>
        /// Get the bundled CJK font. Returns null if the font file is not found,
        /// in which case the caller should fall back to the editor default.
        /// </summary>
        public static Font GetFont()
        {
            if (_attempted) return _cjkFont;
            _attempted = true;

            try
            {
                _cjkFont = AssetDatabase.LoadAssetAtPath<Font>(TtfPath);
                if (_cjkFont == null)
                {
                    Debug.LogWarning($"[UnitySkills] CJK font not found, using editor default: {TtfPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnitySkills] Failed to load CJK font: {ex.Message}");
                _cjkFont = null;
            }

            return _cjkFont;
        }

        /// <summary>
        /// Create a GUIStyle that uses the bundled CJK font for labels.
        /// Falls back to the editor default label style if the font is unavailable.
        /// </summary>
        public static GUIStyle CreateLabelStyle(int fontSize = 11, FontStyle fontStyle = FontStyle.Normal)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                fontSize = fontSize,
                fontStyle = fontStyle,
            };
            var font = GetFont();
            if (font != null) style.font = font;
            return style;
        }

        /// <summary>
        /// Create a GUIStyle that uses the bundled CJK font for bold labels.
        /// Falls back to the editor default bold label style if the font is unavailable.
        /// </summary>
        public static GUIStyle CreateBoldLabelStyle(int fontSize = 11)
        {
            return CreateLabelStyle(fontSize, FontStyle.Bold);
        }
    }
}
#endif

