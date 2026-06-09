#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.PackageManager;
using UnityEditor;
using UnityEngine;
using TJGenerators.Config;

namespace TJGenerators.Utils
{
    /// <summary>
    /// 路径与通用路径取值：项目/资源路径转换，以及响应对象上的点路径反射取值（字段/属性、数组下标）。
    /// </summary>
    public static class PathUtils
    {
        /// <summary>
        /// 解析本包（cn.tuanjie.ai.generators）在磁盘上的根目录，用于 git/本地 Package、以及 Editor/ 相对路径。
        /// </summary>
        public static string TryGetTjGeneratorsPackageRoot()
        {
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ConfigManager).Assembly);
                if (info != null
                    && !string.IsNullOrEmpty(info.resolvedPath)
                    && Directory.Exists(info.resolvedPath))
                    return info.resolvedPath;
            }
            catch
            {
                // ignored
            }

            return null;
        }

        /// <summary>
        /// 将项目相对路径转换为绝对路径
        /// </summary>
        /// <param name="projectRelativePath">项目相对路径（可以是 "Assets/..." 或其他相对路径）</param>
        /// <returns>绝对路径</returns>
        /// <remarks>
        /// - 如果路径为空或已经是绝对路径，直接返回原路径
        /// - "Packages/..." 解析到项目根下的 Packages（与 Assets 同级），不会错误落到 Assets/Packages
        /// - "Assets/..." 会转换为 Application.dataPath 下的绝对路径
        /// - "Editor/..." 解析为当前 TJGenerators 包根下的相对路径（便于随包分发、不依赖 UPM 文件夹名）
        /// - 其他相对路径按「位于 Assets 目录下」与 Application.dataPath 组合（兼容旧用法）
        /// - 会自动规范化路径中的反斜杠为正斜杠
        /// </remarks>
        /// <summary>
        /// 若绝对路径位于本工程 Assets 下，返回 Assets 相对路径；否则返回 null。
        /// </summary>
        public static string TryGetAssetsRelativePathFromAbsolute(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return null;

            absolutePath = Path.GetFullPath(absolutePath);
            string dataPath = Path.GetFullPath(Application.dataPath);
            if (!absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return null;

            string tail = absolutePath.Substring(dataPath.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return "Assets/" + tail.Replace('\\', '/');
        }

        /// <summary>
        /// 将名称规范为可作为 Assets 下文件夹名的片段（去除非法文件名字符）。
        /// </summary>
        public static string SanitizeAssetFolderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Model";

            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            name = name.Trim();
            return string.IsNullOrEmpty(name) ? "Model" : name;
        }

        public static string ToAbsoluteAssetPath(string projectRelativePath)
        {
            if (string.IsNullOrEmpty(projectRelativePath)) return projectRelativePath;
            if (Path.IsPathRooted(projectRelativePath)) return projectRelativePath;

            projectRelativePath = projectRelativePath.Replace("\\", "/");

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            if (projectRelativePath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(Path.Combine(projectRoot, projectRelativePath));

            const string assetsPrefix = "Assets/";
            if (projectRelativePath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
                return Path.Combine(Application.dataPath, projectRelativePath.Substring(assetsPrefix.Length));

            if (projectRelativePath.StartsWith("Editor/", StringComparison.OrdinalIgnoreCase))
            {
                string pkgRoot = TryGetTjGeneratorsPackageRoot();
                if (!string.IsNullOrEmpty(pkgRoot))
                    return Path.GetFullPath(Path.Combine(pkgRoot, projectRelativePath));
            }

            return Path.Combine(Application.dataPath, projectRelativePath);
        }

        /// <summary>
        /// 将磁盘绝对路径转为 Unity 资源路径（Assets/...）。
        /// 不要求路径对应文件已存在（与 <see cref="TryGetAssetsRelativePathFromAbsolute"/> 不同）。
        /// 若路径不在本工程 Assets 目录下，返回传入的 <paramref name="absolutePath"/> 原样。
        /// </summary>
        public static string AbsolutePathToAssetsRelative(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return absolutePath;

            string normalized = Path.GetFullPath(absolutePath).Replace('\\', '/');
            string dataPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            if (normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
                return "Assets" + normalized.Substring(dataPath.Length);
            return absolutePath;
        }

        /// <summary>
        /// 在 Assets 目录下查找扩展名为 .fbx / .glb / .obj 的模型资源路径（Unity 工程相对路径，如 Assets/…）。
        /// 说明：避免使用 FindAssets("", Assets) 扫全库——在工程很大时会在 OnGUI 中反复触发并可能导致编辑器堆内存暴涨崩溃。
        /// Unity 2021.2+ 使用 glob 仅匹配目标扩展名；更早版本退化为全库扫描（调用方应节流，勿每帧调用）。
        /// </summary>
        public static List<string> FindMeshModelAssetPathsInAssets()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

#if UNITY_2021_2_OR_NEWER
            void AddGlob(string glob)
            {
                foreach (string guid in AssetDatabase.FindAssets(glob, new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(path))
                        continue;
                    set.Add(path);
                }
            }

            AddGlob("glob:\"**/*.fbx\"");
            AddGlob("glob:\"**/*.glb\"");
            AddGlob("glob:\"**/*.obj\"");
#else
            string[] guids = AssetDatabase.FindAssets("", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path))
                    continue;

                string ext = Path.GetExtension(path);
                if (
                    ext.Equals(".fbx", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".glb", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".obj", StringComparison.OrdinalIgnoreCase)
                )
                    set.Add(path);
            }
#endif

            var results = new List<string>(set);
            results.Sort(StringComparer.OrdinalIgnoreCase);
            return results;
        }

        /// <summary>
        /// 在对象上按点路径取值（字段/属性）；段为数字时对 <see cref="Array"/> 按下标访问（如 resultFiles.0.url）。
        /// </summary>
        public static object GetRaw(object obj, string path)
        {
            if (obj == null || string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split('.');
            object current = obj;

            foreach (var part in parts)
            {
                if (current == null)
                    return null;

                if (
                    current is Array arr
                    && int.TryParse(part, out int idx)
                    && idx >= 0
                    && idx < arr.Length
                )
                {
                    current = arr.GetValue(idx);
                    continue;
                }

                var type = current.GetType();
                var field = type.GetField(part);
                if (field != null)
                {
                    current = field.GetValue(current);
                }
                else
                {
                    var prop = type.GetProperty(part);
                    if (prop != null)
                    {
                        current = prop.GetValue(current);
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return current;
        }

        /// <summary>
        /// 同 <see cref="GetRaw"/>；若结果为数组则取首元素再 ToString，否则直接 ToString。
        /// </summary>
        public static string GetString(object obj, string path)
        {
            object current = GetRaw(obj, path);
            if (current == null)
                return null;

            if (current is Array arr && arr.Length > 0)
                return arr.GetValue(0)?.ToString();
            return current.ToString();
        }

        /// <summary>
        /// 下载包目录命名规则：直接使用 asset_id（slug），与后端保持一致。
        /// 入参为空时返回空字符串，由调用方自行处理。
        /// </summary>
        public static string BuildPackageDirName(string assetId)
        {
            return string.IsNullOrEmpty(assetId) ? string.Empty : assetId;
        }

        /// <summary>
        /// 递归确保 Assets 下指定路径的资源文件夹存在（Unity AssetDatabase 视角）。
        /// 路径必须以 "Assets" 开头；空路径或已存在则直接返回。
        /// </summary>
        public static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return;
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) return;
            if (!AssetDatabase.IsValidFolder(parent))
                EnsureAssetFolder(parent);
            string name = Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>
        /// 安全删除文件：路径为空/文件不存在直接跳过；IO 异常吞掉不抛出。
        /// </summary>
        public static void SafeDelete(string path)
        {
            try { if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path); }
            catch { /* ignore */ }
        }
    }
}
#endif
