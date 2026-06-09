using UnityEngine;
using UnityEngine.UI;

public static class KRRestyleHUD
{
    public static void RestyleTopBar()
    {
        var canvas = GameObject.Find("画布");
        if (canvas == null) { Debug.LogError("Canvas not found!"); return; }
        var topBar = canvas.transform.Find("顶部标题栏");
        if (topBar == null) { Debug.LogError("Top bar not found!"); return; }

        // ---- 1. Top bar background ----
        var barRect = topBar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0, 1);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(0.5f, 1);
        barRect.anchoredPosition = new Vector2(0, 0);
        barRect.sizeDelta = new Vector2(0, 70);

        var barImg = topBar.GetComponent<Image>();
        if (barImg == null) barImg = topBar.gameObject.AddComponent<Image>();
        barImg.color = new Color32(46, 24, 13, 242);

        // Gold border
        var existingBorder = topBar.Find("GoldBorder");
        if (existingBorder != null && existingBorder.GetComponent<RectTransform>() != null)
        {
            existingBorder.GetComponent<Image>().color = new Color32(196, 150, 60, 255);
        }
        else
        {
            if (existingBorder != null) Object.DestroyImmediate(existingBorder.gameObject);
            var borderObj = new GameObject("GoldBorder");
            borderObj.transform.SetParent(topBar, false);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = new Vector2(0, 0);
            borderRect.anchorMax = new Vector2(1, 0);
            borderRect.pivot = new Vector2(0.5f, 0);
            borderRect.sizeDelta = new Vector2(0, 3);
            borderRect.anchoredPosition = Vector2.zero;
            borderObj.AddComponent<Image>().color = new Color32(196, 150, 60, 255);
        }

        // ---- 2. Gold display ----
        StyleContainerWithIcon(topBar, "金币", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(0, 0.5f), new Vector2(15, 0), new Vector2(160, 44),
            new Color32(26, 15, 5, 204), "Image", new Color32(255, 214, 0, 255),
            "GoldText", 24, new Color32(255, 214, 0, 255), TextAnchor.MiddleLeft);

        // ---- 3. Lives display ----
        StyleContainerWithIcon(topBar, "生命", new Vector2(0, 0.5f), new Vector2(0, 0.5f),
            new Vector2(0, 0.5f), new Vector2(190, 0), new Vector2(120, 44),
            new Color32(26, 15, 5, 204), "Image", new Color32(230, 51, 51, 255),
            "LifeText", 24, new Color32(255, 102, 102, 255), TextAnchor.MiddleLeft);

        // ---- 4. Wave display (has Text component, can't add Image to same GO) ----
        var waveObj = topBar.Find("WaveText");
        if (waveObj != null)
        {
            var waveRect = waveObj.GetComponent<RectTransform>();
            waveRect.anchorMin = new Vector2(0.5f, 0.5f);
            waveRect.anchorMax = new Vector2(0.5f, 0.5f);
            waveRect.pivot = new Vector2(0.5f, 0.5f);
            waveRect.sizeDelta = new Vector2(250, 44);
            waveRect.anchoredPosition = new Vector2(0, 0);

            var waveText = waveObj.GetComponent<Text>();
            if (waveText != null)
            {
                waveText.fontSize = 22;
                waveText.fontStyle = FontStyle.Bold;
                waveText.color = new Color32(217, 204, 166, 255);
                waveText.alignment = TextAnchor.MiddleCenter;
            }

            var waveIcon = waveObj.Find("Image");
            if (waveIcon != null)
            {
                waveIcon.GetComponent<Image>().color = new Color32(153, 191, 230, 255);
            }
        }

        // ---- 5. Enemy count ----
        var enemyObj = topBar.Find("EnemyCountText");
        if (enemyObj != null)
        {
            var enemyRect = enemyObj.GetComponent<RectTransform>();
            enemyRect.anchorMin = new Vector2(1, 0.5f);
            enemyRect.anchorMax = new Vector2(1, 0.5f);
            enemyRect.pivot = new Vector2(1, 0.5f);
            enemyRect.sizeDelta = new Vector2(140, 44);
            enemyRect.anchoredPosition = new Vector2(-70, 0);

            var enemyText = enemyObj.GetComponent<Text>();
            if (enemyText != null)
            {
                enemyText.fontSize = 20;
                enemyText.fontStyle = FontStyle.Bold;
                enemyText.color = new Color32(217, 140, 77, 255);
                enemyText.alignment = TextAnchor.MiddleCenter;
            }
        }

