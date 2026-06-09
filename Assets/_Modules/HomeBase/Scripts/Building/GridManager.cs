using System.Collections.Generic;
using UnityEngine;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 网格管理器，管理 2.5D 场景中建筑占用的网格。
    /// 建筑在 XZ 平面上摆放，Y 轴为高度。
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("网格配置")]
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private int _gridWidth = 20;
        [SerializeField] private int _gridHeight = 20;
        [SerializeField] private Vector3 _origin = Vector3.zero;

        [Header("可视化")]
        [SerializeField] private bool _showGrid = true;
        [SerializeField] private Color _gridColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color _occupiedColor = new Color(1f, 0f, 0f, 0.3f);

        /// <summary>网格坐标 → 占用该格的建筑</summary>
        private Dictionary<Vector2Int, BuildingBase> _occupancy = new Dictionary<Vector2Int, BuildingBase>();

        public float CellSize => _cellSize;
        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;

        #region 坐标转换

        /// <summary>世界坐标 → 网格坐标</summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 local = worldPos - _origin;
            int x = Mathf.FloorToInt(local.x / _cellSize);
            int y = Mathf.FloorToInt(local.z / _cellSize);
            return new Vector2Int(x, y);
        }

        /// <summary>网格坐标 → 世界坐标（格子中心点）</summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            return _origin + new Vector3(
                (gridPos.x + 0.5f) * _cellSize,
                0f,
                (gridPos.y + 0.5f) * _cellSize
            );
        }

        /// <summary>世界坐标吸附到网格中心</summary>
        public Vector3 SnapToGrid(Vector3 worldPos)
        {
            return GridToWorld(WorldToGrid(worldPos));
        }

        #endregion

        #region 占用查询

        /// <summary>判断网格坐标是否在有效范围内</summary>
        public bool IsValidGridPos(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < _gridWidth
                && gridPos.y >= 0 && gridPos.y < _gridHeight;
        }

        /// <summary>判断指定区域是否全部空闲</summary>
        public bool IsAreaAvailable(Vector2Int start, Vector2Int size)
        {
            for (int x = start.x; x < start.x + size.x; x++)
            {
                for (int y = start.y; y < start.y + size.y; y++)
                {
                    var pos = new Vector2Int(x, y);
                    if (!IsValidGridPos(pos)) return false;
                    if (_occupancy.ContainsKey(pos)) return false;
                }
            }
            return true;
        }

        /// <summary>获取占用指定格子的建筑</summary>
        public BuildingBase GetOccupant(Vector2Int gridPos)
        {
            _occupancy.TryGetValue(gridPos, out var building);
            return building;
        }

        #endregion

        #region 占用操作

        /// <summary>占用指定区域</summary>
        public bool OccupyArea(Vector2Int start, Vector2Int size, BuildingBase building)
        {
            if (!IsAreaAvailable(start, size)) return false;

            for (int x = start.x; x < start.x + size.x; x++)
            {
                for (int y = start.y; y < start.y + size.y; y++)
                {
                    _occupancy[new Vector2Int(x, y)] = building;
                }
            }
            return true;
        }

        /// <summary>释放指定区域</summary>
        public void ReleaseArea(Vector2Int start, Vector2Int size)
        {
            for (int x = start.x; x < start.x + size.x; x++)
            {
                for (int y = start.y; y < start.y + size.y; y++)
                {
                    _occupancy.Remove(new Vector2Int(x, y));
                }
            }
        }

        /// <summary>释放指定建筑占用的所有格子</summary>
        public void ReleaseBuilding(BuildingBase building)
        {
            var keysToRemove = new List<Vector2Int>();
            foreach (var kvp in _occupancy)
            {
                if (kvp.Value == building)
                    keysToRemove.Add(kvp.Key);
            }
            foreach (var key in keysToRemove)
                _occupancy.Remove(key);
        }

        /// <summary>清除所有占用</summary>
        public void ClearAll()
        {
            _occupancy.Clear();
        }

        #endregion

        #region Gizmos 可视化

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_showGrid) return;

            // 画网格线
            Gizmos.color = _gridColor;
            for (int x = 0; x <= _gridWidth; x++)
            {
                Vector3 from = _origin + new Vector3(x * _cellSize, 0.01f, 0f);
                Vector3 to = _origin + new Vector3(x * _cellSize, 0.01f, _gridHeight * _cellSize);
                Gizmos.DrawLine(from, to);
            }
            for (int y = 0; y <= _gridHeight; y++)
            {
                Vector3 from = _origin + new Vector3(0f, 0.01f, y * _cellSize);
                Vector3 to = _origin + new Vector3(_gridWidth * _cellSize, 0.01f, y * _cellSize);
                Gizmos.DrawLine(from, to);
            }

            // 画占用格子
            Gizmos.color = _occupiedColor;
            foreach (var kvp in _occupancy)
            {
                Vector3 center = GridToWorld(kvp.Key);
                center.y = 0.02f;
                Gizmos.DrawCube(center, new Vector3(_cellSize * 0.9f, 0.01f, _cellSize * 0.9f));
            }
        }
#endif

        #endregion
    }
}
