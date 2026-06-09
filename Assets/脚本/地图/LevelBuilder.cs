using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using TowerDefense.Data;
using TowerDefense.Utils;

namespace TowerDefense.Map
{
    public class LevelBuilder : MonoBehaviour
    {
        [Header("Tilemaps")]
        [SerializeField] private Tilemap groundMap;
        [SerializeField] private Tilemap pathMap;
        [SerializeField] private Tilemap buildSlotMap;
        [SerializeField] private Tilemap decorationMap;

        [Header("Tile Assets")]
        [SerializeField] private TileBase grassTile;
        [SerializeField] private TileBase pathTile;
        [SerializeField] private TileBase buildSlotTile;
        [SerializeField] private TileBase decoStoneTile;
        [SerializeField] private TileBase decoGrassTile1;
        [SerializeField] private TileBase decoGrassTile2;
        [SerializeField] private TileBase decoGrassTile3;

        [Header("Path Tiles (Auto-selected)")]
        [SerializeField] private TileBase pathHorizontalTop;
        [SerializeField] private TileBase pathHorizontalBottom;
        [SerializeField] private TileBase pathVerticalLeft;
        [SerializeField] private TileBase pathVerticalRight;
        [SerializeField] private TileBase pathCornerTopRight;
        [SerializeField] private TileBase pathCornerTopLeft;
        [SerializeField] private TileBase pathCornerBottomRight;
        [SerializeField] private TileBase pathCornerBottomLeft;

        [Header("Prefabs")]
        [SerializeField] private GameObject towerSlotPrefab;

        private List<Tower.TowerSlot> _towerSlots = new List<Tower.TowerSlot>();
        private Vector3Int _startCell;
        private Vector3Int _endCell;

        /// <summary>各分支的路径格子集合（'1'→index 0, '2'→index 1...）</summary>
        private List<HashSet<Vector3Int>> _branchCells = new List<HashSet<Vector3Int>>();

        public List<Tower.TowerSlot> TowerSlots => _towerSlots;
        public Vector3Int StartCell => _startCell;
        public Vector3Int EndCell => _endCell;

        // 判断字符是否为路径类格子（P/S/E/1/2/3...）
        private static bool IsPathChar(char c)
        {
            return c == 'P' || c == 'S' || c == 'E' || (c >= '1' && c <= '9');
        }

        // 判断字符是否为分支路径（1/2/3...）
        private static bool IsBranchChar(char c)
        {
            return c >= '1' && c <= '9';
        }

        // 获取分支索引（'1'→0, '2'→1, ...）
        private static int BranchIndex(char c)
        {
            return c - '1';
        }

        public void BuildLevel(LevelData levelData)
        {
            if (levelData?.mapRows == null || levelData.mapRows.Length == 0)
            {
                Debug.LogError("LevelData has no map rows!");
                return;
            }

            ClearAll();

            int height = levelData.mapRows.Length;
            int width = levelData.mapRows[0].Length;

            var pathCells = new HashSet<Vector3Int>();
            var buildCells = new HashSet<Vector3Int>();
            var decoCells = new HashSet<Vector3Int>();
            _branchCells = new List<HashSet<Vector3Int>>();

            // First pass: collect all cell positions
            for (int row = 0; row < height; row++)
            {
                string line = levelData.mapRows[row];
                for (int col = 0; col < Mathf.Min(line.Length, width); col++)
                {
                    char c = line[col];
                    var cell = new Vector3Int(col, height - 1 - row, 0);

                    if (IsPathChar(c))
                    {
                        pathCells.Add(cell);
                        if (c == 'S') _startCell = cell;
                        if (c == 'E') _endCell = cell;

                        // 记录分支格子
                        if (IsBranchChar(c))
                        {
                            int idx = BranchIndex(c);
                            while (_branchCells.Count <= idx)
                                _branchCells.Add(new HashSet<Vector3Int>());
                            _branchCells[idx].Add(cell);
                        }
                    }
                    else if (c == 'B') buildCells.Add(cell);
                    else if (c == 'D') decoCells.Add(cell);
                }
            }

            // Second pass: paint tiles
            for (int row = 0; row < height; row++)
            {
                string line = levelData.mapRows[row];
                for (int col = 0; col < Mathf.Min(line.Length, width); col++)
                {
                    char c = line[col];
                    var cell = new Vector3Int(col, height - 1 - row, 0);

                    if (c != '.') groundMap.SetTile(cell, grassTile);

                    if (IsPathChar(c))
                        pathMap.SetTile(cell, SelectPathTile(cell, pathCells));
                    else if (c == 'B')
                        buildSlotMap.SetTile(cell, buildSlotTile);
                    else if (c == 'D')
                        decorationMap.SetTile(cell, PickRandomDeco());
                }
            }

            GenerateTowerSlots();
            UpdatePathCreators();

            int branchCount = _branchCells.Count;
            Debug.Log($"Level '{levelData.levelName}' built: {width}x{height}, " +
                      $"path={pathCells.Count}, build={buildCells.Count}, deco={decoCells.Count}, branches={branchCount}");
        }

