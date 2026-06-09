using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class HUDBuilder
{
    [MenuItem("Tools/Build Fantasy HUD")]
    public static void Build()
    {
        var canvas = GameObject.Find("界面画布");
        if (canvas == null)
        {
            canvas = new GameObject("界面画布");
            canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            var sc = canvas.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;
            canvas.AddComponent<GraphicRaycaster>();
        }

        // 配色 - 中世纪奇幻暖色系
        var COL_BG       = new Color(0.16f, 0.12f, 0.08f, 0.92f);
        var COL_GOLD     = new Color(0.83f, 0.65f, 0.2f, 1f);
        var COL_GOLD_DK = new Color(0.6f, 0.45f, 0.1f, 1f);
        var COL_CREAM    = new Color(1f, 0.95f, 0.8f, 1f);
        var COL_BLUE     = new Color(0.3f, 0.55f, 0.9f, 1f);
        var COL_GREEN    = new Color(0.2f, 0.75f, 0.3f, 1f);
        var COL_BROWN    = new Color(0.55f, 0.35f, 0.15f, 1f);
        var COL_GRAY     = new Color(0.5f, 0.5f, 0.5f, 1f);
        var COL_RED      = new Color(0.85f, 0.2f, 0.15f, 1f);
        var COL_STAMINA  = new Color(0.2f, 0.8f, 0.4f, 1f);
        var COL_PANEL    = new Color(0.12f, 0.08f, 0.05f, 0.95f);
        var COL_OVERLAY  = new Color(0f, 0f, 0f, 0.7f);
        var COL_WOOD     = new Color(0.6f, 0.4f, 0.2f, 1f);
        var COL_STONE    = new Color(0.55f, 0.55f, 0.55f, 1f);
        var COL_FOOD     = new Color(0.8f, 0.5f, 0.2f, 1f);
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ═══════════════════════════════════════
        // 顶部资源栏
        // ═══════════════════════════════════════
        var topBar = CreateObj("顶部资源栏", canvas.transform);
        SetAnchors(topBar, new Vector2(0,1), new Vector2(1,1), new Vector2(0,-70), new Vector2(0,-5));
        topBar.AddComponent<Image>().color = COL_BG;
        var topBdr = CreateObj("边框", topBar.transform);
        SetAnchors(topBdr, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0,-2));
        topBdr.AddComponent<Image>().color = COL_GOLD_DK;

        // 左侧：等级
        var lvlGrp = CreateObj("等级组", topBar.transform);
        SetLeftAnchor(lvlGrp, 15, 0, 100, 55);
        var lvlBg = CreateObj("背景", lvlGrp.transform);
        SetStretchFill(lvlBg.GetComponent<RectTransform>());
        lvlBg.AddComponent<Image>().color = COL_GOLD_DK;
        MakeText("等级文字", lvlGrp.transform, "Lv.1", 24, COL_GOLD, FontStyle.Bold, font);

        // 资源显示组
        MakeStat("金币组", topBar.transform, 130, COL_GOLD, "1,000", COL_GOLD, 200, font);
        MakeStat("钻石组", topBar.transform, 345, COL_BLUE, "100", COL_BLUE, 170, font);
        MakeStat("木材组", topBar.transform, 530, COL_WOOD, "0", COL_CREAM, 160, font);
        MakeStat("石头组", topBar.transform, 705, COL_STONE, "0", COL_CREAM, 160, font);
        MakeStat("食物组", topBar.transform, 880, COL_FOOD, "0", COL_CREAM, 160, font);
        MakeStat("体力组", topBar.transform, 1050, COL_STAMINA, "60", COL_STAMINA, 150, font);

        // 右侧按钮
        var ctrlGrp = CreateObj("功能按钮组", topBar.transform);
        var cR = ctrlGrp.GetComponent<RectTransform>();
        cR.anchorMin = cR.anchorMax = new Vector2(1,1); cR.pivot = new Vector2(1,1);
        cR.anchoredPosition = new Vector2(-10,-5); cR.sizeDelta = new Vector2(340,55);
        var cHlg = ctrlGrp.AddComponent<HorizontalLayoutGroup>();
        cHlg.spacing = 10; cHlg.childControlWidth = false; cHlg.childControlHeight = false;
        cHlg.childForceExpandWidth = false; cHlg.childForceExpandHeight = false; cHlg.childAlignment = TextAnchor.MiddleRight;
        MakeBtn("设置按钮", ctrlGrp.transform, "设置", COL_GOLD_DK, 70, 50, font);
        MakeBtn("背包按钮", ctrlGrp.transform, "背包", COL_GOLD_DK, 70, 50, font);
        MakeBtn("商店按钮", ctrlGrp.transform, "商店", COL_GOLD_DK, 70, 50, font);
        MakeBtn("好友按钮", ctrlGrp.transform, "好友", COL_GOLD_DK, 70, 50, font);

        // ═══════════════════════════════════════
        // 左侧快捷操作栏
        // ═══════════════════════════════════════
        var sideBar = CreateObj("左侧快捷栏", canvas.transform);
        var sR = sideBar.GetComponent<RectTransform>();
        sR.anchorMin = new Vector2(0, 0.3f); sR.anchorMax = new Vector2(0, 0.7f);
        sR.offsetMin = new Vector2(10, 0); sR.offsetMax = new Vector2(70, 0);
        sideBar.AddComponent<Image>().color = new Color(0,0,0,0);
        var sVlg = sideBar.AddComponent<VerticalLayoutGroup>();
        sVlg.spacing = 12; sVlg.childControlWidth = false; sVlg.childControlHeight = false;
        sVlg.childForceExpandWidth = false; sVlg.childForceExpandHeight = false;
        sVlg.childAlignment = TextAnchor.MiddleCenter; sVlg.padding = new RectOffset(0,0,5,5);
        MakeSideBtn("一键收集", sideBar.transform, "收集", COL_GREEN, 55, 55, font);
        MakeSideBtn("建造按钮", sideBar.transform, "建造", COL_GOLD, 55, 55, font);
        MakeSideBtn("任务按钮", sideBar.transform, "任务", COL_BLUE, 55, 55, font);

        // ═══════════════════════════════════════
        // 建造菜单面板
        // ═══════════════════════════════════════
        var buildPanel = CreateObj("建造菜单面板", canvas.transform);
        var bpR = buildPanel.GetComponent<RectTransform>();
        bpR.anchorMin = new Vector2(0,0); bpR.anchorMax = new Vector2(1,0); bpR.pivot = new Vector2(0.5f,0);
        bpR.offsetMin = new Vector2(0,0); bpR.offsetMax = new Vector2(0,220);
        buildPanel.AddComponent<Image>().color = COL_BG;
        var bpBdr = CreateObj("边框", buildPanel.transform);
        SetAnchors(bpBdr, new Vector2(0,1), new Vector2(1,1), new Vector2(0,-3), new Vector2(0,0));
        bpBdr.AddComponent<Image>().color = COL_GOLD_DK;

        // 标题行 + 关闭按钮
        var titleRow = CreateObj("标题行", buildPanel.transform);
        var trR = titleRow.GetComponent<RectTransform>();
        trR.anchorMin = new Vector2(0,1); trR.anchorMax = new Vector2(1,1); trR.pivot = new Vector2(0.5f,1);
        trR.sizeDelta = new Vector2(0,40); trR.anchoredPosition = new Vector2(0,-5);
        var tt = MakeText("标题", titleRow.transform, "建  造", 24, COL_GOLD, FontStyle.Bold, font);
        SetStretchFill(tt.GetComponent<RectTransform>());
        var closeBtn = CreateObj("关闭按钮", titleRow.transform);
        var cbR = closeBtn.GetComponent<RectTransform>();
        cbR.anchorMin = cbR.anchorMax = new Vector2(1,0.5f); cbR.pivot = new Vector2(1,0.5f);
        cbR.sizeDelta = new Vector2(35,35); cbR.anchoredPosition = new Vector2(-10,0);
        closeBtn.AddComponent<Image>().color = COL_RED;
        closeBtn.AddComponent<Button>();

        // 分类标签行
        var tabRow = CreateObj("分类标签", buildPanel.transform);
        var tabR = tabRow.GetComponent<RectTransform>();
        tabR.anchorMin = new Vector2(0,1); tabR.anchorMax = new Vector2(1,1);
        tabR.offsetMin = new Vector2(20,-80); tabR.offsetMax = new Vector2(-20,-45);
        var tabHlg = tabRow.AddComponent<HorizontalLayoutGroup>();
        tabHlg.spacing = 8; tabHlg.childControlWidth = false; tabHlg.childControlHeight = false;
        tabHlg.childForceExpandWidth = false; tabHlg.childForceExpandHeight = false; tabHlg.childAlignment = TextAnchor.MiddleLeft;
        MakeTabBtn("资源类", tabRow.transform, "资源", COL_GOLD, 90, 30, font);
        MakeTabBtn("传送类", tabRow.transform, "传送", COL_BLUE, 90, 30, font);
        MakeTabBtn("仓库类", tabRow.transform, "仓库", COL_BROWN, 90, 30, font);
        MakeTabBtn("装饰类", tabRow.transform, "装饰", COL_GREEN, 90, 30, font);
        MakeTabBtn("特殊类", tabRow.transform, "特殊", COL_GRAY, 90, 30, font);

        // 建筑卡片容器
        var cardContainer = CreateObj("建筑卡片容器", buildPanel.transform);
        var ccR = cardContainer.GetComponent<RectTransform>();
        ccR.anchorMin = Vector2.zero; ccR.anchorMax = new Vector2(1,1);
        ccR.offsetMin = new Vector2(20,10); ccR.offsetMax = new Vector2(-20,-85);
        var ccHlg = cardContainer.AddComponent<HorizontalLayoutGroup>();
        ccHlg.spacing = 15; ccHlg.childControlWidth = false; ccHlg.childControlHeight = false;
        ccHlg.childForceExpandWidth = false; ccHlg.childForceExpandHeight = false; ccHlg.childAlignment = TextAnchor.MiddleCenter;

        // 示例建筑卡片
        string[] bNames = {"金矿场","伐木小屋","采石场","粮仓"};
        Color[] bAccents = {COL_GOLD, COL_WOOD, COL_STONE, COL_FOOD};
        string[] bCosts = {"200金","150金","180金","250金"};
        for(int i=0;i<4;i++) MakeBuildingCard("建筑_"+bNames[i], cardContainer.transform, bNames[i], bCosts[i], bAccents[i], font);

        // 默认隐藏
        buildPanel.SetActive(false);

        // ═══════════════════════════════════════
        // 建造模式提示
        // ═══════════════════════════════════════
        var buildHint = CreateObj("建造模式提示", canvas.transform);
        var bhR = buildHint.GetComponent<RectTransform>();
        bhR.anchorMin = bhR.anchorMax = new Vector2(0.5f,1); bhR.pivot = new Vector2(0.5f,1);
        bhR.anchoredPosition = new Vector2(0,-85); bhR.sizeDelta = new Vector2(500,45);
        buildHint.AddComponent<Image>().color = new Color(0.1f,0.4f,0.2f,0.9f);
        var bhBdr = CreateObj("边框", buildHint.transform);
        SetAnchors(bhBdr, Vector2.zero, Vector2.one, new Vector2(0,-2), new Vector2(0,0));
        bhBdr.AddComponent<Image>().color = COL_GREEN;
        var bhTxt = MakeText("提示文字", buildHint.transform, "左键放置 | 右键取消 | R旋转", 20, Color.white, FontStyle.Bold, font);
        SetStretchFill(bhTxt.GetComponent<RectTransform>());
        buildHint.SetActive(false);

        // ═══════════════════════════════════════
        // 任务面板
        // ═══════════════════════════════════════
        var questPanel = CreateObj("任务面板", canvas.transform);
        var qpR = questPanel.GetComponent<RectTransform>();
        qpR.anchorMin = qpR.anchorMax = new Vector2(1,0.5f); qpR.pivot = new Vector2(1,0.5f);
        qpR.anchoredPosition = new Vector2(-15,0); qpR.sizeDelta = new Vector2(300,400);
        questPanel.AddComponent<Image>().color = COL_BG;
        var qpBdr = CreateObj("边框", questPanel.transform);
        SetAnchors(qpBdr, Vector2.zero, Vector2.one, new Vector2(2,2), new Vector2(-2,-2));
        qpBdr.AddComponent<Image>().color = COL_GOLD_DK;
        // 标题
        var qpTitle = MakeText("标题", questPanel.transform, "每日任务", 24, COL_GOLD, FontStyle.Bold, font);
        var qptR = qpTitle.GetComponent<RectTransform>();
        qptR.anchorMin = new Vector2(0,1); qptR.anchorMax = new Vector2(1,1); qptR.pivot = new Vector2(0.5f,1);
        qptR.anchoredPosition = new Vector2(0,-10); qptR.sizeDelta = new Vector2(260,35);
        // 任务列表
        var questList = CreateObj("任务列表", questPanel.transform);
        var qlR = questList.GetComponent<RectTransform>();
        qlR.anchorMin = Vector2.zero; qlR.anchorMax = new Vector2(1,1);
        qlR.offsetMin = new Vector2(15,15); qlR.offsetMax = new Vector2(-15,-55);
        var qlVlg = questList.AddComponent<VerticalLayoutGroup>();
        qlVlg.spacing = 8; qlVlg.childControlWidth = true; qlVlg.childForceExpandHeight = false;
        qlVlg.childAlignment = TextAnchor.UpperCenter; qlVlg.padding = new RectOffset(5,5,5,5);
        // 示例任务
        MakeQuestItem("任务1", questList.transform, "收集 500 金币", "200/500", COL_GOLD, font);
        MakeQuestItem("任务2", questList.transform, "建造 3 座建筑", "1/3", COL_CREAM, font);
        MakeQuestItem("任务3", questList.transform, "砍伐 10 棵树", "5/10", COL_WOOD, font);
        questPanel.SetActive(false);

        // ═══════════════════════════════════════
        // 设置面板
        // ═══════════════════════════════════════
        var settingsPanel = CreateObj("设置面板", canvas.transform);
        SetAnchors(settingsPanel, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        settingsPanel.AddComponent<Image>().color = COL_OVERLAY;
        var spContent = CreateObj("内容", settingsPanel.transform);
        var spcR = spContent.GetComponent<RectTransform>();
        spcR.anchorMin = spcR.anchorMax = new Vector2(0.5f,0.5f); spcR.pivot = new Vector2(0.5f,0.5f);
        spcR.sizeDelta = new Vector2(400,350);
        spContent.AddComponent<Image>().color = COL_PANEL;
        var spBdr = CreateObj("边框", spContent.transform);
        SetAnchors(spBdr, Vector2.zero, Vector2.one, new Vector2(3,3), new Vector2(-3,-3));
        spBdr.AddComponent<Image>().color = COL_GOLD;
        var spVlg = spContent.AddComponent<VerticalLayoutGroup>();
        spVlg.spacing = 18; spVlg.padding = new RectOffset(30,30,35,25); spVlg.childAlignment = TextAnchor.MiddleCenter;
        spVlg.childControlWidth = true; spVlg.childForceExpandHeight = false;
        MakeText("标题", spContent.transform, "游 戏 设 置", 32, COL_GOLD, FontStyle.Bold, font).AddComponent<LayoutElement>().preferredHeight=45;
        MakeText("音量标签", spContent.transform, "音量", 20, COL_CREAM, FontStyle.Normal, font).AddComponent<LayoutElement>().preferredHeight=25;
        var volSlider = CreateObj("音量滑块", spContent.transform);
        volSlider.AddComponent<LayoutElement>().preferredHeight=30;
        volSlider.AddComponent<Slider>().value=0.8f;
        MakeMenuBtn("继续游戏", spContent.transform, "继续游戏", COL_GREEN, 0, 50, font);
        MakeMenuBtn("重新开始", spContent.transform, "重新开始", COL_RED, 0, 50, font);
        MakeMenuBtn("退出游戏", spContent.transform, "退出游戏", COL_GOLD_DK, 0, 50, font);
        settingsPanel.SetActive(false);

        // ═══════════════════════════════════════
        // 底部信息栏
        // ═══════════════════════════════════════
        var bottomBar = CreateObj("底部信息栏", canvas.transform);
        SetAnchors(bottomBar, new Vector2(0,0), new Vector2(1,0), new Vector2(0,0), new Vector2(0,5));
        bottomBar.AddComponent<Image>().color = new Color(0,0,0,0);
        var infoText = MakeText("信息文字", bottomBar.transform, "点击建筑查看详情 | 拖拽移动视角", 18, new Color(0.8f,0.8f,0.8f,0.6f), FontStyle.Normal, font);
        var iR = infoText.GetComponent<RectTransform>();
        iR.anchorMin = iR.anchorMax = new Vector2(0.5f,0); iR.pivot = new Vector2(0.5f,0);
        iR.anchoredPosition = new Vector2(0,5); iR.sizeDelta = new Vector2(600,25);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[HUD] 家园建设界面构建完成!");
    }

    // ═══════════════════════════════════════
    // 工具方法
    // ═══════════════════════════════════════

    static GameObject CreateObj(string name, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();
        return obj;
    }

    static void SetAnchors(GameObject obj, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = oMin; r.offsetMax = oMax;
    }

    static void SetLeftAnchor(GameObject obj, float x, float y, float w, float h)
    {
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,0.5f);
        r.anchoredPosition = new Vector2(x, -y - h/2); r.sizeDelta = new Vector2(w, h);
    }

    static void SetStretchFill(RectTransform r)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    static void MakeStat(string name, Transform parent, float x, Color iconColor, string value, Color valColor, float w, Font font)
    {
        var g = CreateObj(name, parent);
        var r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0,1); r.pivot = new Vector2(0,0.5f);
        r.anchoredPosition = new Vector2(x,-37); r.sizeDelta = new Vector2(w,50);
        var hlg = g.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6; hlg.childControlWidth = false; hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(4,4,4,4);
        var ico = CreateObj("图标", g.transform);
        ico.GetComponent<RectTransform>().sizeDelta = new Vector2(35,35);
        ico.AddComponent<Image>().color = iconColor;
        var ile = ico.AddComponent<LayoutElement>(); ile.preferredWidth = 35; ile.preferredHeight = 35;
        var txt = MakeText("数值", g.transform, value, 22, valColor, FontStyle.Bold, font);
        txt.AddComponent<LayoutElement>().preferredWidth = w - 50;
    }

    static GameObject MakeText(string name, Transform parent, string text, int size, Color color, FontStyle style, Font font)
    {
        var obj = CreateObj(name, parent);
        var t = obj.AddComponent<Text>();
        t.text = text; t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = TextAnchor.MiddleCenter; t.font = font;
        t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
        var ol = obj.AddComponent<Outline>();
        ol.effectColor = new Color(0,0,0,0.7f); ol.effectDistance = new Vector2(2,-2);
        return obj;
    }

    static void MakeBtn(string name, Transform parent, string label, Color bg, float w, float h, Font font)
    {
        var obj = CreateObj(name, parent);
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        obj.AddComponent<Image>().color = bg;
        var btn = obj.AddComponent<Button>();
        var c = btn.colors; c.highlightedColor = new Color(0.83f,0.65f,0.2f); btn.colors = c;
        var le = obj.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
        var t = MakeText("文字", obj.transform, label, 16, Color.white, FontStyle.Bold, font);
        SetStretchFill(t.GetComponent<RectTransform>());
    }

    static void MakeSideBtn(string name, Transform parent, string label, Color bg, float w, float h, Font font)
    {
        var obj = CreateObj(name, parent);
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        obj.AddComponent<Image>().color = bg;
        var btn = obj.AddComponent<Button>();
        var c = btn.colors; c.highlightedColor = new Color(bg.r*1.3f, bg.g*1.3f, bg.b*1.3f);
        c.pressedColor = new Color(bg.r*0.6f, bg.g*0.6f, bg.b*0.6f); btn.colors = c;
        var le = obj.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
        var t = MakeText("文字", obj.transform, label, 14, Color.white, FontStyle.Bold, font);
        SetStretchFill(t.GetComponent<RectTransform>());
    }

    static void MakeTabBtn(string name, Transform parent, string label, Color accent, float w, float h, Font font)
    {
        var obj = CreateObj(name, parent);
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(w, h);
        obj.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.3f);
        var bdr = CreateObj("底条", obj.transform);
        var bR = bdr.GetComponent<RectTransform>();
        bR.anchorMin = new Vector2(0,0); bR.anchorMax = new Vector2(1,0);
        bR.offsetMin = Vector2.zero; bR.offsetMax = new Vector2(0,3);
        bdr.AddComponent<Image>().color = accent;
        var btn = obj.AddComponent<Button>();
        var le = obj.AddComponent<LayoutElement>(); le.preferredWidth = w; le.preferredHeight = h;
        var t = MakeText("文字", obj.transform, label, 16, accent, FontStyle.Bold, font);
        SetStretchFill(t.GetComponent<RectTransform>());
    }

    static void MakeBuildingCard(string name, Transform parent, string bName, string cost, Color accent, Font font)
    {
        var obj = CreateObj(name, parent);
        obj.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 120);
        var le = obj.AddComponent<LayoutElement>(); le.preferredWidth = 160; le.preferredHeight = 120;
        obj.AddComponent<Image>().color = new Color(0.15f, 0.1f, 0.06f, 0.95f);
        var bdr = CreateObj("边框", obj.transform);
        SetAnchors(bdr, Vector2.zero, Vector2.one, new Vector2(2,2), new Vector2(-2,-2));
        bdr.AddComponent<Image>().color = accent;
        var topStrip = CreateObj("顶条", obj.transform);
        var tsR = topStrip.GetComponent<RectTransform>();
        tsR.anchorMin = new Vector2(0,1); tsR.anchorMax = new Vector2(1,1); tsR.pivot = new Vector2(0.5f,1);
        tsR.sizeDelta = new Vector2(0,4); topStrip.AddComponent<Image>().color = accent;
        // 建筑预览占位
        var preview = CreateObj("预览图", obj.transform);
        var pR = preview.GetComponent<RectTransform>();
        pR.anchorMin = new Vector2(0.2f,0.35f); pR.anchorMax = new Vector2(0.8f,0.9f);
        pR.offsetMin = Vector2.zero; pR.offsetMax = Vector2.zero;
        preview.AddComponent<Image>().color = new Color(accent.r*0.5f, accent.g*0.5f, accent.b*0.5f, 0.5f);
        // 名称
        var nt = MakeText("名称", obj.transform, bName, 16, new Color(1f,0.95f,0.8f), FontStyle.Bold, font);
        var nR = nt.GetComponent<RectTransform>();
        nR.anchorMin = new Vector2(0,0.15f); nR.anchorMax = new Vector2(1,0.4f);
        nR.offsetMin = new Vector2(3,0); nR.offsetMax = new Vector2(-3,0);
        // 费用
        var ct = MakeText("费用", obj.transform, cost, 15, new Color(0.83f,0.65f,0.2f), FontStyle.Bold, font);
        var cR = ct.GetComponent<RectTransform>();
        cR.anchorMin = Vector2.zero; cR.anchorMax = new Vector2(1,0.18f);
        cR.offsetMin = new Vector2(3,2); cR.offsetMax = new Vector2(-3,0);
        var cardBtn = obj.AddComponent<Button>();
        var bc = cardBtn.colors;
        bc.highlightedColor = new Color(accent.r*1.3f, accent.g*1.3f, accent.b*1.3f);
        bc.pressedColor = new Color(0.1f,0.08f,0.05f); cardBtn.colors = bc;
    }

    static void MakeQuestItem(string name, Transform parent, string desc, string progress, Color accent, Font font)
    {
        var obj = CreateObj(name, parent);
        obj.AddComponent<Image>().color = new Color(0.1f,0.07f,0.04f,0.9f);
        var le = obj.AddComponent<LayoutElement>(); le.preferredHeight = 65;
        var vlg = obj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 2; vlg.padding = new RectOffset(10,10,8,5);
        vlg.childControlWidth = true; vlg.childForceExpandHeight = false; vlg.childAlignment = TextAnchor.MiddleLeft;
        MakeText("描述", obj.transform, desc, 16, COL_CREAM_Default(), FontStyle.Normal, font);
        // 进度条
        var bar = CreateObj("进度条", obj.transform);
        bar.AddComponent<LayoutElement>().preferredHeight = 12;
        bar.AddComponent<Image>().color = new Color(0.2f,0.15f,0.1f);
        var fill = CreateObj("填充", bar.transform);
        var fR = fill.GetComponent<RectTransform>();
        fR.anchorMin = Vector2.zero; fR.anchorMax = new Vector2(0.4f,1);
        fR.offsetMin = Vector2.zero; fR.offsetMax = Vector2.zero;
        fill.AddComponent<Image>().color = accent;
        MakeText("进度文字", obj.transform, progress, 13, accent, FontStyle.Normal, font);
    }

    static Color COL_CREAM_Default() => new Color(1f, 0.95f, 0.8f, 1f);

    static void MakeMenuBtn(string name, Transform parent, string label, Color color, float w, float h, Font font)
    {
        var obj = CreateObj(name, parent);
        obj.AddComponent<Image>().color = color;
        var btn = obj.AddComponent<Button>();
        var c = btn.colors;
        c.highlightedColor = new Color(color.r*1.3f, color.g*1.3f, color.b*1.3f);
        c.pressedColor = new Color(color.r*0.6f, color.g*0.6f, color.b*0.6f); btn.colors = c;
        var le = obj.AddComponent<LayoutElement>(); le.preferredHeight = h; if(w > 0) le.preferredWidth = w;
        var t = MakeText("文字", obj.transform, label, 20, Color.white, FontStyle.Bold, font);
        SetStretchFill(t.GetComponent<RectTransform>());
    }
}
