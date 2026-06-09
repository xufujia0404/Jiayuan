#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using TJGenerators.Utils;

namespace TJGenerators.Pipeline
{
    public static class RiggedModelPostProcessUtils
    {
        public static void SetupRiggedCharacterImport(string assetPath)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter != null)
            {
                modelImporter.animationType = ModelImporterAnimationType.Human;
                modelImporter.importAnimation = false;
                modelImporter.SaveAndReimport();
                TJLog.Log($"[RiggedModelPostProcessUtils] 绑骨模型 Humanoid 导入（不内嵌动画剪辑）: {assetPath}");
            }
        }

        /// <summary>
        /// 在 SetupRiggedCharacterImport + Refresh() 之后调用。
        /// 若 Avatar.isHuman 仍为 False，扫描 FBX 内的标准命名骨骼并补充缺失的 Humanoid 映射，
        /// 再触发一次重新导入。适用于 UniRig 输出中 Chest/UpperChest/Neck/Head 未被自动识别的情况。
        /// </summary>
        /// <returns>修复后 avatar.isHuman 是否为 True。</returns>
        public static bool TryFixHumanoidBoneMapping(string assetPath)
        {
            // 已经是 Humanoid 则不处理
            var existingAvatar = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                                              .OfType<Avatar>()
                                              .FirstOrDefault(a => a != null && a.isValid);
            if (existingAvatar != null && existingAvatar.isHuman)
                return true;

            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return false;

            // 收集 FBX 中所有骨骼名
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (go == null) return false;
            var allBones = new HashSet<string>(
                go.GetComponentsInChildren<Transform>(true).Select(t => t.name));

            // 已映射的 humanName 集合
            var desc           = importer.humanDescription;
            var alreadyMapped  = new HashSet<string>(
                (desc.human ?? new HumanBone[0]).Select(h => h.humanName));
            var humanBones     = new List<HumanBone>(desc.human ?? new HumanBone[0]);
            bool changed       = false;

            // 候选：(humanName, boneName in FBX)
            // 先列直接同名映射，Head 条目按优先级排列（先尝试 "Head"，再用 "Neck_end" 兜底）
            var candidates = new (string humanName, string boneName)[]
            {
                ("Chest",          "Chest"),
                ("UpperChest",     "UpperChest"),
                ("Neck",           "Neck"),
                ("Head",           "Head"),
                ("Head",           "Neck_end"),    // UniRig 常见兜底
                ("LeftShoulder",   "LeftShoulder"),
                ("RightShoulder",  "RightShoulder"),
                ("LeftToes",       "LeftToes"),
                ("RightToes",      "RightToes"),
            };

            foreach (var (humanName, boneName) in candidates)
            {
                if (alreadyMapped.Contains(humanName)) continue;
                if (!allBones.Contains(boneName)) continue;

                var hb = new HumanBone();
                hb.humanName           = humanName;
                hb.boneName            = boneName;
                hb.limit.useDefaultValues = true;
                humanBones.Add(hb);
                alreadyMapped.Add(humanName);   // 防止同 humanName 的兜底条目重复添加
                changed = true;
                TJLog.Log($"[RiggedModelPostProcessUtils] 补充 Humanoid 骨映射: {humanName} → {boneName}");
            }

            if (!changed)
            {
                TJLog.LogWarning($"[RiggedModelPostProcessUtils] Avatar 不是 Humanoid 且无法自动补全缺失映射: {assetPath}");
                return false;
            }

            desc.human            = humanBones.ToArray();
            importer.humanDescription = desc;
            importer.SaveAndReimport();

            var newAvatar = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                                         .OfType<Avatar>()
                                         .FirstOrDefault(a => a != null && a.isValid);
            bool success = newAvatar?.isHuman == true;
            if (success)
                TJLog.Log($"[RiggedModelPostProcessUtils] Humanoid 骨映射修复成功: {assetPath}");
            else
                TJLog.LogWarning($"[RiggedModelPostProcessUtils] 修复后 Avatar 仍非 Humanoid，骨骼结构可能不兼容: {assetPath}");
            return success;
        }

        public static void SetupAnimationImport(string assetPath)
        {
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter != null)
            {
                modelImporter.animationType = ModelImporterAnimationType.Human;
                modelImporter.importAnimation = true;
                modelImporter.SaveAndReimport();
                TJLog.Log($"[RiggedModelPostProcessUtils] 动画导入配置完成: {assetPath}");
            }
        }

        /// <summary>
        /// 将进 UniRig 前模型上的材质赋给绑骨后的模型。
        /// 优先按 Renderer 数量一致时按下标一一对应；否则按层级相对路径匹配；再否则按较短列表长度按下标对齐。
        /// </summary>
        /// <returns>成功写入材质的 Renderer 数量。</returns>
        public static int ApplyMaterialsFromSourceModelToRiggedModel(string sourceAssetPath, string riggedAssetPath)
        {
            if (string.IsNullOrEmpty(sourceAssetPath) || string.IsNullOrEmpty(riggedAssetPath))
                return 0;

            var sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(sourceAssetPath);
            var riggedRoot = AssetDatabase.LoadAssetAtPath<GameObject>(riggedAssetPath);
            if (sourceRoot == null || riggedRoot == null)
            {
                TJLog.LogWarning(
                    $"[RiggedModelPostProcessUtils] 绑骨后材质复用：无法加载模型 source={sourceAssetPath} rigged={riggedAssetPath}"
                );
                return 0;
            }

            Renderer[] src = sourceRoot.GetComponentsInChildren<Renderer>(true);
            Renderer[] dst = riggedRoot.GetComponentsInChildren<Renderer>(true);
            if (src.Length == 0 || dst.Length == 0)
                return 0;

            int applied = 0;

            if (src.Length == dst.Length)
            {
                for (int i = 0; i < src.Length; i++)
                {
                    if (src[i].sharedMaterials == null)
                        continue;
                    dst[i].sharedMaterials = src[i].sharedMaterials;
                    applied++;
                }
            }
            else
            {
                var byPath = new Dictionary<string, Material[]>(StringComparer.Ordinal);
                foreach (var r in src)
                {
                    string p = RendererHierarchyPath(sourceRoot.transform, r);
                    if (p == null)
                        continue;
                    if (!byPath.ContainsKey(p) && r.sharedMaterials != null)
                        byPath[p] = r.sharedMaterials;
                }

                var matchedDst = new bool[dst.Length];
                for (int i = 0; i < dst.Length; i++)
                {
                    string p = RendererHierarchyPath(riggedRoot.transform, dst[i]);
                    if (p != null && byPath.TryGetValue(p, out Material[] mats) && mats != null)
                    {
                        dst[i].sharedMaterials = mats;
                        matchedDst[i] = true;
                        applied++;
                    }
                }

                if (applied == 0)
                {
                    int n = Math.Min(src.Length, dst.Length);
                    for (int i = 0; i < n; i++)
                    {
                        if (src[i].sharedMaterials == null)
                            continue;
                        dst[i].sharedMaterials = src[i].sharedMaterials;
                        applied++;
                    }
                }
            }

            if (applied > 0)
                AssetDatabase.SaveAssets();

            TJLog.Log(
                $"[RiggedModelPostProcessUtils] 绑骨后从源模型复用材质: {applied}/{dst.Length} 个 Renderer，源={sourceAssetPath}"
            );
            return applied;
        }

        /// <summary>
        /// 使用外部动作 FBX 中的剪辑，为绑骨后主模型创建单状态循环 AnimatorController。
        /// </summary>
        /// <returns>创建的 AnimatorController 资产路径；若失败返回 null。</returns>
        public static string CreateSingleClipLoopAnimatorControllerFromMotionClip(
            string modelDir,
            string targetRigBaseName,
            string motionFbxUnityPath)
        {
            try
            {
                const string assetsPrefix = "Assets/";
                if (modelDir != null && modelDir.StartsWith(assetsPrefix + assetsPrefix, StringComparison.OrdinalIgnoreCase))
                    modelDir = modelDir.Substring(assetsPrefix.Length);
                modelDir = modelDir?.Replace("\\", "/") ?? "";

                string controllerPath =
                    Path.Combine(modelDir, targetRigBaseName + "_Controller.controller").Replace("\\", "/");
                string controllerDir = Path.GetDirectoryName(controllerPath)?.Replace("\\", "/") ?? "";
                string absoluteControllerDir = PathUtils.ToAbsoluteAssetPath(controllerDir);
                if (!string.IsNullOrEmpty(absoluteControllerDir) && !Directory.Exists(absoluteControllerDir))
                    Directory.CreateDirectory(absoluteControllerDir);

                if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
                {
                    TJLog.Log($"[RiggedModelPostProcessUtils] Animator Controller 已存在，跳过创建: {controllerPath}");
                    return controllerPath;
                }

                AnimationClip clip = GetAnimationClipFromFbx(motionFbxUnityPath);
                if (clip == null)
                {
                    TJLog.LogWarning(
                        $"[RiggedModelPostProcessUtils] 无法从动作 FBX 提取剪辑，跳过控制器创建: {motionFbxUnityPath}"
                    );
                    return null;
                }

                var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                if (controller == null)
                {
                    TJLog.LogWarning($"[RiggedModelPostProcessUtils] 无法创建 Animator Controller: {controllerPath}");
                    return null;
                }

                var sm = controller.layers[0].stateMachine;
                AnimatorState previousDefault = sm.defaultState;
                string stateName = string.IsNullOrEmpty(clip.name) ? "Motion" : clip.name;
                var motionState = sm.AddState(stateName);
                motionState.motion = clip;
                motionState.writeDefaultValues = true;
                sm.defaultState = motionState;
                if (previousDefault != null && previousDefault != motionState)
                    sm.RemoveState(previousDefault);

                var selfLoop = motionState.AddTransition(motionState);
                selfLoop.hasExitTime = true;
                selfLoop.exitTime = 1f;
                selfLoop.duration = 0f;
                selfLoop.offset = 0f;
                selfLoop.hasFixedDuration = true;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                TJLog.Log(
                    $"[RiggedModelPostProcessUtils] 单剪辑循环 Animator Controller 已创建: {controllerPath} (clip={clip.name})"
                );
                return controllerPath;
            }
            catch (Exception e)
            {
                TJLog.LogError($"[RiggedModelPostProcessUtils] 创建 Animator Controller 失败: {e.Message}");
                return null;
            }
        }

        public static AnimationClip GetAnimationClipFromFbx(string fbxPath)
        {
            if (string.IsNullOrEmpty(fbxPath) || !File.Exists(PathUtils.ToAbsoluteAssetPath(fbxPath)))
                return null;

            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__"))
                .ToList();

            if (clips.Count > 0)
            {
                TJLog.Log($"[RiggedModelPostProcessUtils] 从 {fbxPath} 找到 {clips.Count} 个动画剪辑");
                return clips[0];
            }

            TJLog.LogWarning($"[RiggedModelPostProcessUtils] 未在 {fbxPath} 中找到动画剪辑");
            return null;
        }

        public static string RendererHierarchyPath(Transform modelRoot, Renderer renderer)
        {
            if (modelRoot == null || renderer == null)
                return null;

            Transform t = renderer.transform;
            var parts = new List<string>();
            while (t != null && t != modelRoot)
            {
                parts.Add(t.name);
                t = t.parent;
            }

            if (t != modelRoot)
                return null;

            parts.Reverse();
            return parts.Count == 0 ? string.Empty : string.Join("/", parts);
        }
    }
}
#endif
