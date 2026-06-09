using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TowerDefense.Data;
using TowerDefense.Map;

/// <summary>
/// 在编辑模式下将指定关卡的 ASCII 地图构建到场景的 Tilemap 中，
/// 方便可视化编辑第二关地图，完成后可清除或切换关卡。
/// 菜单：Tools → 关卡地图编辑器
/// </summary>
public static class LevelMapEditor
{
    private const string LEVELS_PATH = "Assets/Resources/Data/Levels";

    [MenuItem("Tools/关卡地图编辑器")]
    public static void ShowWindow()
    {
        var w = EditorWindow.GetWindow<LevelMapEditorWindow>("关卡地图编辑器");
        w.minSize = new Vector2(300, 200);
    }

    /// <summary>在编辑模式下构建指定关卡到场景</summary>
    public static void BuildLevelInEditor(int levelIndex)
    {
        var levelData = LoadLevelData(levelIndex);
        if (levelData == null) return;

        var builder = FindOrCreateLevelBuilder();
        if (builder == null) return;

        // 标记场景为脏，以便保存
        Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Build Level Map");
        builder.BuildLevel(levelData);
        EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);

        Debug.Log($"[关卡地图编辑器] 已在编辑模式下构建关卡 {levelIndex}: {levelData.levelName}");
        SceneView.FrameLastActiveSceneView();
    }

    /// <summary>清除场景中的地图 Tile</summary>
    public static void ClearLevelMap()
    {
        var builder = Object.FindObjectOfType<LevelBuilder>();
        if (builder == null)
        {
            Debug.LogWarning("[关卡地图编辑器] 场景中未找到 LevelBuilder");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(builder.gameObject, "Clear Level Map");
        builder.ClearAll();
        EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);

        Debug.Log("[关卡地图编辑器] 地图已清除");
    }

    private static LevelData LoadLevelData(int index)
    {
        var guids = AssetDatabase.FindAssets("t:LevelData", new[] { LEVELS_PATH });
        if (index < 0 || index >= guids.Length)
        {
            Debug.LogError($"[关卡地图编辑器] 关卡索引 {index} 超出范围 (共 {guids.Length} 个关卡)");
            return null;
        }

        string path = AssetDatabase.GUIDToAssetPath(guids[index]);
        var levelData = AssetDatabase.LoadAssetAtPath<LevelData>(path);
        if (levelData == null)
        {
            Debug.LogError($"[关卡地图编辑器] 加载关卡失败: {path}");
        }
        return levelData;
    }

    private static LevelBuilder FindOrCreateLevelBuilder()
    {
        var builder = Object.FindObjectOfType<LevelBuilder>();
        if (builder != null) return builder;

        // 尝试在 Grid 下创建
        var grid = Object.FindObjectOfType<Grid>();
        GameObject builderObj;
        if (grid != null)
        {
            builderObj = new GameObject("LevelBuilder");
            builderObj.transform.SetParent(grid.transform, false);
        }
        else
        {
            builderObj = new GameObject("LevelBuilder");
        }
        builder = builderObj.AddComponent<LevelBuilder>();

        Debug.LogWarning("[关卡地图编辑器] 场景中没有 LevelBuilder，已自动创建。请手动拖入 Tilemap 和 Tile 引用。");
        return builder;
    }
}

public class LevelMapEditorWindow : EditorWindow
{
    private int _selectedLevel = 1; // 默认第二关

    void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("关卡地图编辑器", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "在编辑模式下将关卡 ASCII 地图构建到场景 Tilemap 中，\n" +
            "方便可视化查看和调试地图布局。\n\n" +
            "修改地图请编辑 LevelData 资产的 mapRows 字段。",
            MessageType.Info);

        EditorGUILayout.Space(10);

        // 关卡选择
        var guids = AssetDatabase.FindAssets("t:LevelData", new[] { "Assets/Resources/Data/Levels" });
        string[] levelNames = new string[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var ld = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            levelNames[i] = ld != null ? $"[{i}] {ld.levelName}" : $"[{i}] {System.IO.Path.GetFileNameWithoutExtension(path)}";
        }

        if (levelNames.Length == 0)
        {
            EditorGUILayout.HelpBox("未找到任何 LevelData 资产！", MessageType.Warning);
            return;
        }

        _selectedLevel = EditorGUILayout.Popup("选择关卡", _selectedLevel, levelNames);

        EditorGUILayout.Space(15);

        // 构建按钮
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button($"构建关卡地图: {levelNames[_selectedLevel]}", GUILayout.Height(35)))
        {
            LevelMapEditor.BuildLevelInEditor(_selectedLevel);
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // 清除按钮
        GUI.backgroundColor = new Color(0.9f, 0.4f, 0.3f);
        if (GUILayout.Button("清除地图", GUILayout.Height(30)))
        {
            LevelMapEditor.ClearLevelMap();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);

        // 快捷提示
        EditorGUILayout.LabelField("快捷操作:", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("  • 修改地图 → 编辑 LevelData 资产的 mapRows", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("  • 地图符号: G=草地 P=路径 B=建塔位 D=装饰 S=起点 E=终点", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("  • 改完后点「构建关卡地图」重新可视化", EditorStyles.miniLabel);
    }
}
