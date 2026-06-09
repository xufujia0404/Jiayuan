#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Codely.Newtonsoft.Json;
using Codely.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using TJGenerators.Config;
using TJGenerators.UI;
using TJGenerators.Utils;
using Unity.UniAsset.Manager.Editor.InternalBridge;
using Unity.EditorCoroutines.Editor;

namespace TJGenerators
{
    /// <summary>
    /// 类型/风格图标生成器 - 使用 Seedream API 批量生成图标
    /// </summary>
    public class TJGeneratorsIconGenerator : EditorWindow
    {
        private const string API_BASE_URL = "https://ai-generator.tuanjie.cn/api/editor/";
        private const string ENDPOINT = "task/huoshan-seedream-45-async";
        private const string POLL_ENDPOINT = "task/{taskId}/id-status";

        // 生成状态
        private bool isGenerating = false;
        private int currentIndex = 0;
        private int totalCount = 0;
        private string currentStatus = "";
        private float progress = 0f;

        // 生成结果
        private List<IconGenerationResult> results = new List<IconGenerationResult>();

        // 配置
        private List<VisualSelectorOptionConfig> typeOptions = new List<VisualSelectorOptionConfig>();
        private List<VisualSelectorOptionConfig> styleOptions = new List<VisualSelectorOptionConfig>();

        // 生成模式
        private int generationMode = 0; // 0: 批量生成, 1: 单个生成
        private readonly string[] generationModeLabels = { "批量生成", "单个生成" };

        // 批量生成选项
        private bool generateTypeIcons = true;
        private bool generateStyleIcons = true;
        
        // 单个生成选项
        private int selectedCategory = 0; // 0: 类型, 1: 风格
        private int selectedOptionIndex = 0;
        private string[] typeOptionNames;
        private string[] styleOptionNames;

        // API 尺寸
        private int apiSizeIndex = 0; // 默认 2048x2048 (API 支持的最小尺寸)
        private readonly string[] apiSizeOptions = { "2048x2048", "2304x1728", "1728x2304", "2560x1440", "1440x2560" };
        
        // 最终图标尺寸 (下载后缩放)
        private int finalIconSizeIndex = 1; // 默认 512
        private readonly int[] finalIconSizeOptions = { 256, 512, 1024 };
        private readonly string[] finalIconSizeLabels = { "256x256", "512x512", "1024x1024" };

        // 滚动
        private Vector2 scrollPosition;

#if TJGENERATORS_DEBUG
        /// <summary>由 <see cref="TJGeneratorsMenuItems.OpenIconGeneratorWindow"/> 或外部代码调用。</summary>
        public static void ShowWindow()
        {
            var window = GetWindow<TJGeneratorsIconGenerator>(true, "TJGenerators 图标生成", true);
            window.minSize = new Vector2(500, 600);
            window.ShowUtility();
        }
#endif

        private void OnEnable()
        {
            LoadOptions();
        }

        private void LoadOptions()
        {
            typeOptions.Clear();
            styleOptions.Clear();

            var spriteConfig = ConfigManager.GetSpriteGeneratorConfig("huoshan_seedream");
            if (spriteConfig?.typeSelector?.options != null)
            {
                typeOptions.AddRange(spriteConfig.typeSelector.options);
            }
            if (spriteConfig?.styleSelector?.options != null)
            {
                styleOptions.AddRange(spriteConfig.styleSelector.options);
            }

            // 初始化选项名称数组
            typeOptionNames = typeOptions.Select(o => o.name + " (" + o.id + ")").ToArray();
            styleOptionNames = styleOptions.Select(o => o.name + " (" + o.id + ")").ToArray();

            TJLog.Log("[IconGenerator] 加载了 " + typeOptions.Count + " 个类型选项, " + styleOptions.Count + " 个风格选项");
        }

        private void OnGUI()
        {
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), CommonStyles.WindowBackgroundColor);
            GUILayout.Space(10);

            // 标题
            GUILayout.Label("类型/风格图标生成器", CommonStyles.HeaderStyle);
            GUILayout.Label("使用 Seedream API 为所有类型和风格生成代表性图标", EditorStyles.wordWrappedMiniLabel);

            GUILayout.Space(15);

            // 生成模式选择
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("生成模式:", GUILayout.Width(80));
            generationMode = GUILayout.SelectionGrid(generationMode, generationModeLabels, 2);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 选项区域
            DrawOptions();

