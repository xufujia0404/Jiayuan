using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public static class ApplyIcons
{
    [MenuItem("Tools/Apply HUD Icons")]
    public static void Apply()
    {
        var canvas = GameObject.Find("界面画布");
        if (canvas == null) { Debug.Log("未找到界面画布"); return; }

        var iconMap = new System.Collections.Generic.Dictionary<string, string>()
        {
            { "金币组/图标", "Assets/Art/UI/Icons/icon_gold.png" },
            { "钻石组/图标", "Assets/Art/UI/Icons/icon_diamond.png" },
            { "木材组/图标", "Assets/Art/UI/Icons/icon_wood.png" },
            { "石头组/图标", "Assets/Art/UI/Icons/icon_stone.png" },
            { "食物组/图标", "Assets/Art/UI/Icons/icon_food.png" },
            { "体力组/图标", "Assets/Art/UI/Icons/icon_stamina.png" }
        };

        int applied = 0;
        var topBar = canvas.transform.Find("顶部资源栏");
        if (topBar == null) { Debug.Log("未找到顶部资源栏"); return; }

        foreach (var kv in iconMap)
        {
            var child = topBar.Find(kv.Key);
            if (child == null) { Debug.Log("未找到: " + kv.Key); continue; }
            var img = child.GetComponent<Image>();
            if (img == null) { Debug.Log("无Image组件: " + kv.Key); continue; }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(kv.Value);
            if (sprite == null) { Debug.Log("加载Sprite失败: " + kv.Value); continue; }
            img.sprite = sprite;
            img.color = Color.white;
            applied++;
            Debug.Log("已应用: " + kv.Key + " -> " + kv.Value);
        }
        Debug.Log("[图标] 共应用 " + applied + "/" + iconMap.Count + " 个图标");
    }
}
