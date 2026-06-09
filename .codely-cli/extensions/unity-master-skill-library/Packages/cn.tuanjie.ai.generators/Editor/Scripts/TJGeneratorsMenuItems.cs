using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;
using TJGenerators.AssetSearch;
using TJGenerators.Config;
using TJGenerators.Utils;

namespace TJGenerators
{
    /// <summary>
    /// TJGenerators 菜单项和 Inspector 按钮集成
    /// </summary>
    [InitializeOnLoad]
    public static class TJGeneratorsMenuItems
    {
        private const string DefaultNewAssetName = "New Mesh";
        private static readonly List<Component> k_ComponentsCache = new List<Component>();

        static TJGeneratorsMenuItems()
        {
            Editor.finishedDefaultHeaderGUI += OnHeaderControlsGUI;
        }

        #region Menu Items

        // Window 菜单顺序由 MenuItem priority 决定（数值越大越靠下）；过小会与 Next/Previous Window 等同区。
        [MenuItem("AI/生成/生成天空盒", false, 3001)]
        public static void CreateAIGeneratedSkybox()
        {
            CreateSkyboxAssetWithCallback("New Skybox.png");
        }

        [MenuItem("AI/生成/生成精灵", false, 3002)]
        public static void CreateAIGeneratedSprite()
        {
            CreateSpriteAssetWithCallback("New Sprite.png");
        }

        [MenuItem("AI/生成/生成图片", false, 3006)]
        public static void OpenImageGenerationWindow()
        {
            TJGeneratorsImageWindow.ShowWindow();
        }

        [MenuItem("AI/生成/生成序列帧（Frontier）", false, 3007)]
        public static void OpenFrontierSequenceImageWindow()
        {
            TJGeneratorsImageWindow.ShowFrontierSequenceWindow();
        }

        [MenuItem("AI/生成/生成视频", false, 3007)]
        public static void OpenVideoGenerationWindow()
        {
            TJGeneratorsVideoWindow.ShowWindow();
        }

        [MenuItem("AI/生成/生成表面材质", false, 3003)]
        public static void CreateAIGeneratedMaterial()
        {
            CreateMaterialAssetWithCallback("New Material.mat");
        }

        [MenuItem("AI/生成/生成音频", false, 3004)]
        public static void CreateAIGeneratedMusic()
        {
            CreateAudioClipAssetWithCallback("New AudioClip.wav");
        }

        [MenuItem("AI/生成/生成2D序列帧动画", false, 3005)]
        public static void CreateAIGeneratedSpriteSequence()
        {
            CreateAnimationClipAssetWithCallback("New Sprite Sequence.anim");
        }

        [MenuItem("AI/生成/生成3D模型", false, 3000)]
        public static void CreateAIGeneratedMesh()
        {
            CreatePrefabAssetWithCallback($"{DefaultNewAssetName}.prefab", enableLabel: true, sceneParentForInstance: null);
        }

        [MenuItem("AI/搜索资产库", false, 3010)]
        public static void OpenCodelyAssetLibrarySearch()
        {
            AssetLibrarySearchWindow.Open();
        }

        [MenuItem("AI/搜索生成的资产", false, 3011)]
        public static void SearchAIGeneratedAssets()
        {
            // 聚焦到 Project 窗口
            EditorUtility.FocusProjectWindow();

            // 设置搜索过滤器为 AI 生成标签
            var projectBrowserType = typeof(Editor).Assembly.GetType("UnityEditor.ProjectBrowser");
            if (projectBrowserType != null)
            {
                var window = EditorWindow.GetWindow(projectBrowserType);
                if (window != null)
                {
                    var setSearchMethod = projectBrowserType.GetMethod("SetSearch",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                        null,
                        new[] { typeof(string) },
                        null);
                    setSearchMethod?.Invoke(window, new object[] { $"l:{TJGeneratorsGenerationLabel.Label}" });
                }
            }
        }