            GUILayout.Space(10);

            // 统计信息
            DrawStatistics();

            GUILayout.Space(10);

            // 生成按钮
            DrawGenerateButton();

            GUILayout.Space(10);

            // 进度显示
            if (isGenerating)
            {
                DrawProgress();
            }

            GUILayout.Space(10);

            // 结果列表
            DrawResults();
        }

        private void DrawOptions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("生成选项", EditorStyles.boldLabel);

            if (generationMode == 0)
            {
                // 批量生成模式
                generateTypeIcons = EditorGUILayout.Toggle("生成类型图标", generateTypeIcons);
                generateStyleIcons = EditorGUILayout.Toggle("生成风格图标", generateStyleIcons);
            }
            else
            {
                // 单个生成模式
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("类别:", GUILayout.Width(80));
                selectedCategory = GUILayout.SelectionGrid(selectedCategory, new string[] { "类型", "风格" }, 2);
                EditorGUILayout.EndHorizontal();

                string[] optionNames = selectedCategory == 0 ? typeOptionNames : styleOptionNames;
                if (optionNames != null && optionNames.Length > 0)
                {
                    if (selectedOptionIndex >= optionNames.Length)
                        selectedOptionIndex = 0;

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("选择:", GUILayout.Width(80));
                    selectedOptionIndex = EditorGUILayout.Popup(selectedOptionIndex, optionNames);
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("API图片尺寸:", GUILayout.Width(80));
            apiSizeIndex = EditorGUILayout.Popup(apiSizeIndex, apiSizeOptions);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("最终图标尺寸:", GUILayout.Width(80));
            finalIconSizeIndex = EditorGUILayout.Popup(finalIconSizeIndex, finalIconSizeLabels);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox("API 生成后会缩放到最终图标尺寸", MessageType.Info);

            EditorGUILayout.EndVertical();
        }

        private void DrawStatistics()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            if (generationMode == 0)
            {
                // 批量生成模式
                int typeCount = generateTypeIcons ? typeOptions.Count : 0;
                int styleCount = generateStyleIcons ? styleOptions.Count : 0;
                totalCount = typeCount + styleCount;

                GUILayout.Label("待生成: " + totalCount + " 个图标", EditorStyles.boldLabel);
                GUILayout.Label("  - 类型图标: " + typeCount + " 个", EditorStyles.miniLabel);
                GUILayout.Label("  - 风格图标: " + styleCount + " 个", EditorStyles.miniLabel);
            }
            else
            {
                // 单个生成模式
                totalCount = 1;
                string[] optionNames = selectedCategory == 0 ? typeOptionNames : styleOptionNames;
                string selectedName = (optionNames != null && selectedOptionIndex < optionNames.Length) 
                    ? optionNames[selectedOptionIndex] : "无";
                GUILayout.Label("待生成: 1 个图标", EditorStyles.boldLabel);
                GUILayout.Label("  - " + (selectedCategory == 0 ? "类型" : "风格") + ": " + selectedName, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGenerateButton()
        {
            GUI.enabled = !isGenerating && totalCount > 0;

            string buttonText = isGenerating ? "生成中..." : (generationMode == 0 ? "开始批量生成" : "生成选中项");
            if (GUILayout.Button(buttonText, CommonStyles.ButtonStyle))
            {
                StartGeneration();
            }

            GUI.enabled = true;
        }

        private void DrawProgress()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("进度: " + currentIndex + "/" + totalCount, EditorStyles.boldLabel);

            Rect progressRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(progressRect, progress, Mathf.RoundToInt(progress * 100) + "%");

            GUILayout.Label(currentStatus, EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawResults()
        {
            if (results.Count == 0) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            GUILayout.Label("生成结果", EditorStyles.boldLabel);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

            foreach (var result in results)
            {
                EditorGUILayout.BeginHorizontal();

                // 状态图标
                string statusIcon = result.success ? "OK" : "X";
                Color statusColor = result.success ? Color.green : Color.red;
                var oldColor = GUI.color;
                GUI.color = statusColor;
                GUILayout.Label(statusIcon, GUILayout.Width(25));
                GUI.color = oldColor;

                // 名称
                GUILayout.Label(result.name, GUILayout.Width(150));

                // 类型
                GUILayout.Label(result.isType ? "类型" : "风格", GUILayout.Width(50));

                // 状态
                GUILayout.Label(result.success ? result.savePath : result.error, EditorStyles.miniLabel);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.EndVertical();
        }

private void StartGeneration()
{
    if (generationMode == 0)
    {
        // 批量生成模式
        int typeCount = generateTypeIcons ? typeOptions.Count : 0;
        int styleCount = generateStyleIcons ? styleOptions.Count : 0;
        totalCount = typeCount + styleCount;

        if (totalCount == 0)
        {
            TJLog.LogWarning("[IconGenerator] 没有需要生成的图标");
            return;
        }

        results.Clear();
        isGenerating = true;
        currentIndex = 0;
        progress = 0f;
        currentStatus = "准备生成...";
        Repaint();

        TJLog.Log("[IconGenerator] 开始批量生成，总数: " + totalCount + " (类型: " + typeCount + ", 风格: " + styleCount + ")");
        EditorCoroutineUtility.StartCoroutineOwnerless(GenerateAllIcons());
    }
    else
    {
        // 单个生成模式
        totalCount = 1;
        results.Clear();
        isGenerating = true;
        currentIndex = 0;
        progress = 0f;
        currentStatus = "准备生成...";
        Repaint();

        var option = selectedCategory == 0 ? typeOptions[selectedOptionIndex] : styleOptions[selectedOptionIndex];
        TJLog.Log("[IconGenerator] 开始单个生成: " + option.name);
        EditorCoroutineUtility.StartCoroutineOwnerless(GenerateSingleIconCoroutine(option, selectedCategory == 0));
    }
}

private IEnumerator GenerateSingleIconCoroutine(VisualSelectorOptionConfig option, bool isType)
{
    string apiSize = apiSizeOptions[apiSizeIndex];
    int finalSize = finalIconSizeOptions[finalIconSizeIndex];

    yield return GenerateSingleIcon(option, isType, apiSize, finalSize);

    currentIndex = 1;
    progress = 1f;
    isGenerating = false;

    bool ok = results.Count > 0 && results[results.Count - 1].success;
    currentStatus = ok ? "生成成功!" : "生成失败";
    Repaint();

    TJLog.Log("[IconGenerator] 单个生成完成: " + option.name + ", 结果: " + (ok ? "成功" : "失败"));

    AssetDatabase.Refresh();

    if (ok)
    {
        UpdateConfigFile();
    }
}

private IEnumerator GenerateAllIcons()
{
    TJLog.Log("[IconGenerator] GenerateAllIcons 开始执行");

    string apiSize = apiSizeOptions[apiSizeIndex];
    int finalSize = finalIconSizeOptions[finalIconSizeIndex];
    TJLog.Log("[IconGenerator] API尺寸: " + apiSize + ", 最终图标尺寸: " + finalSize);

    // 生成类型图标
    if (generateTypeIcons)
    {
        TJLog.Log("[IconGenerator] 开始生成类型图标，数量: " + typeOptions.Count);
        foreach (var typeOption in typeOptions)
        {
            TJLog.Log("[IconGenerator] 准备生成类型: " + typeOption.name);
            yield return GenerateSingleIcon(typeOption, isType: true, apiSize, finalSize);
            currentIndex++;
            progress = (float)currentIndex / totalCount;
            Repaint();
        }
    }

    // 生成风格图标
    if (generateStyleIcons)
{
                TJLog.Log("[IconGenerator] 开始生成风格图标，数量: " + styleOptions.Count);
                foreach (var styleOption in styleOptions)
                {
                    TJLog.Log("[IconGenerator] 准备生成风格: " + styleOption.name);
                    yield return GenerateSingleIcon(styleOption, isType: false, apiSize, finalSize);
                    currentIndex++;
                    progress = (float)currentIndex / totalCount;
                    Repaint();
                }
            }

            isGenerating = false;
            int successCount = results.FindAll(r => r.success).Count;
            int failCount = results.Count - successCount;
            currentStatus = "完成! 成功: " + successCount + ", 失败: " + failCount;
            Repaint();

            TJLog.Log("[IconGenerator] 生成完成! 成功: " + successCount + ", 失败: " + failCount);

            // 刷新资源
            AssetDatabase.Refresh();

            // 自动更新配置文件
            if (successCount > 0)
            {
                UpdateConfigFile();
            }
        }

        private IEnumerator GenerateSingleIcon(VisualSelectorOptionConfig option, bool isType, string apiSize, int finalSize)
        {
            currentStatus = "正在生成: " + option.name + "...";

            // 构建 prompt
            string prompt = BuildPromptForOption(option, isType);
            TJLog.Log("[IconGenerator] 生成 " + option.name + ", prompt: " + prompt);

            // 发送 API 请求
            string taskId = null;
            yield return SendGenerationRequest(prompt, apiSize, result => taskId = result);

            if (string.IsNullOrEmpty(taskId))
            {
                results.Add(new IconGenerationResult
                {
                    name = option.name,
                    optionId = option.id,
                    isType = isType,
                    success = false,
                    error = "创建任务失败"
                });
                yield break;
            }

            // 轮询任务状态
            string imageUrl = null;
            yield return PollTaskStatus(taskId, result => imageUrl = result);

            if (string.IsNullOrEmpty(imageUrl))
            {
                results.Add(new IconGenerationResult
                {
                    name = option.name,
                    optionId = option.id,
                    isType = isType,
                    success = false,
                    error = "任务超时或失败"
                });
                yield break;
            }

            // 下载图片
            byte[] imageData = null;
            yield return DownloadImage(imageUrl, result => imageData = result);

            if (imageData == null || imageData.Length == 0)
            {
                results.Add(new IconGenerationResult
                {
                    name = option.name,
                    optionId = option.id,
                    isType = isType,
                    success = false,
                    error = "下载图片失败"
                });
                yield break;
            }

            // 缩放图片到最终尺寸
            byte[] resizedImageData = ResizeImage(imageData, finalSize);
            if (resizedImageData == null)
            {
                results.Add(new IconGenerationResult
                {
                    name = option.name,
                    optionId = option.id,
                    isType = isType,
                    success = false,
                    error = "缩放图片失败"
                });
                yield break;
            }

            // 保存图片
            string savePath = SaveImage(option.id, resizedImageData, isType);

            results.Add(new IconGenerationResult
            {
                name = option.name,
                optionId = option.id,
                isType = isType,
                success = true,
                savePath = savePath
            });
        }

        private string BuildPromptForOption(VisualSelectorOptionConfig option, bool isType)
        {
            var sb = new StringBuilder();

            // 基础提示词 - 生成适合作为图标的图片
            sb.Append("game asset icon, ");
            sb.Append("single centered subject, ");
            sb.Append("solid pure white background, ");
            sb.Append("clean design, ");
            sb.Append("no text, ");
            sb.Append("no watermark, ");
            sb.Append("high quality, ");
            sb.Append("detailed, ");

            if (isType)
            {
                // 类型特定的提示词
                sb.Append(GetTypePrompt(option.id));
            }
            else
            {
                // 风格特定的提示词
                sb.Append(GetStylePrompt(option.id));
            }

            return sb.ToString();
        }

        private string GetTypePrompt(string typeId)
        {
            switch (typeId)
            {
                // 武器
                case "weapon_melee": return "fantasy melee weapon icon, sword, axe, hammer, crossed weapons, game UI icon style";
                case "weapon_ranged": return "fantasy ranged weapon icon, bow, crossbow, arrow, game UI icon style";
                case "weapon_magic": return "magic weapon icon, staff, wand, glowing crystal, magical energy, game UI icon style";

                // 护甲
                case "armor_head": return "fantasy helmet icon, knight helmet, crown, game UI icon style";
                case "armor_body": return "fantasy armor chest icon, breastplate, robe, game UI icon style";
                case "armor_shield": return "fantasy shield icon, round shield, kite shield, game UI icon style";
                case "armor_accessory": return "fantasy accessory icon, ring, necklace, amulet, glowing gem, game UI icon style";

                // 消耗品
                case "consumable_hp": return "health potion icon, red potion bottle, healing flask, game UI icon style";
                case "consumable_mp": return "mana potion icon, blue potion bottle, magic flask, game UI icon style";
                case "consumable_buff": return "buff potion icon, green potion, power up flask, game UI icon style";

                // 材料
                case "material_common": return "common material icon, ore, herb, wood, crafting material, game UI icon style";
                case "material_rare": return "rare material icon, glowing crystal, dragon scale, mithril, game UI icon style";
                case "material_epic": return "epic material icon, ancient artifact, divine essence, legendary item, game UI icon style";

                // 特殊物品
                case "key_item": return "key item icon, ancient key, quest item, glowing artifact, game UI icon style";
                case "quest_item": return "quest item icon, scroll, map, treasure chest, game UI icon style";

                // 工具
                case "tool_gathering": return "gathering tool icon, pickaxe, sickle, fishing rod, game UI icon style";
                case "tool_crafting": return "crafting tool icon, hammer, anvil, workbench, game UI icon style";

                // 角色
                case "mount": return "mount icon, horse, dragon, flying carpet, game UI icon style";
                case "pet": return "pet icon, wolf, eagle, fairy companion, game UI icon style";
                case "character_hero": return "hero portrait icon, fantasy warrior, brave adventurer, game UI icon style";
                case "character_npc": return "NPC portrait icon, fantasy villager, merchant, game UI icon style";
                case "monster": return "monster icon, fantasy creature, enemy, game UI icon style";

                // 场景
                case "furniture": return "furniture icon, chair, table, bed, game UI icon style";
                case "decoration": return "decoration icon, statue, banner, carpet, game UI icon style";

                // UI
                case "ui_button": return "UI button icon, game interface button, interactive element, game UI icon style";
                case "ui_icon": return "UI function icon, settings gear, menu icon, game UI icon style";
                case "ui_frame": return "UI frame icon, panel border, decorative frame, game UI icon style";

                // 技能
                case "skill_active": return "active skill icon, combat ability, glowing power, game UI icon style";
                case "skill_passive": return "passive skill icon, permanent buff, aura effect, game UI icon style";

                // 效果
                case "effect_buff": return "buff effect icon, positive status, green glow, game UI icon style";
                case "effect_debuff": return "debuff effect icon, negative status, red glow, game UI icon style";

                default: return typeId + " icon, game UI icon style, fantasy game asset";
            }
        }

        private string GetStylePrompt(string styleId)
        {
            switch (styleId)
            {
                // 像素风格
                case "pixel": return "pixel art style icon, retro game aesthetic, 16-bit pixel graphics";
                case "pixel_8bit": return "8-bit pixel art icon, classic NES style, retro game graphics";
                case "pixel_16bit": return "16-bit pixel art icon, SNES style, detailed pixel graphics";

                // 卡通风格
                case "cartoon": return "American cartoon style icon, bold outlines, vibrant colors, animated style";
                case "anime": return "Japanese anime style icon, manga aesthetic, cel shaded, anime art";
                case "chibi": return "chibi kawaii style icon, cute, big head, adorable, super deformed";

                // 写实风格
                case "realistic": return "realistic style icon, photorealistic, detailed rendering, high fidelity";
                case "semi_realistic": return "semi-realistic style icon, painterly, artistic realism";

                // 现代风格
                case "flat": return "flat design icon, minimalist, clean shapes, modern UI style";
                case "vector": return "vector art icon, clean lines, scalable graphics, modern design";

                // 绘画风格
                case "watercolor": return "watercolor painting style icon, soft colors, artistic, painted look";
                case "oil_painting": return "oil painting style icon, classical art, rich colors, painterly";
                case "sketch": return "sketch style icon, pencil drawing, rough lines, hand drawn";
                case "lineart": return "line art icon, clean outlines, black and white, minimal";

                // 渲染风格
                case "cell_shading": return "cel shading style icon, anime shading, flat colors with shadows";
                case "low_poly": return "low poly style icon, geometric shapes, minimalist 3D, faceted";

                // 视角风格
                case "isometric": return "isometric view icon, 2.5D perspective, diagonal view, game asset";
                case "top_down": return "top down view icon, bird's eye view, overhead perspective";
                case "side_scroller": return "side scroller view icon, 2D platformer style, side view";

                // 题材风格
                case "cyberpunk": return "cyberpunk style icon, neon lights, futuristic, sci-fi, tech";
                case "steampunk": return "steampunk style icon, brass gears, Victorian era, mechanical";
                case "fantasy": return "fantasy style icon, magical, medieval, enchanted, mystical";
                case "sci_fi": return "sci-fi style icon, futuristic, space, technology, alien";
                case "horror": return "horror style icon, dark, spooky, creepy, gothic horror";
                case "cute": return "cute style icon, adorable, kawaii, pastel colors, sweet";
                case "gothic": return "gothic style icon, dark, mysterious, Victorian gothic, ornate";
                case "medieval": return "medieval style icon, knights, castles, Renaissance, historical";

                // 其他风格
                case "minimalist": return "minimalist style icon, simple, clean, essential elements only";
                case "hand_drawn": return "hand drawn style icon, organic lines, artistic, sketchy";
                case "stained_glass": return "stained glass style icon, colorful glass, religious art, decorative";

                default: return styleId + " style icon, game UI icon style";
            }
        }

        private IEnumerator SendGenerationRequest(string prompt, string apiSize, Action<string> onComplete)
        {
            string url = API_BASE_URL + ENDPOINT;
            TJLog.Log("[IconGenerator] SendGenerationRequest 开始, URL: " + url);

            // 构建请求 JSON
            string json = BuildRequestJson(prompt, apiSize);
            TJLog.Log("[IconGenerator] 请求JSON: " + json);

            byte[] postData = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest uwr = new UnityWebRequest(url, "POST"))
            {
                uwr.uploadHandler = new UploadHandlerRaw(postData);
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.SetRequestHeader("Content-Type", "application/json");
                uwr.timeout = 60;

                // 获取 token
                string token = null;
                try
                {
                    token = UnityConnectSession.instance.GetAccessToken();
                }
                catch (Exception e)
                {
                    TJLog.LogError("[IconGenerator] 获取token失败: " + e.Message);
                    onComplete?.Invoke(null);
                    yield break;
                }

                if (string.IsNullOrEmpty(token))
                {
                    TJLog.LogError("[IconGenerator] Token为空，请确保已登录Unity");
                    onComplete?.Invoke(null);
                    yield break;
                }

                uwr.SetRequestHeader("Authorization", "Bearer " + token);
                uwr.SetRequestHeader("source", "codely");

                TJLog.Log("[IconGenerator] 发送请求...");
                yield return uwr.SendWebRequest();
                
                // 等待请求完成 - 和 AIReferenceImageWindow 一样的方式
                float elapsed = 0f;
                while (uwr.result == UnityWebRequest.Result.InProgress && elapsed < 60f)
                {
                    double t = EditorApplication.timeSinceStartup;
                    while (EditorApplication.timeSinceStartup - t < 0.5) yield return null;
                    elapsed += 0.5f;
                }

                TJLog.Log("[IconGenerator] 请求完成, result: " + uwr.result);

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        string response = uwr.downloadHandler.text;
                        TJLog.Log("[IconGenerator] 响应: " + response);

                        var root = JObject.Parse(response);
                        string taskId = root.Value<string>("taskId") ?? root.Value<string>("task_id");
                        onComplete?.Invoke(taskId);
                    }
                    catch (Exception e)
                    {
                        TJLog.LogError("[IconGenerator] 解析响应失败: " + e.Message);
                        onComplete?.Invoke(null);
                    }
                }
                else
                {
                    TJLog.LogError("[IconGenerator] 请求失败: " + uwr.error + ", response: " + uwr.downloadHandler?.text);
                    onComplete?.Invoke(null);
                }
            }
        }

        private string BuildRequestJson(string prompt, string apiSize)
        {
            var o = new JObject
            {
                ["prompt"] = prompt,
                ["size"] = apiSize,
                ["responseFormat"] = "url",
                ["stream"] = false,
                ["watermark"] = false,
                ["sequentialImageGeneration"] = "disabled"
            };
            return o.ToString(Formatting.None);
        }

        private IEnumerator PollTaskStatus(string taskId, Action<string> onComplete)
        {
            string url = API_BASE_URL + POLL_ENDPOINT.Replace("{taskId}", taskId);
            TJLog.Log("[IconGenerator] 开始轮询任务状态: " + taskId);

            int maxRetries = 60; // 最多等待5分钟
            int retryCount = 0;

            while (retryCount < maxRetries)
            {
                retryCount++;
                TJLog.Log("[IconGenerator] 轮询第 " + retryCount + " 次");

                using (UnityWebRequest uwr = UnityWebRequest.Get(url))
                {
                    string token = null;
                    try
                    {
                        token = UnityConnectSession.instance.GetAccessToken();
                    }
                    catch
                    {
                        TJLog.LogError("[IconGenerator] 获取token失败");
                        onComplete?.Invoke(null);
                        yield break;
                    }

                    uwr.SetRequestHeader("Authorization", "Bearer " + token);
                    uwr.SetRequestHeader("source", "codely");
                    uwr.timeout = 30;

                    yield return uwr.SendWebRequest();
                    
                    // 等待请求完成
                    float elapsed = 0f;
                    while (uwr.result == UnityWebRequest.Result.InProgress && elapsed < 30f)
                    {
                        double t = EditorApplication.timeSinceStartup;
                        while (EditorApplication.timeSinceStartup - t < 0.5) yield return null;
                        elapsed += 0.5f;
                    }

                    if (uwr.result == UnityWebRequest.Result.Success)
                    {
                        try
                        {
                            string response = uwr.downloadHandler.text;
                            var root = JObject.Parse(response);
                            string status = root.Value<string>("status");
                            TJLog.Log("[IconGenerator] 任务状态: " + status);

                            if (status == "completed")
                            {
                                string imageUrl = null;
                                var urls = root["output"]?["data"]?["result"]?["image_urls"] as JArray;
                                if (urls != null && urls.Count > 0)
                                    imageUrl = urls[0].Value<string>();

                                TJLog.Log("[IconGenerator] 任务完成, imageUrl: " + imageUrl);
                                onComplete?.Invoke(imageUrl);
                                yield break;
                            }

                            if (status == "failed" || status == "error")
                            {
                                TJLog.LogError(
                                    "[IconGenerator] 任务失败: "
                                        + status
                                        + ", 错误: "
                                        + (root.Value<string>("error") ?? root.Value<string>("message") ?? "未知错误"));
                                onComplete?.Invoke(null);
                                yield break;
                            }
                        }
                        catch (Exception e)
                        {
                            TJLog.LogError("[IconGenerator] 解析状态响应失败: " + e.Message);
                        }
                    }
                    else
                    {
                        TJLog.LogWarning("[IconGenerator] 轮询请求失败: " + uwr.error);
                    }
                }

                // 等待5秒
                double waitStart = EditorApplication.timeSinceStartup;
                while (EditorApplication.timeSinceStartup - waitStart < 5.0)
                {
                    yield return null;
                }
            }

            TJLog.LogError("[IconGenerator] 轮询超时");
            onComplete?.Invoke(null);
        }

        private IEnumerator DownloadImage(string url, Action<byte[]> onComplete)
        {
            TJLog.Log("[IconGenerator] 下载图片: " + url);
            
            using (UnityWebRequest uwr = UnityWebRequest.Get(url))
            {
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.timeout = 60;
                yield return uwr.SendWebRequest();
                
                // 等待请求完成
                float elapsed = 0f;
                while (uwr.result == UnityWebRequest.Result.InProgress && elapsed < 60f)
                {
                    double t = EditorApplication.timeSinceStartup;
                    while (EditorApplication.timeSinceStartup - t < 0.5) yield return null;
                    elapsed += 0.5f;
                }

                if (uwr.result == UnityWebRequest.Result.Success)
                {
                    TJLog.Log("[IconGenerator] 下载完成, 大小: " + uwr.downloadHandler.data.Length + " bytes");
                    onComplete?.Invoke(uwr.downloadHandler.data);
                }
                else
                {
                    TJLog.LogError("[IconGenerator] 下载图片失败: " + uwr.error);
                    onComplete?.Invoke(null);
                }
            }
        }

        /// <summary>
        /// 缩放图片到指定尺寸
        /// </summary>
        private byte[] ResizeImage(byte[] imageData, int targetSize)
        {
            try
            {
                // 加载原始图片
                Texture2D originalTexture = new Texture2D(2, 2);
                if (!originalTexture.LoadImage(imageData))
                {
                    TJLog.LogError("[IconGenerator] 无法加载图片数据");
                    return null;
                }

                int originalWidth = originalTexture.width;
                int originalHeight = originalTexture.height;
                TJLog.Log("[IconGenerator] 原始图片尺寸: " + originalWidth + "x" + originalHeight);

                // 如果已经是目标尺寸，直接返回
                if (originalWidth == targetSize && originalHeight == targetSize)
                {
                    return imageData;
                }

                // 创建缩放后的纹理
                Texture2D resizedTexture = new Texture2D(targetSize, targetSize, TextureFormat.RGBA32, false);
                
                // 使用双线性插值缩放
                Color[] pixels = new Color[targetSize * targetSize];
                for (int y = 0; y < targetSize; y++)
                {
                    for (int x = 0; x < targetSize; x++)
                    {
                        float u = (float)x / (targetSize - 1);
                        float v = (float)y / (targetSize - 1);
                        int srcX = Mathf.RoundToInt(u * (originalWidth - 1));
                        int srcY = Mathf.RoundToInt(v * (originalHeight - 1));
                        pixels[y * targetSize + x] = originalTexture.GetPixel(srcX, srcY);
                    }
                }
                resizedTexture.SetPixels(pixels);
                resizedTexture.Apply();

                // 编码为 PNG
                byte[] pngData = resizedTexture.EncodeToPNG();

                // 清理
                UnityEngine.Object.DestroyImmediate(originalTexture);
                UnityEngine.Object.DestroyImmediate(resizedTexture);

                TJLog.Log("[IconGenerator] 缩放后尺寸: " + targetSize + "x" + targetSize);
                return pngData;
            }
            catch (Exception e)
            {
                TJLog.LogError("[IconGenerator] 缩放图片失败: " + e.Message);
                return null;
            }
        }

        private string SaveImage(string optionId, byte[] imageData, bool isType)
        {
            // 确定保存目录
            string subFolder = isType ? "TypeIcons" : "StyleIcons";
            string saveDir = "Packages/cn.tuanjie.ai.generators/Editor/EditorTextures/" + subFolder;

            // 转换为绝对路径
            string absoluteDir = Path.Combine(Application.dataPath, "..", saveDir);
            if (!Directory.Exists(absoluteDir))
            {
                Directory.CreateDirectory(absoluteDir);
            }

            // 生成文件名
            string fileName = optionId + ".png";
            string absolutePath = Path.Combine(absoluteDir, fileName);
            File.WriteAllBytes(absolutePath, imageData);

            TJLog.Log("[IconGenerator] 保存图片: " + absolutePath);

            return saveDir + "/" + fileName;
        }

        /// <summary>
        /// 更新配置文件，添加 iconPath 字段
        /// </summary>
        private void UpdateConfigFile()
        {
            const string spriteGeneratorId = "huoshan_seedream";
            string configPath = "Packages/cn.tuanjie.ai.generators/Editor/Config/GeneratorConfig.json";
            string absolutePath = Path.Combine(Application.dataPath, "..", configPath);

            if (!File.Exists(absolutePath))
            {
                TJLog.LogError("[IconGenerator] 配置文件不存在: " + absolutePath);
                return;
            }

            JObject root;
            try
            {
                root = JObject.Parse(File.ReadAllText(absolutePath));
            }
            catch (Exception e)
            {
                TJLog.LogError("[IconGenerator] 解析配置文件失败: " + e.Message);
                return;
            }

            var spriteGenerators = root["spriteGenerators"] as JArray;
            if (spriteGenerators == null)
            {
                TJLog.LogError("[IconGenerator] 配置文件缺少 spriteGenerators");
                return;
            }

            JObject generator = null;
            foreach (var token in spriteGenerators)
            {
                if (token is JObject gen && gen.Value<string>("id") == spriteGeneratorId)
                {
                    generator = gen;
                    break;
                }
            }

            if (generator == null)
            {
                TJLog.LogError("[IconGenerator] 未找到 spriteGenerators 项: " + spriteGeneratorId);
                return;
            }

            bool modified = false;
            foreach (var result in results)
            {
                if (!result.success) continue;
                string selectorKey = result.isType ? "typeSelector" : "styleSelector";
                var selector = generator[selectorKey] as JObject;
                var options = selector?["options"] as JArray;
                if (options == null) continue;

                foreach (var opt in options)
                {
                    if (opt is JObject o && o.Value<string>("id") == result.optionId)
                    {
                        o["iconPath"] = result.savePath;
                        modified = true;
                        break;
                    }
                }
            }

            if (modified)
            {
                File.WriteAllText(absolutePath, root.ToString(Formatting.Indented));
                TJLog.Log("[IconGenerator] 已更新配置文件: " + configPath);
                AssetDatabase.Refresh();
            }
        }

        private class IconGenerationResult
        {
            public string name;
            public string optionId;
            public bool isType;
            public bool success;
            public string savePath;
            public string error;
        }
    }
}
#endif