        // ---- 6. Settings button ----
        var settingsObj = topBar.Find("设置按钮.");
        if (settingsObj != null)
        {
            var settingsRect = settingsObj.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(1, 0.5f);
            settingsRect.anchorMax = new Vector2(1, 0.5f);
            settingsRect.pivot = new Vector2(1, 0.5f);
            settingsRect.sizeDelta = new Vector2(44, 44);
            settingsRect.anchoredPosition = new Vector2(-12, 0);

            var settingsImg = settingsObj.GetComponent<Image>();
            if (settingsImg != null) settingsImg.color = new Color32(140, 107, 51, 255);
        }

        Debug.Log("✅ Kingdom Rush top HUD bar styled!");
    }

    public static void RestyleTowerSelectPanel()
    {
        var canvas = GameObject.Find("画布");
        if (canvas == null) return;
        var panel = canvas.transform.Find("塔选择面板");
        if (panel == null) { Debug.Log("Tower select panel not found"); return; }

        // Panel background - dark parchment
        var panelRect = panel.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = panel.gameObject.AddComponent<RectTransform>();

        // Style title text
        var title = panel.Find("TitleText");
        if (title != null)
        {
            var txt = title.GetComponent<Text>();
            if (txt != null)
            {
                txt.fontSize = 28;
                txt.fontStyle = FontStyle.Bold;
                txt.color = new Color32(217, 204, 166, 255);
                txt.alignment = TextAnchor.MiddleCenter;
            }
        }

        // Style container
        var container = panel.Find("GameObject");
        if (container != null)
        {
            var img = container.GetComponent<Image>();
            if (img == null) img = container.gameObject.AddComponent<Image>();
            img.color = new Color32(46, 24, 13, 200);
        }

        Debug.Log("✅ Tower select panel styled!");
    }

    public static void RestyleTowerInfoPanel()
    {
        var canvas = GameObject.Find("画布");
        if (canvas == null) return;
        var panel = canvas.transform.Find("塔信息面板");
        if (panel == null) { Debug.Log("Tower info panel not found"); return; }

        // Dark medieval panel bg
        var panelImg = panel.GetComponent<Image>();
        if (panelImg == null) panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = new Color32(30, 16, 8, 245);

        // Style all text elements
        string[] textNames = { "TowerNameText", "TowerLevelText", "DamageText", "RangeText", "AttackSpeedText" };
        foreach (var name in textNames)
        {
            var child = panel.Find(name);
            if (child != null)
            {
                var txt = child.GetComponent<Text>();
                if (txt != null)
                {
                    txt.fontSize = 18;
                    txt.fontStyle = FontStyle.Bold;
                    txt.color = new Color32(217, 204, 166, 255);
                }
            }
        }

        // TowerName text bigger
        var nameTxt = panel.Find("TowerNameText");
        if (nameTxt != null)
        {
            var txt = nameTxt.GetComponent<Text>();
            if (txt != null) { txt.fontSize = 24; txt.color = new Color32(255, 214, 0, 255); }
        }

        // Upgrade button - gold/amber
        var upgradeBtn = panel.Find("UpgradeButton");
        if (upgradeBtn != null)
        {
            var btnImg = upgradeBtn.GetComponent<Image>();
            if (btnImg != null) btnImg.color = new Color32(139, 105, 20, 255);
            var costTxt = upgradeBtn.Find("UpgradeCostText");
            if (costTxt != null)
            {
                var txt = costTxt.GetComponent<Text>();
                if (txt != null) { txt.fontSize = 18; txt.fontStyle = FontStyle.Bold; txt.color = new Color32(255, 255, 200, 255); }
            }
        }

        // Sell button - red tint
        var sellBtn = panel.Find("SellButton");
        if (sellBtn != null)
        {
            var btnImg = sellBtn.GetComponent<Image>();
            if (btnImg != null) btnImg.color = new Color32(139, 40, 20, 255);
            var sellTxt = sellBtn.Find("SellValueText");
            if (sellTxt != null)
            {
                var txt = sellTxt.GetComponent<Text>();
                if (txt != null) { txt.fontSize = 16; txt.fontStyle = FontStyle.Bold; txt.color = new Color32(255, 200, 180, 255); }
            }
        }

        // Close button - dark
        var closeBtn = panel.Find("CloseButton");
        if (closeBtn != null)
        {
            var btnImg = closeBtn.GetComponent<Image>();
            if (btnImg != null) btnImg.color = new Color32(80, 50, 30, 255);
            var closeTxt = closeBtn.Find("UpgradeCostText");
            if (closeTxt != null)
            {
                var txt = closeTxt.GetComponent<Text>();
                if (txt != null) { txt.fontSize = 18; txt.fontStyle = FontStyle.Bold; txt.color = new Color32(217, 204, 166, 255); }
            }
        }

        Debug.Log("✅ Tower info panel styled!");
    }

    public static void RestyleSettingsPanel()
    {
        var canvas = GameObject.Find("画布");
        if (canvas == null) return;
        var panel = canvas.transform.Find("设置界面");
        if (panel == null) { Debug.Log("Settings panel not found"); return; }

        // Full dark overlay
        var panelImg = panel.GetComponent<Image>();
        if (panelImg == null) panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = new Color32(0, 0, 0, 200);

        // Style all buttons
        string[] btnNames = { "重新开始", "返回主页面", "退出游戏" };
        Color32[] btnColors = {
            new Color32(100, 70, 30, 255),   // Restart - warm brown
            new Color32(80, 55, 25, 255),    // Home - darker brown
            new Color32(120, 30, 20, 255)    // Exit - dark red
        };
        for (int i = 0; i < btnNames.Length; i++)
        {
            var btn = panel.Find(btnNames[i]);
            if (btn != null)
            {
                var btnImg = btn.GetComponent<Image>();
                if (btnImg != null) btnImg.color = btnColors[i];
                var btnTxt = btn.GetComponentInChildren<Text>();
                if (btnTxt != null)
                {
                    btnTxt.fontSize = 22;
                    btnTxt.fontStyle = FontStyle.Bold;
                    btnTxt.color = new Color32(217, 204, 166, 255);
                }
            }
        }

        // Style labels
        string[] labels = { "背景音乐", "音效", "音量", "语言" };
        foreach (var label in labels)
        {
            var labelObj = panel.Find(label);
            if (labelObj != null)
            {
                var labelImg = labelObj.GetComponent<Image>();
                if (labelImg == null) labelImg = labelObj.gameObject.AddComponent<Image>();
                labelImg.color = new Color32(40, 22, 10, 230);
                var labelTxt = labelObj.GetComponentInChildren<Text>();
                if (labelTxt != null)
                {
                    labelTxt.fontSize = 20;
                    labelTxt.fontStyle = FontStyle.Bold;
                    labelTxt.color = new Color32(217, 204, 166, 255);
                }
            }
        }

        // Title
        var titleObj = panel.Find("Text (Legacy)");
        if (titleObj != null)
        {
            var titleImg = titleObj.GetComponent<Image>();
            if (titleImg != null) titleImg.color = new Color32(46, 24, 13, 245);
        }

        Debug.Log("✅ Settings panel styled!");
    }

    public static void RestyleAll()
    {
        RestyleTopBar();
        RestyleTowerSelectPanel();
        RestyleTowerInfoPanel();
        RestyleSettingsPanel();
        Debug.Log("🎉 All Kingdom Rush UI styled!");
    }

    static void StyleContainerWithIcon(Transform parent, string objName,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size,
        Color32 bgColor, string iconName, Color32 iconColor,
        string textName, int fontSize, Color32 textColor, TextAnchor alignment)
    {
        var obj = parent.Find(objName);
        if (obj == null) return;

        var rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        // Only add Image bg if no Text component on same GO
        var existingText = obj.GetComponent<Text>();
        if (existingText == null)
        {
            var bg = obj.GetComponent<Image>();
            if (bg == null) bg = obj.gameObject.AddComponent<Image>();
            bg.color = bgColor;
        }

        var icon = obj.Find(iconName);
        if (icon != null)
        {
            var iconImg = icon.GetComponent<Image>();
            if (iconImg != null) iconImg.color = iconColor;
        }

        var txtObj = obj.Find(textName);
        if (txtObj != null)
        {
            var txt = txtObj.GetComponent<Text>();
            if (txt != null)
            {
                txt.fontSize = fontSize;
                txt.fontStyle = FontStyle.Bold;
                txt.color = textColor;
                txt.alignment = alignment;
            }
        }
    }
}
