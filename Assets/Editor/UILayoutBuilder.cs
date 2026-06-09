using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public static class UILayoutBuilder
{
    [MenuItem("Tools/Build Game UI Layout")]
    public static void BuildLayout()
    {
        var canvas = GameObject.Find("画布");
        if (canvas == null)
        {
            Debug.LogError("Canvas '画布' not found!");
            return;
        }

        // Clean up old UI elements if they exist
        var oldSidebar = canvas.transform.Find("右侧面板");
        if (oldSidebar != null) Object.DestroyImmediate(oldSidebar.gameObject);
        var oldBottom = canvas.transform.Find("底部信息栏");
        if (oldBottom != null) Object.DestroyImmediate(oldBottom.gameObject);
        var oldOverlay = canvas.transform.Find("点击遮罩");
        if (oldOverlay != null) Object.DestroyImmediate(oldOverlay.gameObject);

        // === 1. Right Sidebar Panel ===
        var sidebarObj = new GameObject("右侧面板");
        sidebarObj.transform.SetParent(canvas.transform, false);
        var sidebarRect = sidebarObj.GetComponent<RectTransform>();
        sidebarRect.anchorMin = new Vector2(1f, 0f);
        sidebarRect.anchorMax = new Vector2(1f, 1f);
        sidebarRect.pivot = new Vector2(1f, 0.5f);
        sidebarRect.anchoredPosition = Vector2.zero;
        sidebarRect.sizeDelta = new Vector2(240, 0);
        var sidebarBg = sidebarObj.AddComponent<Image>();
        sidebarBg.color = new Color(0.15f, 0.1f, 0.05f, 0.92f);
        var sidebarLayout = sidebarObj.AddComponent<VerticalLayoutGroup>();
        sidebarLayout.padding = new RectOffset(8, 8, 8, 8);
        sidebarLayout.spacing = 6;
        sidebarLayout.childAlignment = TextAnchor.UpperCenter;
        sidebarLayout.childControlWidth = true;
        sidebarLayout.childControlHeight = false;
        sidebarLayout.childForceExpandWidth = true;
        sidebarLayout.childForceExpandHeight = false;

        // Title
        CreateLabel(sidebarObj.transform, "标题", "🏗️ 建造塔", 40, 22, FontStyle.Bold,
            new Color(1f, 0.85f, 0.4f), new Color(0.25f, 0.15f, 0.05f, 1f));

        // Tower Button Container
        var containerObj = new GameObject("塔按钮容器");
        containerObj.transform.SetParent(sidebarObj.transform, false);
        var containerRect = containerObj.GetComponent<RectTransform>();
        containerRect.sizeDelta = new Vector2(0, 350);
        var containerLayout = containerObj.AddComponent<VerticalLayoutGroup>();
        containerLayout.padding = new RectOffset(4, 4, 4, 4);
        containerLayout.spacing = 8;
        containerLayout.childAlignment = TextAnchor.UpperCenter;
        containerLayout.childControlWidth = true;
        containerLayout.childControlHeight = false;
        containerLayout.childForceExpandWidth = true;
        containerLayout.childForceExpandHeight = false;
        var csf = containerObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Divider
        CreateLabel(sidebarObj.transform, "分割线", "───────────", 24, 14, FontStyle.Normal,
            new Color(0.6f, 0.5f, 0.3f, 0.5f), Color.clear);

        // Tower Info Section (hidden by default)
        var infoSectionObj = new GameObject("塔信息区");
        infoSectionObj.transform.SetParent(sidebarObj.transform, false);
        var infoSectionRect = infoSectionObj.GetComponent<RectTransform>();
        infoSectionRect.sizeDelta = new Vector2(0, 260);
        var infoSectionBg = infoSectionObj.AddComponent<Image>();
        infoSectionBg.color = new Color(0.2f, 0.12f, 0.06f, 0.95f);
        var infoSectionLayout = infoSectionObj.AddComponent<VerticalLayoutGroup>();
        infoSectionLayout.padding = new RectOffset(8, 8, 8, 8);
        infoSectionLayout.spacing = 4;
        infoSectionLayout.childAlignment = TextAnchor.UpperCenter;
        infoSectionLayout.childControlWidth = true;
        infoSectionLayout.childControlHeight = false;
        infoSectionLayout.childForceExpandWidth = true;
        infoSectionLayout.childForceExpandHeight = false;

        CreateLabel(infoSectionObj.transform, "信息标题", "ℹ️ 塔详情", 28, 18, FontStyle.Bold,
            new Color(1f, 0.85f, 0.4f), Color.clear);

        var towerName = CreateLabel(infoSectionObj.transform, "TowerNameText", "", 24, 16, FontStyle.Bold,
            Color.white, Color.clear);
        towerName.alignment = TextAnchor.MiddleCenter;

        var statsText = CreateLabel(infoSectionObj.transform, "StatsText", "", 80, 14, FontStyle.Normal,
            new Color(0.9f, 0.9f, 0.8f), Color.clear);
        statsText.alignment = TextAnchor.UpperLeft;
        statsText.lineSpacing = 1.3f;

        // Upgrade button
        var upgradeBtn = CreateButton(infoSectionObj.transform, "UpgradeButton", "⬆ 升级",
            36, new Color(0.2f, 0.5f, 0.2f, 1f));

        // Sell button
        var sellBtn = CreateButton(infoSectionObj.transform, "SellButton", "💰 出售",
            36, new Color(0.6f, 0.2f, 0.2f, 1f));

        infoSectionObj.SetActive(false);

        // === 2. Bottom Info Bar ===
        var bottomBarObj = new GameObject("底部信息栏");
        bottomBarObj.transform.SetParent(canvas.transform, false);
        var bottomBarRect = bottomBarObj.GetComponent<RectTransform>();
        bottomBarRect.anchorMin = new Vector2(0f, 0f);
        bottomBarRect.anchorMax = new Vector2(0.875f, 0f);
        bottomBarRect.pivot = new Vector2(0.5f, 0f);
        bottomBarRect.anchoredPosition = Vector2.zero;
        bottomBarRect.sizeDelta = new Vector2(0, 50);
        var bottomBarBg = bottomBarObj.AddComponent<Image>();
        bottomBarBg.color = new Color(0.12f, 0.08f, 0.03f, 0.9f);
        var bottomBarLayout = bottomBarObj.AddComponent<HorizontalLayoutGroup>();
        bottomBarLayout.padding = new RectOffset(20, 20, 8, 8);
        bottomBarLayout.spacing = 30;
        bottomBarLayout.childAlignment = TextAnchor.MiddleCenter;
        bottomBarLayout.childControlWidth = false;
        bottomBarLayout.childControlHeight = true;
        bottomBarLayout.childForceExpandWidth = false;
        bottomBarLayout.childForceExpandHeight = true;

        var waveTimerText = CreateLabel(bottomBarObj.transform, "WaveTimerText", "⏳ 下一波: 准备中", 0, 16, FontStyle.Normal,
            new Color(1f, 0.85f, 0.4f), Color.clear, 200);
        waveTimerText.alignment = TextAnchor.MiddleLeft;

        var enemyCountText = CreateLabel(bottomBarObj.transform, "EnemyCountText", "👾 敌人: 0", 0, 16, FontStyle.Normal,
            new Color(1f, 0.5f, 0.3f), Color.clear, 160);
        enemyCountText.alignment = TextAnchor.MiddleLeft;

        CreateButton(bottomBarObj.transform, "SpeedButton", "1x ⏩", 34, new Color(0.3f, 0.25f, 0.1f, 1f), 80);
        CreateButton(bottomBarObj.transform, "StartWaveButton", "▶ 开始波次", 34, new Color(0.6f, 0.2f, 0.1f, 1f), 120);

        // === 3. Click-Outside Overlay ===
        var overlayObj = new GameObject("点击遮罩");
        overlayObj.transform.SetParent(canvas.transform, false);
        var overlayRect = overlayObj.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        overlayObj.AddComponent<Image>().color = new Color(0, 0, 0, 0.3f);
        overlayObj.AddComponent<Button>().transition = Selectable.Transition.None;
        overlayObj.SetActive(false);

        // === 4. Move overlay to be first child (behind everything) ===
        overlayObj.transform.SetAsFirstSibling();

        Debug.Log("✅ UI Layout created successfully!");
    }

    static Text CreateLabel(Transform parent, string name, string text, float height, int fontSize,
        FontStyle fontStyle, Color textColor, Color bgColor, float width = 0)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        if (bgColor != Color.clear)
        {
            obj.AddComponent<Image>().color = bgColor;
        }
        var txt = obj.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = fontSize;
        txt.fontStyle = fontStyle;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        return txt;
    }

    static Button CreateButton(Transform parent, string name, string text, float height, Color bgColor, float width = 0)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        obj.AddComponent<Image>().color = bgColor;
        var btn = obj.AddComponent<Button>();
        var txt = obj.AddComponent<Text>();
        txt.text = text;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        txt.fontSize = 16;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        return btn;
    }
}