        // ─── PathCreator 管理 ─────────────────────────────────

        private const string PATH_CREATOR_PARENT = "路径";

        /// <summary>
        /// 根据分支数量，更新场景中的 PathCreator。
        /// 复用已有的 PathCreator（不销毁），只更新路径点。
        /// 如果 PathCreator 已有手动设置的路径点，则不覆盖。
        /// </summary>
        private void UpdatePathCreators()
        {
            var existing = FindObjectsOfType<PathCreator>().ToList();

            if (_branchCells.Count == 0)
            {
                // 无分支：单路径
                var waypoints = GetPathWaypoints(null);
                EnsurePathCreator(existing, 0, "路径", waypoints);
            }
            else
            {
                // 有分支：每个分支一条路径
                for (int i = 0; i < _branchCells.Count; i++)
                {
                    var waypoints = GetPathWaypoints(_branchCells[i]);
                    string name = _branchCells.Count == 1 ? "路径" : $"路径_{i + 1}路";
                    EnsurePathCreator(existing, i, name, waypoints);
                }
            }

            // 清理多余 PathCreator（只删自动创建的，不删用户手动的）
            var allAfter = FindObjectsOfType<PathCreator>();
            int expected = _branchCells.Count > 0 ? _branchCells.Count : 1;
            if (allAfter.Length > expected + 1) // +1 容差
            {
                for (int i = expected; i < allAfter.Length; i++)
                {
                    if (allAfter[i] != null)
                    {
                        Debug.Log($"[LevelBuilder] 清理多余 PathCreator: {allAfter[i].gameObject.name}");
                        Destroy(allAfter[i].gameObject);
                    }
                }
            }
        }

        /// <summary>
        /// 确保指定索引的 PathCreator 存在并更新路径点。
        /// 复用已有对象，不销毁重建（保持引用不断裂）。
        /// </summary>
        private void EnsurePathCreator(List<PathCreator> existing, int index, string objectName, List<Vector3> waypoints)
        {
            // 按名字或索引找已有的 PathCreator
            PathCreator pc = null;

            // 先按名字找
            foreach (var e in existing)
            {
                if (e != null && (e.gameObject.name == objectName || e.gameObject.name == "路径"))
                {
                    pc = e;
                    break;
                }
            }

            // 按索引找
            if (pc == null && index < existing.Count)
                pc = existing[index];

            // 都没有，创建新的
            if (pc == null)
            {
                var parent = FindOrCreatePathParent();
                var go = new GameObject(objectName);
                go.transform.SetParent(parent.transform, false);
                pc = go.AddComponent<PathCreator>();
            }

            // 更新路径点：如果自动追踪到了有效路径就覆盖，否则保留已有数据
            if (waypoints.Count > 0)
            {
                pc.ClearPath();
                foreach (var wp in waypoints)
                    pc.AddWaypoint(wp);
                Debug.Log($"[LevelBuilder] PathCreator '{pc.gameObject.name}' 已更新: {waypoints.Count} 个路径点");
            }
            else if (pc.WaypointCount == 0)
            {
                Debug.LogWarning($"[LevelBuilder] PathCreator '{pc.gameObject.name}' 无自动路径点，也无手动路径点！");
            }
            else
            {
                Debug.Log($"[LevelBuilder] PathCreator '{pc.gameObject.name}' 自动追踪失败，保留现有 {pc.WaypointCount} 个路径点");
            }
        }

