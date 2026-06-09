using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 建筑类型枚举。
    /// </summary>
    public enum BuildingType
    {
        ResourceGen,    // 资源产出建筑
        Portal,         // 传送门（进入小游戏）
        Storage,        // 仓库（提升资源上限）
        Decoration,     // 装饰建筑
        Special         // 特殊建筑
    }

    /// <summary>
    /// 建筑等级配置。
    /// </summary>
    [Serializable]
    public class BuildingLevel
    {
        public int level;
        public int upgradeCost;
        public int buildCost;
        public float productionRate;    // 每秒产出量
        public int productionCapacity;  // 产出上限
        public int storageCapacity;     // 仓库容量（仅 Storage 类型）
    }

    /// <summary>
    /// 建筑配置数据（ScriptableObject）。
    /// 在 Inspector 中编辑，定义建筑的所有属性。
    /// </summary>
    [CreateAssetMenu(fileName = "NewBuilding", menuName = "HomeBase/Building Data")]
    public class BuildingData : ScriptableObject
    {
        [Header("基础信息")]
        public string buildingId;
        public string buildingName;
        [TextArea(2, 4)]
        public string description;
        public BuildingType buildingType;
        public Sprite icon;
        public GameObject prefab;

        [Header("占地配置")]
        public Vector2Int footprintSize = Vector2Int.one;  // 占据的占地面积 (X宽, Z深)

        [Header("等级配置")]
        public BuildingLevel[] levels;

        [Header("传送门配置（仅 Portal 类型）")]
        public string targetModuleId;    // 目标模块 ID（如 "towerdefense"）
        public string targetSceneName;   // 目标场景名

        [Header("产出资源类型（仅 ResourceGen 类型）")]
        public ResourceType producedResource = ResourceType.Gold;

        /// <summary>获取指定等级的配置，超出范围返回最高等级。</summary>
        public BuildingLevel GetLevelData(int level)
        {
            if (levels == null || levels.Length == 0)
                return new BuildingLevel { level = 1, buildCost = 100 };
            int index = Mathf.Clamp(level - 1, 0, levels.Length - 1);
            return levels[index];
        }

        /// <summary>最大等级。</summary>
        public int MaxLevel => levels != null ? levels.Length : 1;
    }
}
