// HomeSceneSetup.cs - 使用反射添加组件，绕过 Roslyn 限制
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using System;

public class HomeSceneSetupRunner
{
    [MenuItem("Tools/Setup HomeScene")]
    public static void Run()
    {
        // 查找 HomeManager 类型（通过反射）
        Type homeManagerType = null;
        Type buildingManagerType = null;
        Type resourceManagerType = null;
        Type homeMainUIType = null;

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var t in asm.GetTypes())
            {
                if (t.FullName == "Sttop5.Modules.HomeBase.HomeManager") homeManagerType = t;
                if (t.FullName == "Sttop5.Modules.HomeBase.BuildingManager") buildingManagerType = t;
                if (t.FullName == "Sttop5.Modules.HomeBase.ResourceManager") resourceManagerType = t;
                if (t.FullName == "Sttop5.Modules.HomeBase.HomeMainUI") homeMainUIType = t;
            }
        }

        Debug.Log($"HomeManager: {(homeManagerType != null ? "FOUND" : "NULL")}");
        Debug.Log($"BuildingManager: {(buildingManagerType != null ? "FOUND" : "NULL")}");
        Debug.Log($"ResourceManager: {(resourceManagerType != null ? "FOUND" : "NULL")}");
        Debug.Log($"HomeMainUI: {(homeMainUIType != null ? "FOUND" : "NULL")}");

        // 挂组件
        var hm = GameObject.Find("HomeManager");
        if (hm != null && homeManagerType != null && hm.GetComponent(homeManagerType) == null)
            hm.AddComponent(homeManagerType);

        var bm = GameObject.Find("BuildingManager");
        if (bm != null && buildingManagerType != null && bm.GetComponent(buildingManagerType) == null)
            bm.AddComponent(buildingManagerType);

        var rm = GameObject.Find("ResourceManager");
        if (rm != null && resourceManagerType != null && rm.GetComponent(resourceManagerType) == null)
            rm.AddComponent(resourceManagerType);

        // 配置相机
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.transform.position = new Vector3(4.5f, 3.5f, -10);
            cam.backgroundColor = new Color(0.18f, 0.32f, 0.18f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        // 草地背景
        CreateGrassBG();

        // 网格线
        // Grid removed - no longer needed

        // UI
        CreateUI(hm, bm, rm, homeMainUIType);

        // 事件系统
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("✅ HomeScene 配置完成！");
    }

    static void CreateGrassBG()
    {
        var bg = FindOrCreate("GrassBG");
        var sr = bg.GetComponent<SpriteRenderer>();
        if (sr == null) sr = bg.AddComponent<SpriteRenderer>();
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, new Color(0.3f, 0.55f, 0.2f));
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        bg.transform.position = new Vector3(4.5f, 4f, 0);
        bg.transform.localScale = new Vector3(10, 8, 1);
        sr.sortingOrder = -10;
    }

    static void CreateGridLines()
    {
        var parent = FindOrCreate("--- GRID ---");
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(parent.transform.GetChild(i).gameObject);

        for (int x = 0; x <= 10; x++)
        {
            var go = new GameObject("V_" + x);
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(x, 4, 0);
            var s = go.AddComponent<SpriteRenderer>();
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, new Color(0.2f, 0.45f, 0.15f, 0.4f));
            t.Apply();
            s.sprite = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            go.transform.localScale = new Vector3(0.03f, 8, 1);
            s.sortingOrder = -9;
        }
        for (int y = 0; y <= 8; y++)
        {
            var go = new GameObject("H_" + y);
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(4.5f, y, 0);
            var s = go.AddComponent<SpriteRenderer>();
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, new Color(0.2f, 0.45f, 0.15f, 0.4f));
            t.Apply();
            s.sprite = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            go.transform.localScale = new Vector3(10, 0.03f, 1);
            s.sortingOrder = -9;
        }
    }

    static void CreateUI(GameObject hm, GameObject bm, GameObject rm, Type homeMainUIType)
    {
        // Canvas
        var canvasGO = FindOrCreate("Canvas");
        var canvas = canvasGO.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var cs = canvasGO.GetComponent<CanvasScaler>();
        if (cs == null) cs = canvasGO.AddComponent<CanvasScaler>();
        cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        cs.referenceResolution = new Vector2(1920, 1080);
        if (canvasGO.GetComponent<GraphicRaycaster>() == null)
            canvasGO.AddComponent<GraphicRaycaster>();

        // 资源栏
        var bar = FindOrCreateChild(canvasGO, "ResourceBar");
        var barRT = bar.GetComponent<RectTransform>();
        if (barRT == null) barRT = bar.AddComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1);
        barRT.anchorMax = new Vector2(1, 1);
        barRT.pivot = new Vector2(0.5f, 1);
        barRT.sizeDelta = new Vector2(0, 50);
        barRT.anchoredPosition = Vector2.zero;
        var barImg = bar.GetComponent<Image>();
        if (barImg == null) barImg = bar.AddComponent<Image>();
        barImg.color = new Color(0.1f, 0.1f, 0.15f, 0.85f);

        // 资源文本
        float xPos = 30;
        CreateText(bar, "GoldText", "金币: 1000", ref xPos, Color.white);
        CreateText(bar, "DiamondText", "钻石: 100", ref xPos, new Color(0.4f, 0.8f, 1f));
        CreateText(bar, "WoodText", "木材: 0", ref xPos, new Color(0.8f, 0.6f, 0.3f));
        CreateText(bar, "LevelText", "Lv.1", ref xPos, new Color(1f, 0.9f, 0.3f));

        // 收集按钮
        var btnGO = FindOrCreateChild(canvasGO, "CollectAllBtn");
        var btnRT = btnGO.GetComponent<RectTransform>();
        if (btnRT == null) btnRT = btnGO.AddComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1, 0);
        btnRT.anchorMax = new Vector2(1, 0);
        btnRT.pivot = new Vector2(1, 0);
        btnRT.sizeDelta = new Vector2(160, 45);
        btnRT.anchoredPosition = new Vector2(-20, 20);
        var btnImg = btnGO.GetComponent<Image>();
        if (btnImg == null) btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.2f, 0.65f, 0.25f);
        if (btnGO.GetComponent<Button>() == null) btnGO.AddComponent<Button>();

        var btnTxt = FindOrCreateChild(btnGO, "Text");
        var btxtRT = btnTxt.GetComponent<RectTransform>();
        if (btxtRT == null) btxtRT = btnTxt.AddComponent<RectTransform>();
        btxtRT.anchorMin = Vector2.zero;
        btxtRT.anchorMax = Vector2.one;
        btxtRT.sizeDelta = Vector2.zero;
        var btxt = btnTxt.GetComponent<Text>();
        if (btxt == null) btxt = btnTxt.AddComponent<Text>();
        btxt.text = "Collect All";
        btxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btxt.fontSize = 22;
        btxt.color = Color.white;
        btxt.alignment = TextAnchor.MiddleCenter;

        // HomeMainUI (反射)
        if (homeMainUIType != null)
        {
            var ui = canvasGO.GetComponent(homeMainUIType);
            if (ui == null) ui = canvasGO.AddComponent(homeMainUIType);

            var so = new SerializedObject(ui as Component);
            SetRef(so, "_homeManager", hm);
            SetRef(so, "_buildingManager", bm);
            SetRef(so, "_resourceManager", rm);
            SetRef(so, "_goldText", FindChild(bar, "GoldText"));
            SetRef(so, "_diamondText", FindChild(bar, "DiamondText"));
            SetRef(so, "_woodText", FindChild(bar, "WoodText"));
            SetRef(so, "_levelText", FindChild(bar, "LevelText"));
            SetRef(so, "_collectAllButton", btnGO);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void CreateText(GameObject parent, string name, string content, ref float xPos, Color color)
    {
        var go = FindOrCreateChild(parent, name);
        var rt = go.GetComponent<RectTransform>();
        if (rt == null) rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 0.5f);
        rt.sizeDelta = new Vector2(200, 0);
        rt.anchoredPosition = new Vector2(xPos, 0);
        var txt = go.GetComponent<Text>();
        if (txt == null) txt = go.AddComponent<Text>();
        txt.text = content;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 20;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleLeft;
        xPos += 220;
    }

    static void SetRef(SerializedObject so, string prop, GameObject target)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = target;
    }

    static GameObject FindChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        return t != null ? t.gameObject : null;
    }

    static GameObject FindOrCreate(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        return go;
    }

    static GameObject FindOrCreateChild(GameObject parent, string name)
    {
        var t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }
}