        [MenuItem("AI/修复/固定GLB模型相关依赖", false, 3020)]
        public static void PinGlbDependenciesFromMenu()
        {
            DependencyPinning.PinDependenciesMenu();
        }

        [MenuItem("Assets/Create/3D/生成3D模型", false, -199)]
        public static void CreateTJGeneratorsMesh()
        {
            CreatePrefabAssetWithCallback($"{DefaultNewAssetName}.prefab", enableLabel: true, sceneParentForInstance: null);
        }

        [MenuItem("Assets/Create/3D/生成天空盒", false, -198)]
        public static void CreateTJGeneratorsSkybox()
        {
            CreateSkyboxAssetWithCallback("New Skybox.png");
        }

        [MenuItem("Assets/Create/3D/生成表面材质", false, -197)]
        public static void CreateTJGeneratorsMaterial()
        {
            CreateMaterialAssetWithCallback("New Material.mat");
        }

        [MenuItem("GameObject/3D Object/生成3D模型", false, -1)]
        public static void CreateInSceneAndNameMesh()
        {
            CreatePrefabAssetWithCallback($"{DefaultNewAssetName}.prefab", enableLabel: false, sceneParentForInstance: Selection.activeGameObject);
        }

        [MenuItem("Assets/Create/2D/生成2D精灵", false, -199)]
        public static void CreateTJGeneratorsSprite()
        {
            CreateSpriteAssetWithCallback("New Sprite.png");
        }

        [MenuItem("Assets/Create/2D/生成图片", false, -196)]
        public static void CreateTJGeneratorsImage()
        {
            CreateImageAssetWithCallback("New Image.jpg");
        }

        [MenuItem("GameObject/2D Object/生成2D精灵", false, -1)]
        public static void CreateInSceneAndNameSprite()
        {
            CreateSpriteAssetWithCallback("New Sprite.png");
        }

        [MenuItem("Assets/Create/2D/生成2D序列帧动画", false, -198)]
        public static void CreateTJGeneratorsSpriteSequence()
        {
            CreateAnimationClipAssetWithCallback("New Sprite Sequence.anim");
        }

        [MenuItem("GameObject/3D Object/生成天空盒", false, 0)]
        public static void CreateInSceneAndNameSkybox()
        {
            CreateSkyboxAssetWithCallback("New Skybox.png");
        }

        [MenuItem("GameObject/3D Object/生成表面材质", false, 1)]
        public static void CreateInSceneAndNameMaterial()
        {
            CreateMaterialAssetWithCallback("New Material.mat");
        }

        [MenuItem("Assets/Create/Audio/生成音频", false, -199)]
        public static void CreateTJGeneratorsAudioClip()
        {
            CreateAudioClipAssetWithCallback("New AudioClip.wav");
        }

#if TJGENERATORS_DEBUG
        [MenuItem("AI/开发/运行生成测试", false, 3050)]
        public static void OpenGenerationTestRunnerWindow()
        {
            TJGeneratorsGenerationTestRunner.Open();
        }

        [MenuItem("AI/开发/打印 Access Token", false, 3099)]
        public static void PrintAccessToken()
        {
            string token = Unity.UniAsset.Manager.Editor.InternalBridge.UnityConnectSession.instance.GetAccessToken();
            if (string.IsNullOrEmpty(token))
            {
                TJLog.LogWarning("[TJGenerators] Access Token 为空，请确认编辑器已登录 Unity 账号");
                ErrorDialogUtils.ShowErrorDialog("Access Token", "Token 为空，请先登录 Unity 账号。", "[TJGeneratorsMenuItems]");
            }
            else
            {
                TJLog.Log($"[TJGenerators] Access Token: {token}");
                EditorGUIUtility.systemCopyBuffer = token;
                TJLog.Log($"[TJGenerators] Token 已复制到剪贴板。\n{token}");
            }
        }

        [MenuItem("AI/开发/清除配置缓存并重新加载", false, 3100)]
        public static void ClearConfigCache()
        {
            ConfigManager.ClearCache();

            TJLog.Log(
                "[TJGenerators] 所有配置缓存已清除。请关闭并重新打开 TJGenerators 窗口以加载最新配置。"
                    + "\n本地配置文件：Editor/Config/GeneratorConfig.json"
            );
        }

