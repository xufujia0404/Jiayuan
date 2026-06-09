using System;
using System.Collections.Generic;
using UnityEngine;
using Sttop5.Shared.Core;
using Sttop5.Shared.Player;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 建筑存档数据。
    /// </summary>
    [Serializable]
    public class BuildingSaveEntry
    {
        public string buildingId;
        public float posX;
        public float posY;   // 旧 2D 兼容
        public float posZ;   // 3D Z 坐标
        public int rotation;  // 0/90/180/270
        public int level;
    }

    /// <summary>
    /// 家园存档数据。
    /// </summary>
    [Serializable]
    public class HomeSaveData
    {
        public int wood;
        public int stone;
        public int food;
        public List<BuildingSaveEntry> buildings = new List<BuildingSaveEntry>();
    }

    /// <summary>
    /// 建筑管理器，负责建筑的放置、升级和拆除。
    /// 使用 PlacementManager 进行碰撞检测，支持自由建造。
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        [Header("建筑配置列表")]
        [SerializeField] private BuildingData[] _availableBuildings;
        [SerializeField] private PlacementManager _placementManager;

        private List<BuildingBase> _buildings = new List<BuildingBase>();

        /// <summary>建筑被放置时触发，可用于注册点击事件等。</summary>
        public event System.Action<BuildingBase> OnBuildingPlaced;

        public BuildingData[] AvailableBuildings => _availableBuildings;
        public IReadOnlyList<BuildingBase> Buildings => _buildings;
        public PlacementManager Placement => _placementManager;

        #region 放置与检测

        /// <summary>
        /// 在指定世界坐标放置建筑。
        /// </summary>
        public BuildingBase PlaceBuilding(BuildingData data, Vector3 worldPos)
        {
            var levelData = data.GetLevelData(1);
            var profile = PlayerProfile.Instance;
            if (profile != null && levelData.buildCost > 0 && !profile.HasEnoughGold(levelData.buildCost))
            {
                Debug.LogWarning("[BuildingManager] 金币不足");
                return null;
            }
            profile?.SpendGold(levelData.buildCost, "building_place");

            GameObject go = data.prefab != null
                ? Instantiate(data.prefab, worldPos, Quaternion.identity, transform)
                : CreatePlaceholderBuilding(data, worldPos);

            BuildingBase building = go.GetComponent<BuildingBase>();
            if (building == null)
                building = go.AddComponent<BuildingBase>();

            building.Initialize(data);
            _buildings.Add(building);

            // 注册到放置管理器
            _placementManager?.RegisterBuilding(building);

            OnBuildingPlaced?.Invoke(building);

            Debug.Log($"[BuildingManager] 建筑已放置: {data.buildingName} at {worldPos}");
            return building;
        }

        /// <summary>
        /// 拆除指定建筑。
        /// </summary>
        public bool RemoveBuilding(BuildingBase building)
        {
            if (building == null || !_buildings.Contains(building))
                return false;

            _placementManager?.UnregisterBuilding(building);
            _buildings.Remove(building);
            building.Demolish();
            return true;
        }

        /// <summary>
        /// 获取指定位置附近的建筑。
        /// </summary>
        public BuildingBase GetBuildingNear(Vector3 worldPos, float radius = 1f)
        {
            float minDist = float.MaxValue;
            BuildingBase nearest = null;
            foreach (var b in _buildings)
            {
                if (b == null) continue;
                float dist = Vector3.Distance(b.transform.position, worldPos);
                if (dist < radius && dist < minDist)
                {
                    minDist = dist;
                    nearest = b;
                }
            }
            return nearest;
        }

        #endregion

        #region 存档

        public List<BuildingSaveEntry> ExportSaveData()
        {
            var result = new List<BuildingSaveEntry>();
            foreach (var building in _buildings)
            {
                if (building == null) continue;
                result.Add(new BuildingSaveEntry
                {
                    buildingId = building.Data.buildingId,
                    posX = building.transform.position.x,
                    posY = 0f,
                    posZ = building.transform.position.z,
                    rotation = building.Rotation,
                    level = building.CurrentLevel
                });
            }
            return result;
        }

        public void RestoreFromSaveData(List<BuildingSaveEntry> entries)
        {
            if (entries == null) return;

            foreach (var entry in entries)
            {
                BuildingData data = FindBuildingData(entry.buildingId);
                if (data == null)
                {
                    Debug.LogWarning($"[BuildingManager] 未找到建筑配置: {entry.buildingId}");
                    continue;
                }

                float z = entry.posZ;
                if (z == 0f && entry.posY != 0f)
                    z = entry.posY;

                Vector3 worldPos = new Vector3(entry.posX, 0f, z);
                GameObject go = data.prefab != null
                    ? Instantiate(data.prefab, worldPos, Quaternion.Euler(0f, entry.rotation, 0f), transform)
                    : CreatePlaceholderBuilding(data, worldPos);

                BuildingBase building = go.GetComponent<BuildingBase>();
                if (building == null)
                    building = go.AddComponent<BuildingBase>();

                building.RestoreFromSave(data, entry.level);
                building.SetPlacementInfo(worldPos, entry.rotation);
                building.transform.rotation = Quaternion.Euler(0f, entry.rotation, 0f);

                _placementManager?.RegisterBuilding(building);
                _buildings.Add(building);
            }

            Debug.Log($"[BuildingManager] 从存档恢复 {entries.Count} 个建筑");
        }

        #endregion

        #region 辅助

        private BuildingData FindBuildingData(string buildingId)
        {
            if (_availableBuildings == null) return null;
            foreach (var data in _availableBuildings)
            {
                if (data.buildingId == buildingId)
                    return data;
            }
            return null;
        }

        private GameObject CreatePlaceholderBuilding(BuildingData data, Vector3 worldPos)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"Building_{data.buildingName}";
            go.transform.position = worldPos + new Vector3(0f, 0.25f, 0f);
            go.transform.SetParent(transform);
            go.transform.localScale = new Vector3(data.footprintSize.x, 0.5f, data.footprintSize.y);

            var renderer = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = GetBuildingColor(data.buildingType);
            renderer.sharedMaterial = mat;

            var collider = go.GetComponent<Collider>();
            if (collider != null) DestroyImmediate(collider);

            return go;
        }

        private Color GetBuildingColor(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.ResourceGen: return new Color(0.2f, 0.8f, 0.3f);
                case BuildingType.Portal: return new Color(0.3f, 0.5f, 1f);
                case BuildingType.Storage: return new Color(0.9f, 0.7f, 0.2f);
                case BuildingType.Decoration: return new Color(0.9f, 0.4f, 0.7f);
                case BuildingType.Special: return new Color(1f, 0.5f, 0.1f);
                default: return Color.white;
            }
        }

        #endregion
    }
}
