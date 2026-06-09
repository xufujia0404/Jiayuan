using System;
using System.Collections.Generic;
using UnityEngine;
using Sttop5.Shared.Core;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 资源管理器，管理家园中的资源产出和收集。
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        [Header("本地资源")]
        [SerializeField] private int _wood = 0;
        [SerializeField] private int _stone = 0;
        [SerializeField] private int _food = 0;

        public int Wood => _wood;
        public int Stone => _stone;
        public int Food => _food;

        public event Action<int, int, int> OnLocalResourcesChanged;

        #region 本地资源操作

        public void AddWood(int amount)
        {
            if (amount <= 0) return;
            _wood += amount;
            OnLocalResourcesChanged?.Invoke(_wood, _stone, _food);
        }

        public void AddStone(int amount)
        {
            if (amount <= 0) return;
            _stone += amount;
            OnLocalResourcesChanged?.Invoke(_wood, _stone, _food);
        }

        public void AddFood(int amount)
        {
            if (amount <= 0) return;
            _food += amount;
            OnLocalResourcesChanged?.Invoke(_wood, _stone, _food);
        }

        public bool SpendWood(int amount)
        {
            if (_wood < amount) return false;
            _wood -= amount;
            OnLocalResourcesChanged?.Invoke(_wood, _stone, _food);
            return true;
        }

        public bool SpendStone(int amount)
        {
            if (_stone < amount) return false;
            _stone -= amount;
            OnLocalResourcesChanged?.Invoke(_wood, _stone, _food);
            return true;
        }

        public bool SpendFood(int amount)
        {
            if (_food < amount) return false;
            _food -= amount;
            OnLocalResourcesChanged?.Invoke(_wood, _stone, _food);
            return true;
        }

        #endregion

        #region 收集所有建筑产出

        /// <summary>
        /// 收集所有资源建筑的产出。
        /// </summary>
        public void CollectAllResources()
        {
            var buildingManager = FindObjectOfType<BuildingManager>();
            if (buildingManager == null) return;

            int totalCollected = 0;
            foreach (var building in buildingManager.Buildings)
            {
                var resourceBuilding = building as ResourceBuilding;
                if (resourceBuilding != null)
                {
                    totalCollected += resourceBuilding.CollectResources();
                }
            }

            if (totalCollected > 0)
            {
                Debug.Log($"[ResourceManager] 收集所有建筑产出: {totalCollected}");
            }
        }

        #endregion

        #region 存档

        public void LoadFromSave(HomeSaveData data)
        {
            if (data == null) return;
            _wood = data.wood;
            _stone = data.stone;
            _food = data.food;
            OnLocalResourcesChanged?.Invoke(_wood, _stone, _food);
        }

        public void SaveToSave(HomeSaveData data)
        {
            data.wood = _wood;
            data.stone = _stone;
            data.food = _food;
        }

        #endregion
    }
}
