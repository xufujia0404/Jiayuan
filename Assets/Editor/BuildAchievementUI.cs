using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class BuildAchievementUI
{
    // AI-generated sprite paths
    const string PANEL_BG = "Assets/TJGenerators/History/Sprite_20260526_103331.png";
    const string GREEN_TAB = "Assets/TJGenerators/History/Sprite_20260526_103338.png";
    const string GOLD_BTN = "Assets/TJGenerators/History/Sprite_20260526_103354.png";
    const string ICON_FRAME = "Assets/TJGenerators/History/Sprite_20260526_103359.png";
    const string HEADER_BAR = "Assets/TJGenerators/History/Sprite_20260526_103404.png";
    const string ICON_TROPHY = "Assets/TJGenerators/History/Sprite_20260526_103539.png";
    const string ICON_SWORDS = "Assets/TJGenerators/History/Sprite_20260526_103540.png";
    const string ICON_TREE = "Assets/TJGenerators/History/Sprite_20260526_103541.png";
    const string ICON_FLAG = "Assets/TJGenerators/History/Sprite_20260526_103542.png";

    [MenuItem("Tools/Build Achievement UI")]
    public static void Build()
    {
        var canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("Canvas not found"); return; }

        // Remove old AchievementPanel
        var existing = canvas.transform.Find("AchievementPanel");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // Ensure sprites are imported as Sprite type
        EnsureSpriteImport(PANEL_BG);
        EnsureSpriteImport(GREEN_TAB);
        EnsureSpriteImport(GOLD_BTN);
        EnsureSpriteImport(ICON_FRAME);
        EnsureSpriteImport(HEADER_BAR);
        EnsureSpriteImport(ICON_TROPHY);
        EnsureSpriteImport(ICON_SWORDS);
        EnsureSpriteImport(ICON_TREE);
        EnsureSpriteImport(ICON_FLAG);
        AssetDatabase.Refresh();

        // Load sprites
        var sPanelBg = LoadSprite(PANEL_BG);
        var sGreenTab = LoadSprite(GREEN_TAB);
        var sGoldBtn = LoadSprite(GOLD_BTN);
        var sIconFrame = LoadSprite(ICON_FRAME);
        var sHeader = LoadSprite(HEADER_BAR);
        var sTrophy = LoadSprite(ICON_TROPHY);
        var sSwords = LoadSprite(ICON_SWORDS);
        var sTree = LoadSprite(ICON_TREE);
        var sFlag = LoadSprite(ICON_FLAG);

        var canvasRect = canvas.GetComponent<RectTransform>();
        float W = canvasRect.rect.width;
        float H = canvasRect.rect.height;

        // ===== Root: semi-transparent overlay =====
        var panelRoot = CreateUI("AchievementPanel", canvas.transform);
        SetStretch(panelRoot, W, H);
        panelRoot.AddComponent<CanvasGroup>();
        var pImg = panelRoot.AddComponent<Image>();
        pImg.color = new Color(0, 0, 0, 0.6f);
        pImg.raycastTarget = true;
        panelRoot.SetActive(false);

        // ===== Main Container: cream background with AI sprite =====
        var main = CreateUI("MainContainer", panelRoot.transform);
        SetCenter(main, 1500, 780);
        var mcImg = main.AddComponent<Image>();
        if (sPanelBg != null) { mcImg.sprite = sPanelBg; mcImg.type = Image.Type.Sliced; }
        else mcImg.color = new Color(0.96f, 0.94f, 0.88f, 1f);

        // ===== Header Bar with AI sprite =====
        var header = CreateUI("Header", main.transform);
        SetTopStretch(header, 70);
        var hImg = header.AddComponent<Image>();
        if (sHeader != null) { hImg.sprite = sHeader; hImg.type = Image.Type.Sliced; }
        else hImg.color = new Color(0.45f, 0.75f, 0.3f, 1f);

        // Trophy icon in header
        var trophy = CreateUI("Trophy", header.transform);
        SetAnchor(trophy, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(50, 50), new Vector2(-55, 0));
        var trImg = trophy.AddComponent<Image>();
        if (sTrophy != null) trImg.sprite = sTrophy;
        else { trImg.color = new Color(1f, 0.84f, 0.2f, 1f); }

        // Title text
        var title = CreateUI("Title", header.transform);
        SetCenter(title, 200, 50);
        var tText = AddText(title, "成 就", 32, Color.white, true);

        // Close button
        var closeBtnGO = CreateUI("CloseBtn", header.transform);
        SetAnchor(closeBtnGO, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(50, 50), new Vector2(-15, 0));
        closeBtnGO.AddComponent<Image>().color = new Color(1, 1, 1, 0.25f);
        var closeBtn = closeBtnGO.AddComponent<Button>();
        AddText(CreateUI("X", closeBtnGO.transform), "✕", 28, Color.white).alignment = TextAnchor.MiddleCenter;

        // ===== Progress Bar Area =====
        var progArea = CreateUI("ProgressArea", main.transform);
        SetAnchor(progArea, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(-20, 45), new Vector2(0, -80));
        progArea.AddComponent<Image>().color = new Color(0.92f, 0.9f, 0.82f, 1f);

        var tcGO = CreateUI("TotalCount", progArea.transform);
        SetAnchor(tcGO, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 30), new Vector2(15, 0));
        var tcText = AddText(tcGO, "0/28", 18, new Color(0.3f, 0.25f, 0.15f, 1f), true);

        // Progress slider
        var psGO = CreateUI("ProgressSlider", progArea.transform);
        SetCenter(psGO, 900, 16);
        var psSlider = psGO.AddComponent<Slider>();
        psGO.AddComponent<Image>().color = new Color(0.8f, 0.75f, 0.65f, 1f);
        psSlider.targetGraphic = psGO.GetComponent<Image>();
        var fillArea = CreateUI("FillArea", psGO.transform);
        SetCenter(fillArea, 0, 0);
        var fill = CreateUI("Fill", fillArea.transform);
        SetStretchZero(fill);
        fill.AddComponent<Image>().color = new Color(0.45f, 0.8f, 0.3f, 1f);
        psSlider.fillRect = fill.GetComponent<RectTransform>();
        psSlider.handleRect = null;

        var pctGO = CreateUI("Percent", progArea.transform);
        SetAnchor(pctGO, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(60, 30), new Vector2(-15, 0));
        var pctText = AddText(pctGO, "0%", 16, new Color(0.45f, 0.75f, 0.3f, 1f));

        // ===== Left Tab Area =====
        var leftPanel = CreateUI("LeftPanel", main.transform);
        var lpRT = leftPanel.GetComponent<RectTransform>();
        lpRT.anchorMin = new Vector2(0, 0); lpRT.anchorMax = new Vector2(0, 1);
        lpRT.pivot = new Vector2(0, 0.5f);
        lpRT.sizeDelta = new Vector2(180, -140);
        lpRT.anchoredPosition = new Vector2(10, -5);
        leftPanel.AddComponent<Image>().color = new Color(0.93f, 0.91f, 0.84f, 1f);

        var vlg = leftPanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(5, 5, 10, 10);
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        string[] tabNames = { "全部", "成长", "战斗", "收集", "探索", "趣味" };
        Button[] tabBtns = new Button[6];
        for (int i = 0; i < 6; i++)
        {
            var tab = CreateUI("Tab_" + tabNames[i], leftPanel.transform);
            var tbImg = tab.AddComponent<Image>();
            // Use green tab sprite for active, plain for inactive
            if (i == 0 && sGreenTab != null) { tbImg.sprite = sGreenTab; tbImg.type = Image.Type.Sliced; }
            else tbImg.color = new Color(0.95f, 0.93f, 0.85f, 1f);
            var tbBtn = tab.AddComponent<Button>();
            var lbl = CreateUI("Label", tab.transform);
            SetStretchZero(lbl);
            var lt = AddText(lbl, tabNames[i], 20, i == 0 ? Color.white : new Color(0.3f, 0.25f, 0.15f, 1f));
            lt.alignment = TextAnchor.MiddleCenter;
            var le = tab.AddComponent<LayoutElement>();
            le.preferredHeight = 48; le.minHeight = 40;
            tabBtns[i] = tbBtn;
        }

        // ===== Right Content Area =====
        var rightPanel = CreateUI("RightPanel", main.transform);
        var rpRT = rightPanel.GetComponent<RectTransform>();
        rpRT.anchorMin = new Vector2(0, 0); rpRT.anchorMax = new Vector2(1, 1);
        rpRT.pivot = new Vector2(0.5f, 0.5f);
        rpRT.sizeDelta = new Vector2(-200, -140);
        rpRT.anchoredPosition = new Vector2(85, -5);

        var sv = CreateUI("ScrollView", rightPanel.transform);
        var svRT = sv.GetComponent<RectTransform>();
        svRT.anchorMin = new Vector2(0, 0.08f); svRT.anchorMax = Vector2.one;
        svRT.sizeDelta = Vector2.zero;
        sv.AddComponent<Image>().color = new Color(0.94f, 0.92f, 0.86f, 1f);
        sv.AddComponent<Mask>().showMaskGraphic = false;
        var sr = sv.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.vertical = true;

        var content = CreateUI("Content", sv.transform);
        var cRT = content.GetComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1);
        cRT.pivot = new Vector2(0.5f, 1); cRT.sizeDelta = new Vector2(0, 0);
        var cvlg = content.AddComponent<VerticalLayoutGroup>();
        cvlg.padding = new RectOffset(10, 10, 10, 10);
        cvlg.spacing = 8;
        cvlg.childControlWidth = true; cvlg.childControlHeight = false;
        cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = cRT; sr.viewport = svRT;

        // ===== Claim All button with gold sprite =====
        var caGO = CreateUI("ClaimAllBtn", main.transform);
        var caRT = caGO.GetComponent<RectTransform>();
        caRT.anchorMin = caRT.anchorMax = new Vector2(0, 0);
        caRT.pivot = new Vector2(0, 0);
        caRT.sizeDelta = new Vector2(160, 42);
        caRT.anchoredPosition = new Vector2(10, 10);
        var caImg = caGO.AddComponent<Image>();
        if (sGoldBtn != null) { caImg.sprite = sGoldBtn; caImg.type = Image.Type.Sliced; }
        else caImg.color = new Color(1f, 0.78f, 0.2f, 1f);
        var caBtn = caGO.AddComponent<Button>();
        var caLbl = CreateUI("Label", caGO.transform);
        SetStretchZero(caLbl);
        var caTxt = AddText(caLbl, "一键领取", 20, Color.white, true);
        caTxt.alignment = TextAnchor.MiddleCenter;

        // ===== Achievement Item Prefab =====
        var itemPrefab = CreateUI("AchievementItem", null);
        var ipImg = itemPrefab.AddComponent<Image>();
        ipImg.color = new Color(1f, 0.99f, 0.95f, 0.95f);
        itemPrefab.AddComponent<LayoutElement>().preferredHeight = 90;

        // Icon: hexagonal frame + achievement icon
        var iconBg = CreateUI("IconBg", itemPrefab.transform);
        SetAnchor(iconBg, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, 70), new Vector2(10, 0));
        var ibImg = iconBg.AddComponent<Image>();
        if (sIconFrame != null) { ibImg.sprite = sIconFrame; }
        else ibImg.color = new Color(1f, 0.84f, 0.2f, 1f);

        // Inner icon (trophy by default, will be overridden per-item)
        var iconInner = CreateUI("IconInner", iconBg.transform);
        var iiRT = iconInner.GetComponent<RectTransform>();
        iiRT.anchorMin = new Vector2(0.15f, 0.15f); iiRT.anchorMax = new Vector2(0.85f, 0.85f);
        iiRT.pivot = new Vector2(0.5f, 0.5f); iiRT.sizeDelta = Vector2.zero;
        var iiImg = iconInner.AddComponent<Image>();
        if (sTrophy != null) iiImg.sprite = sTrophy;

        // Text area
        var textArea = CreateUI("TextArea", itemPrefab.transform);
        SetAnchor(textArea, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(500, 70), new Vector2(95, 0));

        var nameGO = CreateUI("Name", textArea.transform);
        var nRT = nameGO.GetComponent<RectTransform>();
        nRT.anchorMin = new Vector2(0, 1); nRT.anchorMax = new Vector2(1, 1);
        nRT.pivot = new Vector2(0, 1); nRT.sizeDelta = new Vector2(0, 28);
        var nText = AddText(nameGO, "成就名称", 18, new Color(0.2f, 0.15f, 0.1f, 1f), true);

        var descGO = CreateUI("Desc", textArea.transform);
        var dRT = descGO.GetComponent<RectTransform>();
        dRT.anchorMin = new Vector2(0, 0.5f); dRT.anchorMax = new Vector2(1, 0.5f);
        dRT.pivot = new Vector2(0, 0.5f); dRT.sizeDelta = new Vector2(0, 20);
        var dText = AddText(descGO, "条件描述", 14, new Color(0.5f, 0.45f, 0.35f, 1f));

        // Progress bar row
        var progRow = CreateUI("ProgressRow", textArea.transform);
        var prRT = progRow.GetComponent<RectTransform>();
        prRT.anchorMin = new Vector2(0, 0); prRT.anchorMax = new Vector2(1, 0);
        prRT.pivot = new Vector2(0, 0); prRT.sizeDelta = new Vector2(0, 20);

        var isldGO = CreateUI("ItemSlider", progRow.transform);
        SetAnchor(isldGO, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(350, 12), Vector2.zero);
        var isldSlider = isldGO.AddComponent<Slider>();
        isldGO.AddComponent<Image>().color = new Color(0.82f, 0.78f, 0.7f, 1f);
        isldSlider.targetGraphic = isldGO.GetComponent<Image>();
        var ifa = CreateUI("FillArea", isldGO.transform);
        SetCenter(ifa, 0, 0);
        var ifill = CreateUI("Fill", ifa.transform);
        SetStretchZero(ifill);
        ifill.AddComponent<Image>().color = new Color(0.45f, 0.8f, 0.3f, 1f);
        isldSlider.fillRect = ifill.GetComponent<RectTransform>();
        isldSlider.handleRect = null;

        var plGO = CreateUI("ProgressLabel", progRow.transform);
        SetAnchor(plGO, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(80, 20), new Vector2(360, 0));
        var plText = AddText(plGO, "0/10", 14, new Color(0.4f, 0.35f, 0.25f, 1f));

        // Right side: reward + claim button
        var rightSide = CreateUI("RightSide", itemPrefab.transform);
        SetAnchor(rightSide, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(200, 70), new Vector2(-10, 0));

        var rlGO = CreateUI("RewardLabel", rightSide.transform);
        var rlRT = rlGO.GetComponent<RectTransform>();
        rlRT.anchorMin = new Vector2(0, 0.5f); rlRT.anchorMax = new Vector2(1, 0.5f);
        rlRT.pivot = new Vector2(0.5f, 1); rlRT.sizeDelta = new Vector2(0, 22);
        rlRT.anchoredPosition = new Vector2(0, -5);
        var rlText = AddText(rlGO, "🪙×100  💎×10", 14, new Color(0.5f, 0.4f, 0.2f, 1f));
        rlText.alignment = TextAnchor.MiddleCenter;

        // Claim button with gold sprite
        var clbGO = CreateUI("ClaimBtn", rightSide.transform);
        var clbRT = clbGO.GetComponent<RectTransform>();
        clbRT.anchorMin = new Vector2(0.5f, 0); clbRT.anchorMax = new Vector2(0.5f, 0);
        clbRT.pivot = new Vector2(0.5f, 0); clbRT.sizeDelta = new Vector2(100, 34);
        clbRT.anchoredPosition = new Vector2(0, 5);
        var clbImg = clbGO.AddComponent<Image>();
        if (sGoldBtn != null) { clbImg.sprite = sGoldBtn; clbImg.type = Image.Type.Sliced; }
        else clbImg.color = new Color(1f, 0.75f, 0.15f, 1f);
        var clbBtn = clbGO.AddComponent<Button>();
        var clblText = AddText(CreateUI("Label", clbGO.transform), "领取", 18, Color.white, true);
        clblText.alignment = TextAnchor.MiddleCenter;

        // Completed overlay
        var coGO = CreateUI("CompletedOverlay", itemPrefab.transform);
        SetStretchZero(coGO);
        coGO.AddComponent<Image>().color = new Color(0.7f, 0.65f, 0.55f, 0.35f);
        coGO.SetActive(false);

        // Add AchievementItemUI + wire
        var itemUI = itemPrefab.AddComponent<TowerDefense.UI.AchievementItemUI>();
        var soI = new SerializedObject(itemUI);
        soI.FindProperty("_iconBg").objectReferenceValue = ibImg;
        soI.FindProperty("_iconSymbol").objectReferenceValue = iiImg;
        soI.FindProperty("_nameText").objectReferenceValue = nText;
        soI.FindProperty("_descText").objectReferenceValue = dText;
        soI.FindProperty("_progressSlider").objectReferenceValue = isldSlider;
        soI.FindProperty("_progressText").objectReferenceValue = plText;
        soI.FindProperty("_rewardText").objectReferenceValue = rlText;
        soI.FindProperty("_claimButton").objectReferenceValue = clbBtn;
        soI.FindProperty("_claimButtonText").objectReferenceValue = clblText;
        soI.FindProperty("_completedOverlay").objectReferenceValue = coGO;
        soI.ApplyModifiedProperties();

        itemPrefab.transform.SetParent(panelRoot.transform, false);
        itemPrefab.SetActive(false);

        // Wire AchievementPanel
        var ap = panelRoot.AddComponent<TowerDefense.UI.AchievementPanel>();
        var so = new SerializedObject(ap);
        so.FindProperty("_panel").objectReferenceValue = panelRoot;
        so.FindProperty("_closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("_titleText").objectReferenceValue = tText;
        so.FindProperty("_totalCountText").objectReferenceValue = tcText;
        so.FindProperty("_totalProgressSlider").objectReferenceValue = psSlider;
        so.FindProperty("_totalPercentText").objectReferenceValue = pctText;
        so.FindProperty("_tabAll").objectReferenceValue = tabBtns[0];
        so.FindProperty("_tabGrowth").objectReferenceValue = tabBtns[1];
        so.FindProperty("_tabBattle").objectReferenceValue = tabBtns[2];
        so.FindProperty("_tabCollection").objectReferenceValue = tabBtns[3];
        so.FindProperty("_tabExploration").objectReferenceValue = tabBtns[4];
        so.FindProperty("_tabFun").objectReferenceValue = tabBtns[5];
        so.FindProperty("_itemContainer").objectReferenceValue = cRT;
        so.FindProperty("_achievementItemPrefab").objectReferenceValue = itemPrefab;
        so.FindProperty("_claimAllButton").objectReferenceValue = caBtn;
        so.ApplyModifiedProperties();

        // Wire 成就 button
        var achBtnT = canvas.transform.Find("ui按钮/成就");
        if (achBtnT != null)
        {
            var achBtn = achBtnT.gameObject.GetComponent<Button>();
            if (achBtn == null) achBtn = achBtnT.gameObject.AddComponent<Button>();
            achBtn.onClick.RemoveAllListeners();
            achBtn.onClick.AddListener(() => ap.OpenPanel());
            Debug.Log("[BuildAchievementUI] 成就 button wired!");
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[BuildAchievementUI] Done! Achievement UI rebuilt with AI-generated sprites.");
    }

    static void EnsureSpriteImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }
    }

    static Sprite LoadSprite(string path)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static GameObject CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void SetStretch(GameObject go, float w, float h)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
    }

    static void SetCenter(GameObject go, float w, float h)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);
    }

    static void SetTopStretch(GameObject go, float h)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1);
        r.pivot = new Vector2(0.5f, 1);
        r.sizeDelta = new Vector2(0, h);
        r.anchoredPosition = Vector2.zero;
    }

    static void SetAnchor(GameObject go, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = min; r.anchorMax = max; r.pivot = pivot;
        r.sizeDelta = size; r.anchoredPosition = pos;
    }

    static void SetStretchZero(GameObject go)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = Vector2.zero; r.anchoredPosition = Vector2.zero;
    }

    static Text AddText(GameObject go, string text, int size, Color color, bool bold = false)
    {
        var t = go.AddComponent<Text>();
        t.text = text; t.fontSize = size; t.color = color;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (bold) t.fontStyle = FontStyle.Bold;
        var r = go.GetComponent<RectTransform>();
        if (r.anchorMin == new Vector2(0.5f, 0.5f) && r.anchorMax == new Vector2(0.5f, 0.5f))
        { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.sizeDelta = Vector2.zero; }
        return t;
    }
}
