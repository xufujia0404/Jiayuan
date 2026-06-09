#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace TJGenerators.Utils
{
    /// <summary>
    /// 音频占位符路径：文生音频（如火山）常为 .mp4，音效为 .mp3 等，与占位 .wav 不同扩展名但同名。
    /// <see cref="AssetDatabase.GenerateUniqueAssetPath"/> 只检查单一扩展名，会误判「基名未占用」而重复命名。
    /// </summary>
    public static class TJGeneratorsAudioAssetPathUtility
    {
        private static readonly string[] k_AudioFileExtensions =
        {
            ".wav",
            ".mp4",
            ".m4a",
            ".mp3",
            ".ogg",
            ".m4a",
            ".aac",
            ".flac",
            ".aif",
            ".aiff",
            ".wma"
        };

        /// <summary>
        /// 在目标目录下生成唯一的 .wav 占位路径：若同基名已存在任意常见音频扩展名，则递增序号（与 Unity「名称」「名称 1」「名称 2」规则一致）。
        /// 会将「名称 1」末尾序号视为递增部分，从而在冲突时生成「名称 2」而非「名称 1 1」。
        /// </summary>
        public static string GenerateUniquePlaceholderWavPath(string preferredAssetPath)
        {
            if (string.IsNullOrEmpty(preferredAssetPath))
                preferredAssetPath = "Assets/New AudioClip.wav";

            preferredAssetPath = preferredAssetPath.Replace('\\', '/');
            string directory = Path.GetDirectoryName(preferredAssetPath);
            if (string.IsNullOrEmpty(directory))
                directory = "Assets";

            string baseStem = Path.GetFileNameWithoutExtension(preferredAssetPath);
            if (string.IsNullOrEmpty(baseStem))
                baseStem = "New AudioClip";

            if (!AudioStemIsOccupied(directory, baseStem))
                return $"{directory}/{baseStem}.wav";

            string rootStem;
            int suffixFrom = 1;
            if (TryParseUnityTrailingNumericSuffix(baseStem, out rootStem, out int k))
                suffixFrom = k;

            string candidateStem;
            for (int n = suffixFrom; ; n++)
            {
                candidateStem = $"{rootStem} {n}";
                if (!AudioStemIsOccupied(directory, candidateStem))
                    return $"{directory}/{candidateStem}.wav";
            }
        }

        /// <summary>例如 "New Audio Clip 1" → root "New Audio Clip", trailing 1；无尾部数字则返回 false。</summary>
        private static bool TryParseUnityTrailingNumericSuffix(string stem, out string rootStem, out int trailing)
        {
            rootStem = stem;
            trailing = 0;
            if (string.IsNullOrEmpty(stem))
                return false;
            var m = Regex.Match(stem.TrimEnd(), @"^(.+)\s(\d+)$");
            if (!m.Success)
                return false;
            rootStem = m.Groups[1].Value.TrimEnd();
            if (rootStem.Length == 0 || !int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out trailing))
                return false;
            return trailing >= 0;
        }

        /// <summary>
        /// 将配置/API 侧的音频格式转为 Unity 工程中实际使用的文件扩展名（不含路径点）。
        /// 若使用 .mp4 扩展名，Unity 默认走<strong>视频</strong>导入器；在 Windows 上对常见「仅 AAC 音轨」的 MP4 流易触发 WindowsVideoMedia 0xc00d36c4。
        /// 对这类内容改用 .m4a，走音频导入器。
        /// </summary>
        public static string NormalizeImportedAudioFileExtension(string audioFormat)
        {
            if (string.IsNullOrWhiteSpace(audioFormat))
                return "wav";
            string s = audioFormat.Trim().TrimStart('.').ToLowerInvariant();
            return s == "mp4" ? "m4a" : s;
        }

        private static bool AudioStemIsOccupied(string directoryAssetPath, string fileNameWithoutExtension)
        {
            foreach (var ext in k_AudioFileExtensions)
            {
                string assetPath = $"{directoryAssetPath}/{fileNameWithoutExtension}{ext}";
                if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
                    return true;
                string abs = PathUtils.ToAbsoluteAssetPath(assetPath);
                if (!string.IsNullOrEmpty(abs) && File.Exists(abs))
                    return true;
            }
            return false;
        }
    }
}
#endif
