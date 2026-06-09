using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Codely.Newtonsoft.Json;
using Codely.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace TJGenerators.Utils
{
    [InitializeOnLoad]
    internal static class DependencyPinning
    {
        private const string DepGltFast = "com.unity.cloud.gltfast";

        /// <summary>UTF-8 without BOM so Unity can parse manifest.json (Char 65279 = BOM causes "not valid JSON").</summary>
        private static readonly Encoding UTF8NoBom = new UTF8Encoding(false);

        private static ListRequest _listRequest;
        private static int _listRetry;

        static DependencyPinning()
        {
            EditorApplication.delayCall += AutoCheckOnLoad;
        }

        // ── Auto-check on editor load ────────────────────────────────────────

        private static void AutoCheckOnLoad()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoCheckOnLoad;
                return;
            }

            var alreadyPinned = IsManifestAlreadyPinned(out var missing);
            if (alreadyPinned)
                return;

            if (missing != null && missing.Count > 0)
            {
                var missingList = string.Join("\n", missing.Select(d => "- " + d));
                Debug.Log(
                    "[DependencyPinning] 检测到项目缺少以下依赖，将自动写入 Packages/manifest.json 进行固定：\n"
                        + missingList
                        + "\n若不固定，卸载本插件后可能导致已生成的 GLB 模型不可用。"
                );
                RunPinFlow(interactive: false);
            }
        }

        /// <summary>由 <see cref="TJGeneratorsMenuItems.PinGlbDependenciesFromMenu"/> 或外部代码调用。</summary>
        public static void PinDependenciesMenu() => RunPinFlow(interactive: true);

        // ── Core flow ────────────────────────────────────────────────────────

        private static void RunPinFlow(bool interactive)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                if (interactive)
                    Debug.LogWarning(
                        "[DependencyPinning] Unity 正在编译或导入资源，请稍后通过菜单「AI/修复/固定GLB模型相关依赖」重试。"
                    );
                return;
            }

            if (IsManifestAlreadyPinned(out var missing))
            {
                if (interactive)
                    Debug.Log(
                        "[DependencyPinning] 项目已固定所需依赖，无需修改 manifest.json。"
                    );
                return;
            }

            StartListInstalledPackages(interactive, missing);
        }

        private static void StartListInstalledPackages(bool interactive, HashSet<string> missing)
        {
            _listRetry = 0;
            _listRequest = Client.List(true);
            EditorApplication.update -= OnListProgress;
            EditorApplication.update += OnListProgress;

            void OnListProgress()
            {
                if (_listRequest == null)
                {
                    EditorApplication.update -= OnListProgress;
                    return;
                }

                if (!_listRequest.IsCompleted)
                    return;

                EditorApplication.update -= OnListProgress;

                if (_listRequest.Status != StatusCode.Success)
                {
                    if (_listRetry < 3)
                    {
                        _listRetry++;
                        _listRequest = Client.List(true);
                        EditorApplication.update += OnListProgress;
                        return;
                    }

                    if (interactive)
                        ErrorDialogUtils.ShowErrorDialog(
                            "读取包列表失败",
                            $"无法获取已安装包列表：{_listRequest.Error?.message ?? "unknown error"}\n将使用本包依赖版本作为回退。",
                            "[DependencyPinning]"
                        );
                }

                var installed =
                    _listRequest.Result?.ToDictionary(p => p.name, p => p.version)
                    ?? new Dictionary<string, string>();

                var fallback = GetFallbackVersions();

                var versionsToPin = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase
                );
                foreach (var dep in missing)
                {
                    if (installed.TryGetValue(dep, out var v) && !string.IsNullOrEmpty(v))
                        versionsToPin[dep] = v;
                    else if (fallback.TryGetValue(dep, out var fv) && !string.IsNullOrEmpty(fv))
                        versionsToPin[dep] = fv;
                }

                if (versionsToPin.Count == 0)
                {
                    if (interactive)
                        Debug.Log(
                            "[DependencyPinning] 未找到需要固定的依赖（可能已被固定或无法解析版本）。"
                        );
                    return;
                }

                var ok = TryPinIntoManifest(versionsToPin, out var error);
                if (!ok)
                {
                    if (interactive)
                        ErrorDialogUtils.ShowErrorDialog("写入失败", error ?? "unknown error", "[DependencyPinning]");
                    else
                        Debug.LogWarning($"[DependencyPinning] 写入 manifest 失败: {error}");
                    return;
                }

                Client.Resolve();

                if (interactive)
                    Debug.Log(
                        "[DependencyPinning] 已写入 Packages/manifest.json 并触发依赖解析。如果未立即生效，请等待 Package Manager 完成解析或重启编辑器。"
                    );
            }
        }

        private static bool IsManifestAlreadyPinned(out HashSet<string> missing)
        {
            missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { DepGltFast };

            var manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath))
                return false;

            var manifest = JObject.Parse(
                StripBomIfPresent(File.ReadAllText(manifestPath, Encoding.UTF8))
            );
            var deps = manifest["dependencies"] as JObject;
            if (deps != null)
            {
                if (deps[DepGltFast] != null)
                    missing.Remove(DepGltFast);
            }

            return missing.Count == 0;
        }

        private static bool TryPinIntoManifest(
            Dictionary<string, string> versionsToPin,
            out string error
        )
        {
            error = null;
            try
            {
                var manifestPath = GetManifestPath();
                if (!File.Exists(manifestPath))
                {
                    error = $"manifest.json 不存在：{manifestPath}";
                    return false;
                }

                var original = StripBomIfPresent(File.ReadAllText(manifestPath, Encoding.UTF8));
                var manifest = JObject.Parse(original);

                if (!(manifest["dependencies"] is JObject deps))
                {
                    deps = new JObject();
                    manifest["dependencies"] = deps;
                }

                bool changed = false;
                foreach (var kv in versionsToPin)
                {
                    if (deps[kv.Key] == null)
                    {
                        deps[kv.Key] = kv.Value;
                        changed = true;
                    }
                }

                if (!changed)
                    return true;

                var bakPath = manifestPath + ".bak";
                if (!File.Exists(bakPath))
                    File.WriteAllText(bakPath, original, UTF8NoBom);

                File.WriteAllText(manifestPath, manifest.ToString(Formatting.Indented), UTF8NoBom);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private static Dictionary<string, string> GetFallbackVersions()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { DepGltFast, "6.8.0" },
            };

            try
            {
                var info = PackageManagerPackageInfo.FindForAssembly(
                    typeof(DependencyPinning).Assembly
                );
                if (string.IsNullOrEmpty(info?.resolvedPath))
                    return result;

                var packageJsonPath = Path.Combine(info.resolvedPath, "package.json");
                if (!File.Exists(packageJsonPath))
                    return result;

                var pkg = JObject.Parse(File.ReadAllText(packageJsonPath, Encoding.UTF8));
                if (pkg["dependencies"] is JObject deps)
                {
                    foreach (var kv in deps)
                        result[kv.Key] = kv.Value?.ToString() ?? "";
                }
            }
            catch
            { /* hardcoded fallback is fine */
            }

            return result;
        }

        private static string GetManifestPath()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Packages", "manifest.json");
        }

        private static string StripBomIfPresent(string text)
        {
            return !string.IsNullOrEmpty(text) && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }
    }
}