        private GameObject FindOrCreatePathParent()
        {
            var parent = GameObject.Find(PATH_CREATOR_PARENT);
            if (parent == null) parent = GameObject.Find("路径");
            if (parent == null) parent = new GameObject(PATH_CREATOR_PARENT);
            return parent;
        }

        // ─── 路径追踪 ──────────────────────────────────────────

        /// <summary>
        /// 从 Tilemap 追踪路径拐点。
        /// branchCells 为 null 时，走所有路径格（P+S+E+所有分支）；
        /// 不为 null 时，走共享路径格（P+S+E）+ 指定分支格。
        /// </summary>
        public List<Vector3> GetPathWaypoints(HashSet<Vector3Int> branchCells)
        {
            var waypoints = new List<Vector3>();
            if (pathMap == null) return waypoints;

            // 收集所有路径格
            var allPathCells = new HashSet<Vector3Int>();
            BoundsInt bounds = pathMap.cellBounds;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                var c = new Vector3Int(x, y, 0);
                if (pathMap.GetTile(c) != null) allPathCells.Add(c);
            }

            if (allPathCells.Count == 0) return waypoints;

            // 构建当前分支可走的格子集合
            HashSet<Vector3Int> walkable;
            if (branchCells == null || branchCells.Count == 0)
            {
                walkable = allPathCells;
            }
            else
            {
                // 共享格（P/S/E）+ 当前分支格
                walkable = new HashSet<Vector3Int>();
                // 找出共享格：属于所有分支都不独占的格子
                var allBranchCells = new HashSet<Vector3Int>();
                foreach (var bc in _branchCells)
                    allBranchCells.UnionWith(bc);

                foreach (var cell in allPathCells)
                {
                    if (!allBranchCells.Contains(cell) || branchCells.Contains(cell))
                        walkable.Add(cell);
                }
            }

            Vector3Int start = _startCell;
            Vector3Int end = _endCell;

            if (!walkable.Contains(start) || !walkable.Contains(end)) return waypoints;

            // BFS
            var prev = new Dictionary<Vector3Int, Vector3Int>();
            var queue = new Queue<Vector3Int>();
            var visited = new HashSet<Vector3Int>();
            queue.Enqueue(start);
            visited.Add(start);
            prev[start] = start;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (cur == end) break;
                foreach (var dir in new[] { Vector3Int.right, Vector3Int.up, Vector3Int.left, Vector3Int.down })
                {
                    var nb = cur + dir;
                    if (walkable.Contains(nb) && !visited.Contains(nb))
                    {
                        visited.Add(nb);
                        prev[nb] = cur;
                        queue.Enqueue(nb);
                    }
                }
            }

            if (!prev.ContainsKey(end)) return waypoints;

            var path = new List<Vector3Int>();
            var node = end;
            while (node != start) { path.Add(node); node = prev[node]; }
            path.Add(start);
            path.Reverse();

            // 简化：只保留拐点
            var simplified = new List<Vector3Int> { path[0] };
            for (int i = 1; i < path.Count - 1; i++)
            {
                if (path[i] - path[i - 1] != path[i + 1] - path[i])
                    simplified.Add(path[i]);
            }
            simplified.Add(path[path.Count - 1]);

            foreach (var cell in simplified)
                waypoints.Add(pathMap.CellToWorld(cell) + new Vector3(0.5f, 0.5f, 0));