        [MenuItem("AI/开发/生成纹理走势模板图", false, 3101)]
        public static void OpenMaterialTemplateGeneratorWindow()
        {
            TJGeneratorsMaterialTemplateGenerator.ShowWindow();
        }

        [MenuItem("AI/开发/生成类型风格图标", false, 3102)]
        public static void OpenIconGeneratorWindow()
        {
            TJGeneratorsIconGenerator.ShowWindow();
        }

        [MenuItem("AI/开发/一键清空所有历史记录", false, 3103)]
        public static void ClearAllGenerationHistory()
        {
            if (!EditorUtility.DisplayDialog(
                    "清空历史记录",
                    "确定要清空所有 TJGenerators 生成历史记录吗？此操作不可撤销。",
                    "清空",
                    "取消"))
            {
                return;
            }

            TJGeneratorsHistoryManager.ClearAllHistory();
        }
#endif

        #endregion

        #region Inspector Button

        private static void OnHeaderControlsGUI(Editor editor)
        {
            // 检查是否是Sprite资产（Texture2D 或 Sprite 子资产）
            string spritePath = null;

            // 情况1：直接选择Texture2D资产
            if (editor.target is Texture2D texture)
            {
                spritePath = AssetDatabase.GetAssetPath(texture);
            }
            // 情况2：选择Sprite子资产
            else if (editor.target is Sprite sprite)
            {
                spritePath = AssetDatabase.GetAssetPath(sprite);
            }
            // 情况3：Inspector显示的是TextureImporter（导入设置界面）
            else if (editor.target is TextureImporter textureImporter)
            {
                spritePath = textureImporter.assetPath;
            }

            if (!string.IsNullOrEmpty(spritePath))
            {
                var importer = AssetImporter.GetAtPath(spritePath) as TextureImporter;

                // 检查是否是Sprite类型的Texture2D
                if (importer != null && importer.textureType == TextureImporterType.Sprite)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(new GUIContent("生成", "使用 TJGenerators AI 生成2D精灵")))
                    {
                        TJGeneratorsSpriteWindow.OpenForAsset(spritePath);
                    }

                    EditorGUILayout.EndHorizontal();
                    return;
                }

                // 检查是否是图片资源（用于 TJGeneratorsImageWindow）
                if (importer != null &&
                    importer.textureType == TextureImporterType.Default &&
                    importer.textureShape != TextureImporterShape.TextureCube &&
                    (spritePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                     || spritePath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                     || spritePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(new GUIContent("生成", "使用 TJGenerators AI 生成图片")))
                    {
                        TJGeneratorsImageWindow.OpenForAsset(spritePath);
                    }

                    EditorGUILayout.EndHorizontal();
                    return;
                }

                // 检查是否是Cubemap类型（天空盒）
                if (importer != null && importer.textureShape == TextureImporterShape.TextureCube)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(new GUIContent("生成", "使用 TJGenerators AI 生成天空盒")))
                    {
                        TJGeneratorsSkyboxWindow.OpenForAsset(spritePath);
                    }

