using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class VictoryPanelBuilder
{
    [MenuItem("Tools/Configure Victory Panel")]
    public static void Build()
    {
        var canvasObj = GameObject.Find("画布");
        if (canvasObj == null) { Debug.LogError("Canvas not found!"); return; }

        // Destroy existing victory panel if present
        var existing = canvasObj.transform.Find("胜利界面");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var canvasT = canvasObj.transform;

        // ROOT
        var root = NewUI("胜利界面", canvasT);
        Stretch(root.GetComponent<RectTransform>());
        root.SetActive(false);

        // OVERLAY (dark backdrop)
        var ov = NewUI("遮罩", root.transform);
        Stretch(ov.GetComponent<RectTransform>());
        ov.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        // PANEL BACKGROUND
        var pn = NewUI("面板背景", root.transform);
        var pr = pn.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = pr.pivot = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(600f, 720f);
        pn.AddComponent<Image>().color = new Color(0.18f, 0.12f, 0.08f, 0.96f);

        // RIBBON (decorative top stripe)
        var rb = NewUI("装饰丝带", pn.transform);
        var rr = rb.GetComponent<RectTransform>();
        rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f);
        rr.pivot = new Vector2(0.5f, 1f); rr.sizeDelta = new Vector2(0f, 8f);
        rb.AddComponent<Image>().color = new Color(0.12f, 0.55f, 0.22f, 0.9f);

        // TITLE
        var tt = NewUI("胜利标题", pn.transform);
        var tr = tt.GetComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f); tr.sizeDelta = new Vector2(0f, 100f);
        tr.anchoredPosition = new Vector2(0f, -30f);
        var titleTxt = tt.AddComponent<Text>();
        titleTxt.text = "胜 利 !"; titleTxt.font = font; titleTxt.fontSize = 56;
        titleTxt.fontStyle = FontStyle.Bold; titleTxt.color = new Color(1f, 0.85f, 0.1f);
        titleTxt.alignment = TextAnchor.MiddleCenter;

        // DIVIDER
        var dv = NewUI("分割线", pn.transform);
        var dr = dv.GetComponent<RectTransform>();
        dr.anchorMin = new Vector2(0.1f, 1f); dr.anchorMax = new Vector2(0.9f, 1f);
        dr.pivot = new Vector2(0.5f, 1f); dr.sizeDelta = new Vector2(0f, 3f);
        dr.anchoredPosition = new Vector2(0f, -130f);
        dv.AddComponent<Image>().color = new Color(1f, 0.85f, 0.1f, 0.3f);

        // STARS AREA
        var ss = NewUI("星星区域", pn.transform);
        var sr = ss.GetComponent<RectTransform>();
        sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f);
        sr.pivot = new Vector2(0.5f, 1f); sr.sizeDelta = new Vector2(0f, 120f);
        sr.anchoredPosition = new Vector2(0f, -145f);

        float[] sx = { -80f, 0f, 80f };
        Image[] starImgs = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var s = NewUI("Star" + (i + 1), ss.transform);
            var scr = s.GetComponent<RectTransform>();
            scr.anchorMin = scr.anchorMax = scr.pivot = new Vector2(0.5f, 0.5f);
            scr.sizeDelta = new Vector2(70f, 70f);
            scr.anchoredPosition = new Vector2(sx[i], 0f);
            starImgs[i] = s.AddComponent<Image>();
            starImgs[i].color = new Color(1f, 0.85f, 0f, 1f);
        }

        // STATS AREA
        var st = NewUI("统计区域", pn.transform);
        var str2 = st.GetComponent<RectTransform>();
        str2.anchorMin = new Vector2(0f, 1f); str2.anchorMax = new Vector2(1f, 1f);
        str2.pivot = new Vector2(0.5f, 1f); str2.sizeDelta = new Vector2(0f, 200f);
        str2.anchoredPosition = new Vector2(0f, -275f);

        string[] sLabels = { "击杀数", "剩余金币", "存活波次" };
        float[] sy = { -20f, -75f, -130f };
        Text[] valTxts = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            var row = NewUI("StatRow" + i, st.transform);
            var rowr = row.GetComponent<RectTransform>();
            rowr.anchorMin = new Vector2(0.1f, 1f); rowr.anchorMax = new Vector2(0.9f, 1f);
            rowr.pivot = new Vector2(0.5f, 1f); rowr.sizeDelta = new Vector2(0f, 45f);
            rowr.anchoredPosition = new Vector2(0f, sy[i]);
            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(1f, 1f, 1f, 0.06f); rowImg.raycastTarget = false;

            var lb = NewUI("Label", row.transform);
            var lr = lb.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 0f); lr.anchorMax = new Vector2(0.45f, 1f);
            lr.offsetMin = new Vector2(20f, 0f); lr.offsetMax = Vector2.zero;
            var lt = lb.AddComponent<Text>();
            lt.text = sLabels[i]; lt.font = font; lt.fontSize = 26;
            lt.color = new Color(0.85f, 0.78f, 0.65f); lt.alignment = TextAnchor.MiddleLeft;

            var vl = NewUI("Value", row.transform);
            var vr = vl.GetComponent<RectTransform>();
            vr.anchorMin = new Vector2(0.55f, 0f); vr.anchorMax = new Vector2(1f, 1f);
            vr.offsetMin = Vector2.zero; vr.offsetMax = new Vector2(-20f, 0f);
            var vt = vl.AddComponent<Text>();
            vt.text = "0"; vt.font = font; vt.fontSize = 28;
            vt.fontStyle = FontStyle.Bold; vt.color = Color.white;
            vt.alignment = TextAnchor.MiddleRight;
            valTxts[i] = vt;
        }

        // BUTTONS AREA
        var bt = NewUI("按钮区域", pn.transform);
        var br2 = bt.GetComponent<RectTransform>();
        br2.anchorMin = new Vector2(0f, 0f); br2.anchorMax = new Vector2(1f, 0f);
        br2.pivot = new Vector2(0.5f, 0f); br2.sizeDelta = new Vector2(0f, 120f);
        br2.anchoredPosition = new Vector2(0f, 40f);

        // Restart button (left)
        var rbtn = NewUI("重新挑战按钮", bt.transform);
        var rbr = rbtn.GetComponent<RectTransform>();
        rbr.anchorMin = new Vector2(0.04f, 0.15f); rbr.anchorMax = new Vector2(0.32f, 0.85f);
        rbr.sizeDelta = Vector2.zero;
        var rbi = rbtn.AddComponent<Image>(); rbi.color = new Color(0.92f, 0.55f, 0.12f);
        var rb2 = rbtn.AddComponent<Button>(); rb2.targetGraphic = rbi;
        var rbt = NewUI("Text", rbtn.transform);
        Stretch(rbt.GetComponent<RectTransform>());
        var rbtxt = rbt.AddComponent<Text>();
        rbtxt.text = "重新挑战"; rbtxt.font = font; rbtxt.fontSize = 22;
        rbtxt.fontStyle = FontStyle.Bold; rbtxt.color = Color.white;
        rbtxt.alignment = TextAnchor.MiddleCenter;

        // Next level button (center)
        var nbtn = NewUI("下一关按钮", bt.transform);
        var nbr = nbtn.GetComponent<RectTransform>();
        nbr.anchorMin = new Vector2(0.36f, 0.15f); nbr.anchorMax = new Vector2(0.64f, 0.85f);
        nbr.sizeDelta = Vector2.zero;
        var nbi = nbtn.AddComponent<Image>(); nbi.color = new Color(0.2f, 0.72f, 0.35f);
        var nb2 = nbtn.AddComponent<Button>(); nb2.targetGraphic = nbi;
        var nbt = NewUI("Text", nbtn.transform);
        Stretch(nbt.GetComponent<RectTransform>());
        var nbtxt = nbt.AddComponent<Text>();
        nbtxt.text = "下一关"; nbtxt.font = font; nbtxt.fontSize = 22;
        nbtxt.fontStyle = FontStyle.Bold; nbtxt.color = Color.white;
        nbtxt.alignment = TextAnchor.MiddleCenter;

        // Main menu button (right)
        var mbtn = NewUI("返回主页按钮", bt.transform);
        var mbr = mbtn.GetComponent<RectTransform>();
        mbr.anchorMin = new Vector2(0.68f, 0.15f); mbr.anchorMax = new Vector2(0.96f, 0.85f);
        mbr.sizeDelta = Vector2.zero;
        var mbi = mbtn.AddComponent<Image>(); mbi.color = new Color(0.4f, 0.45f, 0.55f);
        var mb2 = mbtn.AddComponent<Button>(); mb2.targetGraphic = mbi;
        var mbt = NewUI("Text", mbtn.transform);
        Stretch(mbt.GetComponent<RectTransform>());
        var mbtxt = mbt.AddComponent<Text>();
        mbtxt.text = "返回主页"; mbtxt.font = font; mbtxt.fontSize = 22;
        mbtxt.fontStyle = FontStyle.Bold; mbtxt.color = Color.white;
        mbtxt.alignment = TextAnchor.MiddleCenter;

        // VictoryPanel component - wire references
        var vp = root.AddComponent<TowerDefense.UI.VictoryPanel>();
        var so = new SerializedObject(vp);
        so.FindProperty("_titleText").objectReferenceValue = titleTxt;
        var sp = so.FindProperty("_starImages");
        sp.arraySize = 3;
        for (int i = 0; i < 3; i++) sp.GetArrayElementAtIndex(i).objectReferenceValue = starImgs[i];
        so.FindProperty("_killCountText").objectReferenceValue = valTxts[0];
        so.FindProperty("_goldText").objectReferenceValue = valTxts[1];
        so.FindProperty("_waveText").objectReferenceValue = valTxts[2];
        so.FindProperty("_restartButton").objectReferenceValue = rb2;
        so.FindProperty("_nextLevelButton").objectReferenceValue = nb2;
        so.FindProperty("_mainMenuButton").objectReferenceValue = mb2;
        // Wire star sprites
        var starFilled = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260601_090551.png");
        var starEmpty = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/TJGenerators/History/Sprite_20260601_090554.png");
        if (starFilled != null) so.FindProperty("_starFilledSprite").objectReferenceValue = starFilled;
        if (starEmpty != null) so.FindProperty("_starEmptySprite").objectReferenceValue = starEmpty;
        so.ApplyModifiedProperties();

        // Wire GameUI._victoryPanel
        var gui = canvasObj.transform.Find("顶部标题栏/GameUI");
        if (gui != null)
        {
            var gc = gui.GetComponent<TowerDefense.UI.GameUI>();
            if (gc != null)
            {
                var gso = new SerializedObject(gc);
                gso.FindProperty("_victoryPanel").objectReferenceValue = root;
                gso.ApplyModifiedProperties();
                Debug.Log("GameUI._victoryPanel wired!");
            }
        }

        EditorSceneManager.MarkSceneDirty(canvasObj.scene);
        Debug.Log("Victory Panel built successfully!");
    }

    /// <summary>
    /// Creates a UI-ready GameObject with RectTransform on layer 5 (UI).
    /// </summary>
    static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.layer = 5;
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}
