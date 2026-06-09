using System;
using System.Collections.Generic;
using UnityEngine;
using Sttop5.Shared.Core;
using Sttop5.Shared.Player;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 资源建筑，自动产出资源。
    /// </summary>
    public class ResourceBuilding : BuildingBase
    {
        private float _accumulated;

        protected override void BuildingUpdate()
        {
            if (_data == null || _data.buildingType != BuildingType.ResourceGen)
                return;

            var levelData = CurrentLevelData;
            if (levelData.productionRate <= 0) return;

            _productionTimer += Time.deltaTime;
            float interval = 1f / levelData.productionRate;

            if (_productionTimer >= interval)
            {
                _productionTimer -= interval;
                _accumulated++;

                if (_accumulated >= levelData.productionCapacity)
                {
                    CollectResources();
                }
            }
        }

        /// <summary>
        /// 收集已产出的资源。
        /// </summary>
        public int CollectResources()
        {
            int collected = (int)_accumulated;
            if (collected <= 0) return 0;

            _accumulated = 0;

            var profile = PlayerProfile.Instance;
            if (profile != null)
            {
                switch (_data.producedResource)
                {
                    case ResourceType.Gold:
                        profile.AddGold(collected, "home_resource");
                        break;
                    case ResourceType.Diamond:
                        profile.AddDiamond(collected, "home_resource");
                        break;
                }
            }

            Debug.Log($"[ResourceBuilding] 收集 {collected} {_data.producedResource} from {_data.buildingName}");
            return collected;
        }

        /// <summary>
        /// 计算离线收益。
        /// </summary>
        public int CalculateOfflineEarnings(float offlineSeconds)
        {
            if (_data == null || _data.buildingType != BuildingType.ResourceGen) return 0;
            var levelData = CurrentLevelData;
            return Mathf.FloorToInt(levelData.productionRate * offlineSeconds);
        }

        protected override void OnUpgraded()
        {
            // 升级后立即收集一次
            CollectResources();
        }
    }
}