            return waypoints;
        }

        // ─── Tile 选择 ──────────────────────────────────────────

        private TileBase SelectPathTile(Vector3Int cell, HashSet<Vector3Int> pathCells)
        {
            bool up = pathCells.Contains(cell + Vector3Int.up);
            bool down = pathCells.Contains(cell + Vector3Int.down);
            bool left = pathCells.Contains(cell + Vector3Int.left);
            bool right = pathCells.Contains(cell + Vector3Int.right);

            bool upLeft = pathCells.Contains(cell + Vector3Int.up + Vector3Int.left);
            bool upRight = pathCells.Contains(cell + Vector3Int.up + Vector3Int.right);
            bool downLeft = pathCells.Contains(cell + Vector3Int.down + Vector3Int.left);
            bool downRight = pathCells.Contains(cell + Vector3Int.down + Vector3Int.right);

            bool upIsParallel = up && upLeft && upRight;
            bool downIsParallel = down && downLeft && downRight;
            bool leftIsParallel = left && upLeft && downLeft;
            bool rightIsParallel = right && upRight && downRight;

            if (left && right)
            {
                if (upIsParallel) return pathHorizontalBottom;
                if (downIsParallel) return pathHorizontalTop;
                if (up && !down) return pathHorizontalBottom;
                if (down && !up) return pathHorizontalTop;
                if (!downRight && upLeft) return pathCornerBottomRight;
                if (!downLeft && upRight) return pathCornerBottomLeft;
                if (!upRight && downLeft) return pathCornerTopRight;
                if (!upLeft && downRight) return pathCornerTopLeft;
                return up ? pathHorizontalBottom : pathHorizontalTop;
            }

            if (up && down)
            {
                if (rightIsParallel) return pathVerticalLeft;
                if (leftIsParallel) return pathVerticalRight;
                if (right && !left) return pathVerticalLeft;
                if (left && !right) return pathVerticalRight;
                if (!downRight && upLeft) return pathCornerBottomRight;
                if (!downLeft && upRight) return pathCornerBottomLeft;
                if (!upRight && downLeft) return pathCornerTopRight;
                if (!upLeft && downRight) return pathCornerTopLeft;
                return right ? pathVerticalLeft : pathVerticalRight;
            }

            if (up && right && !down && !left) return pathCornerBottomLeft;
            if (up && left && !down && !right) return pathCornerBottomRight;
            if (down && right && !up && !left) return pathCornerTopLeft;
            if (down && left && !up && !right) return pathCornerTopRight;

            if (left || right) return up ? pathHorizontalBottom : pathHorizontalTop;
            if (up || down) return right ? pathVerticalLeft : pathVerticalRight;

            return pathTile;
        }

        private TileBase PickRandomDeco()
        {
            float r = Random.value;
            if (r < 0.4f) return decoGrassTile1;
            if (r < 0.7f) return decoGrassTile2;
            if (r < 0.9f) return decoGrassTile3;
            return decoStoneTile;
        }

        // ─── 建造槽 ──────────────────────────────────────────

        private void GenerateTowerSlots()
        {
            if (buildSlotMap == null || towerSlotPrefab == null) return;

            _towerSlots.Clear();

            Transform buildSlotsParent = null;
            var existingParent = GameObject.Find("建造槽");
            if (existingParent == null) existingParent = GameObject.Find("BuildSlots");
            if (existingParent != null)
            {
                for (int i = existingParent.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(existingParent.transform.GetChild(i).gameObject);
                buildSlotsParent = existingParent.transform;
            }

            BoundsInt bounds = buildSlotMap.cellBounds;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cellPosition = new Vector3Int(x, y, 0);
                    if (buildSlotMap.GetTile(cellPosition) == null) continue;

                    Vector3 worldPosition = buildSlotMap.CellToWorld(cellPosition) + new Vector3(0.5f, 0.5f, 0);
                    GameObject slotObj = Instantiate(towerSlotPrefab, worldPosition, Quaternion.identity);
                    slotObj.name = $"TowerSlot_{x}_{y}";
                    if (buildSlotsParent != null) slotObj.transform.SetParent(buildSlotsParent, true);

                    Tower.TowerSlot slot = slotObj.GetComponent<Tower.TowerSlot>();
                    if (slot != null) _towerSlots.Add(slot);
                }
            }
            Debug.Log($"Generated {_towerSlots.Count} tower slots");
        }

        // ─── 清理 ──────────────────────────────────────────

        public void ClearAll()
        {
            foreach (var slot in _towerSlots) { if (slot != null) Destroy(slot.gameObject); }
            _towerSlots.Clear();

            var buildSlotsObj = GameObject.Find("建造槽");
            if (buildSlotsObj == null) buildSlotsObj = GameObject.Find("BuildSlots");
            if (buildSlotsObj != null)
            {
                for (int i = buildSlotsObj.transform.childCount - 1; i >= 0; i--)
                    DestroyImmediate(buildSlotsObj.transform.GetChild(i).gameObject);
            }

            groundMap?.ClearAllTiles();
            pathMap?.ClearAllTiles();
            buildSlotMap?.ClearAllTiles();
            decorationMap?.ClearAllTiles();
        }
    }
}
