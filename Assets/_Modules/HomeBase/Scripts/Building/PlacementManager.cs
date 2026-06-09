using System.Collections.Generic;
using UnityEngine;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 自由放置管理器，替代原有的网格系统。
    /// 使用碰撞检测管理建筑占用，支持帕鲁风格自由建造。
    /// </summary>
    public class PlacementManager : MonoBehaviour
    {
        [Header("放置配置")]
        [SerializeField] private LayerMask _groundLayer;
        [SerializeField] private float _minDistanceBetweenBuildings = 0.3f;
        [SerializeField] private float _maxPlaceHeight = 100f;

        [Header("可视化")]
        [SerializeField] private bool _showBounds = true;
        [SerializeField] private Color _boundsColor = new Color(0f, 1f, 0.5f, 0.3f);
        [SerializeField] private Color _overlapColor = new Color(1f, 0f, 0f, 0.3f);

        /// <summary>已放置的建筑列表。</summary>
        private readonly List<BuildingBase> _placedBuildings = new List<BuildingBase>();

        /// <summary>放置区域边界（可选，限制建造范围）。</summary>
        [Header("放置区域（可选）")]
        [SerializeField] private Vector3 _areaCenter = Vector3.zero;
        [SerializeField] private Vector3 _areaSize = new Vector3(50f, 0f, 50f);
        [SerializeField] private bool _limitArea = false;

        public IReadOnlyList<BuildingBase> PlacedBuildings => _placedBuildings;

        #region 射线检测

        /// <summary>
        /// 从屏幕坐标射线检测地面位置。
        /// </summary>
        public bool RaycastGround(Camera camera, Vector3 screenPos, out Vector3 hitPoint)
        {
            hitPoint = Vector3.zero;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(screenPos);

            // 优先使用 Physics 射线（Layer 精确）
            if (_groundLayer != 0 && Physics.Raycast(ray, out RaycastHit hit, 1000f, _groundLayer))
            {
                hitPoint = hit.point;
                return true;
            }

            // 回退：用 Plane 射线
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float distance))
            {
                hitPoint = ray.GetPoint(distance);
                return true;
            }

            return false;
        }

        #endregion

        #region 碰撞检测

        /// <summary>
        /// 检查指定位置是否可以放置建筑（不与已有建筑重叠，不超出区域）。
        /// </summary>
        public bool IsPositionAvailable(Vector3 worldPos, Vector3 footprintSize, int rotation)
        {
            // 高度限制
            if (worldPos.y > _maxPlaceHeight) return false;

            // 区域限制
            if (_limitArea)
            {
                var half = _areaSize * 0.5f;
                if (worldPos.x < _areaCenter.x - half.x || worldPos.x > _areaCenter.x + half.x ||
                    worldPos.z < _areaCenter.z - half.z || worldPos.z > _areaCenter.z + half.z)
                    return false;
            }

            // 计算当前建筑的世界包围盒
            Bounds newBounds = CalculateBounds(worldPos, footprintSize, rotation);

            // 检查与已有建筑的碰撞
            foreach (var building in _placedBuildings)
            {
                if (building == null) continue;

                Bounds existingBounds = GetBuildingBounds(building);
                if (newBounds.Intersects(existingBounds))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 简化版碰撞检测，只传位置和尺寸（旋转为0）。
        /// </summary>
        public bool IsPositionAvailable(Vector3 worldPos, Vector3 footprintSize)
        {
            return IsPositionAvailable(worldPos, footprintSize, 0);
        }

        #endregion

        #region 建筑注册

        /// <summary>
        /// 注册已放置的建筑。
        /// </summary>
        public void RegisterBuilding(BuildingBase building)
        {
            if (building != null && !_placedBuildings.Contains(building))
                _placedBuildings.Add(building);
        }

        /// <summary>
        /// 注销建筑（拆除时调用）。
        /// </summary>
        public void UnregisterBuilding(BuildingBase building)
        {
            _placedBuildings.Remove(building);
        }

        /// <summary>
        /// 清除所有已注册建筑。
        /// </summary>
        public void ClearAll()
        {
            _placedBuildings.Clear();
        }

        #endregion

        #region 包围盒计算

        /// <summary>
        /// 计算指定位置、尺寸、旋转的建筑世界包围盒。
        /// </summary>
        public static Bounds CalculateBounds(Vector3 worldPos, Vector3 footprintSize, int rotation)
        {
            // 旋转 90°/270° 时 XZ 互换
            Vector3 effectiveSize = footprintSize;
            if (rotation == 90 || rotation == 270)
            {
                effectiveSize = new Vector3(footprintSize.z, footprintSize.y, footprintSize.x);
            }

            // Y 方向默认 2m 高度用于碰撞检测
            effectiveSize.y = Mathf.Max(effectiveSize.y, 2f);

            return new Bounds(
                new Vector3(worldPos.x, worldPos.y + effectiveSize.y * 0.5f, worldPos.z),
                effectiveSize
            );
        }

        /// <summary>
        /// 获取已放置建筑的世界包围盒。
        /// </summary>
        public Bounds GetBuildingBounds(BuildingBase building)
        {
            if (building == null || building.Data == null)
                return new Bounds(building != null ? building.transform.position : Vector3.zero, Vector3.one);

            return CalculateBounds(
                building.transform.position,
                new Vector3(building.Data.footprintSize.x, 2f, building.Data.footprintSize.y),
                building.Rotation
            );
        }

        #endregion

        #region Gizmos

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showBounds) return;

            // 显示区域边界
            if (_limitArea)
            {
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.2f);
                Gizmos.DrawCube(_areaCenter, new Vector3(_areaSize.x, 0.1f, _areaSize.z));
            }

            // 显示已放置建筑的包围盒
            foreach (var building in _placedBuildings)
            {
                if (building == null) continue;
                Gizmos.color = _boundsColor;
                Gizmos.DrawCube(GetBuildingBounds(building).center, GetBuildingBounds(building).size);
            }
        }
#endif

        #endregion
    }
}