                    EditorGUILayout.EndHorizontal();
                    return;
                }
            }

            // 检查是否是Cubemap资产（天空盒）
            if (editor.target is Cubemap cubemap)
            {
                string path = AssetDatabase.GetAssetPath(cubemap);
                if (!string.IsNullOrEmpty(path))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(new GUIContent("生成", "使用 TJGenerators AI 生成天空盒")))
                    {
                        TJGeneratorsSkyboxWindow.OpenForAsset(path);
                    }

                    EditorGUILayout.EndHorizontal();
                }
                return;
            }

            // 检查是否是AudioClip资产
            string audioClipPath = null;

            // 情况1：直接选择AudioClip资产
            if (editor.target is UnityEngine.AudioClip audioClip)
            {
                audioClipPath = AssetDatabase.GetAssetPath(audioClip);
            }
            // 情况2：Inspector显示的是AudioImporter（导入设置界面）
            else if (editor.target is UnityEditor.AudioImporter audioImporter)
            {
                audioClipPath = audioImporter.assetPath;
            }

            if (!string.IsNullOrEmpty(audioClipPath))
            {
                var importer = AssetImporter.GetAtPath(audioClipPath) as UnityEditor.AudioImporter;

                // 检查是否是AudioClip
                if (importer != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(new GUIContent("生成", "使用 TJGenerators AI 生成音频")))
                    {
                        TJGeneratorsMusicWindow.OpenForAsset(audioClipPath);
                    }

                    EditorGUILayout.EndHorizontal();
                }
                return;
            }

            // 检查是否是Material资产
            if (editor.target is Material material)
            {
                string path = AssetDatabase.GetAssetPath(material);
                if (!string.IsNullOrEmpty(path))
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button(new GUIContent("生成", "使用 TJGenerators AI 生成表面材质")))
                    {
                        TJGeneratorsSpriteWindow.OpenForMaterialAsset(path);
                    }

                    EditorGUILayout.EndHorizontal();
                }
                return;
            }

            // 检查是否是持久化资产（Project中的prefab）
            if (EditorUtility.IsPersistent(editor.target))
            {
                if (!OnAssetGenerationValidation(editor.targets))
                    return;

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("生成", "使用 TJGenerators AI 生成3D模型")))
                {
                    OnAssetGenerationRequest(editor.targets);
                }

                EditorGUILayout.EndHorizontal();
            }
            // 检查是否是场景中的 prefab 实例
            else if (editor.target is GameObject sceneObject)
            {
                if (!OnScenePrefabInstanceValidation(sceneObject))
                    return;

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("生成", "使用 TJGenerators AI 生成3D模型到对应预制体")))
                {
                    OnScenePrefabInstanceGenerationRequest(sceneObject);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// 检查场景中的 GameObject 是否是有效的 prefab 实例，并且适合显示生成按钮
        /// </summary>
        private static bool OnScenePrefabInstanceValidation(GameObject sceneObject)
        {
            // 检查是否是 prefab 实例
            if (!PrefabUtility.IsPartOfPrefabInstance(sceneObject))
                return false;

            // 获取最近的 prefab 实例根节点
            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(sceneObject);
            if (prefabRoot == null)
                return false;

            // 只在选中的是 prefab 根节点时显示按钮（避免在子对象上重复显示）
            if (prefabRoot != sceneObject)
                return false;

            // 获取对应的 prefab 资产
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot);
            if (prefabAsset == null)
                return false;

            // 获取 prefab 路径
            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (string.IsNullOrEmpty(prefabPath) || !prefabPath.EndsWith(".prefab"))
                return false;

            // 有生成标签
            if (TJGeneratorsGenerationLabel.HasLabel(prefabAsset))
                return true;

            // 空 GameObject (只有 Transform 组件)
            if (IsEmptyGameObject(prefabAsset))
                return true;

            return false;
        }

        /// <summary>
        /// 处理场景中 prefab 实例的生成请求
        /// </summary>
        private static void OnScenePrefabInstanceGenerationRequest(GameObject sceneObject)
        {
            // 获取最近的 prefab 实例根节点
            var prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(sceneObject);
            if (prefabRoot == null)
                return;

            // 获取对应的 prefab 资产
            var prefabAsset = PrefabUtility.GetCorrespondingObjectFromSource(prefabRoot);
            if (prefabAsset == null)
                return;

            // 获取 prefab 路径
            string prefabPath = AssetDatabase.GetAssetPath(prefabAsset);
            if (!string.IsNullOrEmpty(prefabPath) && prefabPath.EndsWith(".prefab"))
            {
                TJGenerators3DModelWindow.OpenForAsset(prefabPath);
            }
        }

        private static bool OnAssetGenerationValidation(IEnumerable<UnityEngine.Object> objects)
        {
            foreach (var obj in objects)
            {
                if (AssetDatabase.IsOpenForEdit(obj) && TryGetValidPrefabPath(obj, out _))
                {
                    // 有生成标签
                    if (TJGeneratorsGenerationLabel.HasLabel(obj))
                        return true;

                    // 空 GameObject (只有 Transform 组件)
                    if (obj is GameObject gameObject && IsEmptyGameObject(gameObject))
                        return true;
                }
            }
            return false;
        }

        private static void OnAssetGenerationRequest(IEnumerable<UnityEngine.Object> objects)
        {
            foreach (var obj in objects)
            {
                if (TryGetValidPrefabPath(obj, out var validPath))
                {
                    TJGenerators3DModelWindow.OpenForAsset(validPath);
                }
            }
        }

        private static bool TryGetValidPrefabPath(UnityEngine.Object obj, out string path)
        {
            path = null;

            if (obj is GameObject)
            {
                path = AssetDatabase.GetAssetPath(obj);
            }

            if (string.IsNullOrEmpty(path))
                path = AssetDatabase.GetAssetPath(obj);

            if (!string.IsNullOrEmpty(path) && path.EndsWith(".prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                return prefab != null;
            }

            return false;
        }

        private static bool IsEmptyGameObject(GameObject gameObject)
        {
            gameObject.GetComponents(k_ComponentsCache);
            // 只有 Transform 组件表示是空的
            return k_ComponentsCache.Count == 1 && k_ComponentsCache[0] is Transform;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 创建天空盒资产的通用方法
        /// </summary>
        private static void CreateSkyboxAssetWithCallback(string defaultName)
        {
            var icon = EditorGUIUtility.ObjectContent(null, typeof(Cubemap))?.image as Texture2D;
            var doCreate = ScriptableObject.CreateInstance<DoCreateTJGeneratorsAsset>();
            doCreate.action = (instanceId, path, resourceFile) =>
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = TJGeneratorsSkyboxWindow.CreateBlankSkybox(path);
                if (string.IsNullOrEmpty(path))
                    return;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var cubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
                if (cubemap != null)
                {
                    Selection.activeObject = cubemap;
                    EditorGUIUtility.PingObject(cubemap);
                }

                TJGeneratorsSkyboxWindow.OpenForAsset(path);
            };

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, doCreate, defaultName, icon, null);
        }

        /// <summary>
        /// 创建 Sprite 资产并打开 TJGenerators Sprite 窗口
        /// </summary>
        private static void CreateSpriteAssetWithCallback(string defaultName)
        {
            var icon = EditorGUIUtility.ObjectContent(null, typeof(Texture2D))?.image as Texture2D;
            var doCreate = ScriptableObject.CreateInstance<DoCreateTJGeneratorsAsset>();
            doCreate.action = (instanceId, path, resourceFile) =>
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = TJGeneratorsSpriteWindow.CreateBlankSprite(path);
                if (string.IsNullOrEmpty(path))
                    return;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    Selection.activeObject = texture;
                    EditorGUIUtility.PingObject(texture);
                }

                // 延迟打开窗口，确保Inspector已刷新
                EditorApplication.delayCall += () =>
                {
                    TJGeneratorsSpriteWindow.OpenForAsset(path);
                };
            };

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, doCreate, defaultName, icon, null);
        }

        /// <summary>
        /// 创建图片（JPG）资产并打开 TJGenerators Image 窗口
        /// </summary>
        private static void CreateImageAssetWithCallback(string defaultName)
        {
            var icon = EditorGUIUtility.ObjectContent(null, typeof(Texture2D))?.image as Texture2D;
            var doCreate = ScriptableObject.CreateInstance<DoCreateTJGeneratorsAsset>();
            doCreate.action = (instanceId, path, resourceFile) =>
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                path = TJGeneratorsImageWindow.CreateBlankImage(path);
                if (string.IsNullOrEmpty(path))
                    return;

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture != null)
                {
                    Selection.activeObject = texture;
                    EditorGUIUtility.PingObject(texture);
                }

                // 延迟打开窗口，确保Inspector已刷新
                EditorApplication.delayCall += () =>
                {
                    TJGeneratorsImageWindow.OpenForAsset(path);
                };
            };

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, doCreate, defaultName, icon, null);
        }

        /// <summary>
        /// 创建 Material 资产并打开 TJGenerators Material 窗口
        /// </summary>
        private static void CreateMaterialAssetWithCallback(string defaultName)
        {
            var icon = EditorGUIUtility.ObjectContent(null, typeof(Material))?.image as Texture2D;
            var doCreate = ScriptableObject.CreateInstance<DoCreateTJGeneratorsAsset>();
            doCreate.action = (instanceId, path, resourceFile) =>
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                
                var surfaceShader = TJMaterialShaderUtility.ResolveSurfaceLitShader()
                                    ?? Shader.Find("Unlit/Texture");
                Material material = new Material(surfaceShader);
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                
                var loadedMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (loadedMaterial != null)
                {
                    Selection.activeObject = loadedMaterial;
                    EditorGUIUtility.PingObject(loadedMaterial);
                }

                // 延迟打开窗口，确保Inspector已刷新
                EditorApplication.delayCall += () =>
                {
                    TJGeneratorsSpriteWindow.OpenForMaterialAsset(path);
                };
            };

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, doCreate, defaultName, icon, null);
        }

        /// <summary>
        /// Project 窗口创建资源时用于解析「将落在哪个文件夹」（与 Unity 内置 Create 行为一致：取当前选中资产的父文件夹或所选文件夹）。
        /// </summary>
        private static string GetProjectBrowserInsertionFolderAssetPath()
        {
            foreach (UnityEngine.Object obj in Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets))
            {
                string p = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(p))
                    continue;
                p = p.Replace('\\', '/');
                if (AssetDatabase.IsValidFolder(p))
                    return p;
                string dir = Path.GetDirectoryName(p);
                if (!string.IsNullOrEmpty(dir))
                {
                    dir = dir.Replace('\\', '/');
                    if (AssetDatabase.IsValidFolder(dir))
                        return dir;
                }
                break;
            }
            return "Assets";
        }

        /// <summary>
        /// 创建 AudioClip 资产并打开生成音频窗口
        /// </summary>
        private static void CreateAudioClipAssetWithCallback(string defaultName)
        {
            string folder = GetProjectBrowserInsertionFolderAssetPath();
            if (string.IsNullOrWhiteSpace(defaultName))
                defaultName = "New AudioClip.wav";
            string fileName = Path.GetFileName(defaultName.Trim().Replace('\\', '/'));
            if (string.IsNullOrEmpty(fileName))
                fileName = "New AudioClip.wav";
            // Unity 内置命名只按单一扩展判重；若已与同基名的 .mp3/.m4a 等占位，会先显示冲突名再在回调里改掉。此处与 GenerateUniquePlaceholderWavPath 对齐，使初始输入框即为最终可用名。
            string defaultNameForDialog = Path.GetFileName(
                TJGeneratorsAudioAssetPathUtility.GenerateUniquePlaceholderWavPath($"{folder}/{fileName}"));

            var icon = EditorGUIUtility.ObjectContent(null, typeof(UnityEngine.AudioClip))?.image as Texture2D;
            var doCreate = ScriptableObject.CreateInstance<DoCreateTJGeneratorsAsset>();
            doCreate.action = (instanceId, path, resourceFile) =>
            {
                path = TJGeneratorsAudioAssetPathUtility.GenerateUniquePlaceholderWavPath(path);
                path = TJGeneratorsAudioUtils.CreateBlankAudioClip(path);
                if (string.IsNullOrEmpty(path))
                    return;
                var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.AudioClip>(path);
                if (clip != null)
                {
                    Selection.activeObject = clip;
                    EditorGUIUtility.PingObject(clip);
                }

                EditorApplication.delayCall += () =>
                {
                    TJGeneratorsMusicWindow.OpenForAsset(path);
                };
            };

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, doCreate, defaultNameForDialog, icon, null);
        }

        /// <summary>
        /// 创建 AnimationClip 资产并打开 2D 序列帧窗口
        /// </summary>
        private static void CreateAnimationClipAssetWithCallback(string defaultName)
        {
            var icon = EditorGUIUtility.ObjectContent(null, typeof(AnimationClip))?.image as Texture2D;
            var doCreate = ScriptableObject.CreateInstance<DoCreateTJGeneratorsAsset>();
            doCreate.action = (instanceId, path, resourceFile) =>
            {
                path = AssetDatabase.GenerateUniqueAssetPath(path);

                var clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                var loaded = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (loaded != null)
                {
                    Selection.activeObject = loaded;
                    EditorGUIUtility.PingObject(loaded);
                }

                // 延迟打开窗口，确保 Inspector 已刷新
                EditorApplication.delayCall += () =>
                {
                    TJGeneratorsSpriteSequenceWindow.OpenForAsset(path);
                };
            };

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, doCreate, defaultName, icon, null);
        }

        private static void CreatePrefabAssetWithCallback(string defaultName, bool enableLabel, GameObject sceneParentForInstance)
        {
            var icon = EditorGUIUtility.ObjectContent(null, typeof(GameObject))?.image as Texture2D;
            var doCreate = ScriptableObject.CreateInstance<DoCreateTJGeneratorsAsset>();
            var capturedParent = sceneParentForInstance;
            doCreate.action = (instanceId, path, resourceFile) =>
            {
                HandlePrefabCreation(path, enableLabel, capturedParent);
            };

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, doCreate, defaultName, icon, null);
        }

        private static void HandlePrefabCreation(string path, bool enableLabel, GameObject sceneParentForInstance)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            path = CreateBlankPrefab(path);
            if (string.IsNullOrEmpty(path))
                return;

            if (enableLabel)
            {
                TJGeneratorsGenerationLabel.EnableLabel(TJGeneratorsAssetReference.FromPath(path));
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                TJLog.LogError($"无法加载 Prefab: {path}");
                return;
            }

            // 在场景中实例化
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                if (sceneParentForInstance != null)
                {
                    GameObjectUtility.SetParentAndAlign(instance, sceneParentForInstance);
                }
                Undo.RegisterCreatedObjectUndo(instance, $"Create {instance.name}");
                Selection.activeObject = instance;
            }
            else
            {
                TJLog.LogError($"无法实例化 Prefab: {prefab.name}");
                return;
            }

            // 打开 TJGenerators 窗口绑定到该资产
            TJGenerators3DModelWindow.OpenForAsset(path);
        }


        /// <summary>
        /// 创建带 Cube 占位符的 Prefab
        /// </summary>
        private static string CreateBlankPrefab(string path)
        {
            path = Path.ChangeExtension(path, ".prefab");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var rootGameObject = new GameObject("Generated Mesh");
            try
            {
                // 创建 Cube 占位符作为子对象
                var placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                placeholder.name = "Placeholder";
                placeholder.transform.SetParent(rootGameObject.transform);
                placeholder.transform.localPosition = Vector3.zero;
                placeholder.transform.localRotation = Quaternion.identity;
                placeholder.transform.localScale = Vector3.one;

                PrefabUtility.SaveAsPrefabAsset(rootGameObject, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootGameObject);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TJGeneratorsGenerationLabel.EnableLabel(TJGeneratorsAssetReference.FromPath(path));

            return path;
        }

        #endregion
    }

    /// <summary>
    /// 用于 ProjectWindowUtil 的资产创建回调
    /// </summary>
    internal class DoCreateTJGeneratorsAsset : EndNameEditAction
    {
        public delegate void ActionHandler(int instanceId, string pathName, string resourceFile);
        public ActionHandler action { get; set; }

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            action?.Invoke(instanceId, pathName, resourceFile);
        }
    }
}
