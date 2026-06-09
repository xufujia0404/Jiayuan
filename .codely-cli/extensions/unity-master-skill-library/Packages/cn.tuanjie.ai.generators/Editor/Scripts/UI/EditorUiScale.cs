#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace TJGenerators.UI
{
    public static class EditorUiScale
    {
        /// <summary>固定 UI 布局缩放比例。</summary>
        public const float FixedScale = 0.75f;

        /// <summary>固定字号缩放比例。</summary>
        public const float FixedFontScale = 0.82f;

        private const string LegacyPrefKey = "TJGenerators.EditorUi.Scale";

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            // 移除旧版菜单写入的缩放偏好，避免残留键误导排查
            if (EditorPrefs.HasKey(LegacyPrefKey))
                EditorPrefs.DeleteKey(LegacyPrefKey);
        }

        /// <summary>当前 UI 缩放因子（恒为 <see cref="FixedScale"/>）。</summary>
        public static float Scale => FixedScale;

        public static float S(float designPixels) => designPixels * Scale;

        public static int Ro(float designPixels) => Mathf.Max(0, Mathf.RoundToInt(designPixels * Scale));

        public static int Font(int designFontSize) =>
            Mathf.Max(8, Mathf.RoundToInt(designFontSize * FixedFontScale));
    }
}
#endif
