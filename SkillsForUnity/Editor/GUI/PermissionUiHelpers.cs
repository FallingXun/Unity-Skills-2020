#if !UNITY_2021_2_OR_NEWER
using UnityEditor;
using System;
namespace UnitySkills
{
    /// <summary>
    /// v1.9 权限/审计面板共享小工具。
    /// 集中处理 Localization fallback 与"老安装"判定，让 EditorWindow 实现保持薄。
    /// 从 UI/UnitySkillsWindow.cs 独立拷贝到 GUI 文件夹，保证 GUI 模块自包含。
    /// </summary>
    internal static class PermissionUiHelpers
    {
        /// <summary>
        /// 先查 SkillsLocalization；如果 key 缺失（Get 返回 key 本身），按当前语言走 fallback。
        /// 让 UI 不依赖其他 agent 补译 Localization.cs，后续补 key 自动生效。
        /// </summary>
        public static string L(string key, string enFallback, string cnFallback)
        {
            var v = SkillsLocalization.Get(key);
            if (!string.Equals(v, key, StringComparison.Ordinal)) return v;
            return SkillsLocalization.Current == SkillsLocalization.Language.Chinese
                ? cnFallback : enFallback;
        }

        /// <summary>
        /// 与 <c>SkillsModeManager</c> 内部 IsExistingInstall 同步的 UI 侧判定，
        /// 用于决定是否对老用户隐藏首启 toast；保持两侧 key 列表一致即可。
        /// </summary>
        public static bool IsExistingInstall()
        {
            return EditorPrefs.HasKey("UnitySkills_RequireConfirmation")
                || EditorPrefs.HasKey("UnitySkills_PreferredPort")
                || EditorPrefs.HasKey("UnitySkills_LogLevel")
                || EditorPrefs.HasKey("UnitySkills_Language")
                || EditorPrefs.HasKey("UnitySkills_RequestTimeoutMinutes")
                || EditorPrefs.HasKey("UnitySkills_KeepAliveIntervalSeconds")
                || EditorPrefs.HasKey("UnitySkills_AutoInstallPackagesOnStartup");
        }

        public static string FormatCountdown(DateTime expiresAtUtc)
        {
            var remaining = expiresAtUtc - DateTime.UtcNow;
            if (remaining.TotalSeconds <= 0) return "expired";
            if (remaining.TotalMinutes >= 1)
                return $"{(int)remaining.TotalMinutes}m{remaining.Seconds:00}s";
            return $"{(int)remaining.TotalSeconds}s";
        }

        public static string ShortToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            return token.Length <= 6 ? token : token.Substring(0, 6);
        }
    }
}
#endif

