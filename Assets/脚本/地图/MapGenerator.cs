using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace TowerDefense.Map
{
    public class MapGenerator : MonoBehaviour
    {
        [Header("Tilemaps")]
        [SerializeField] private Tilemap groundMap;
        [SerializeField] private Tilemap pathMap;
        [SerializeField] private Tilemap buildSlotMap;
        [SerializeField] private Tilemap decorationMap;

        [Header("Prefabs")]
        [SerializeField] private GameObject towerSlotPrefab;

        [Header("Map Settings")]
        [SerializeField] private Vector3 mapSize = new Vector3(20, 15, 0);

        private List<Tower.TowerSlot> towerSlots = new List<Tower.TowerSlot>();

        public void InitializeMap()
        {
            GenerateTowerSlots();
            InitializePaths();
        }

        private void GenerateTowerSlots()
        {
            if (buildSlotMap == null || towerSlotPrefab == null) return;

            towerSlots.Clear();

            BoundsInt bounds = buildSlotMap.cellBounds;
            TileBase[] allTiles = buildSlotMap.GetTilesBlock(bounds);

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cellPosition = new Vector3Int(x, y, 0);
                    TileBase tile = buildSlotMap.GetTile(cellPosition);

                    if (tile != null)
                    {
                        Vector3 worldPosition = buildSlotMap.CellToWorld(cellPosition);
                        worldPosition += new Vector3(0.5f, 0.5f, 0);

                        GameObject slotObj = Instantiate(towerSlotPrefab, worldPosition, Quaternion.identity);
                        slotObj.name = $"TowerSlot_{x}_{y}";

                        Tower.TowerSlot slot = slotObj.GetComponent<Tower.TowerSlot>();
                        if (slot != null)
                        {
                            towerSlots.Add(slot);
                        }
                    }
                }
            }

            Debug.Log($"Generated {towerSlots.Count} tower slots");
        }

        private void InitializePaths()
        {
            if (pathMap == null) return;

            List<Vector3> waypoints = new List<Vector3>();

            BoundsInt bounds = pathMap.cellBounds;
            TileBase[] allTiles = pathMap.GetTilesBlock(bounds);

            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int cellPosition = new Vector3Int(x, y, 0);
                    TileBase tile = pathMap.GetTile(cellPosition);

                    if (tile != null)
                    {
                        Vector3 worldPosition = pathMap.CellToWorld(cellPosition);
                        worldPosition += new Vector3(0.5f, 0.5f, 0);
                        waypoints.Add(worldPosition);
                    }
                }
            }

            Debug.Log($"Found {waypoints.Count} path points");
        }

        public List<Tower.TowerSlot> GetTowerSlots()
        {
            return towerSlots;
        }

        public void ClearMap()
        {
            foreach (var slot in towerSlots)
            {
                if (slot != null && slot.CurrentTower != null)
                {
                    slot.SellTower();
                }
            }

            towerSlots.Clear();
        }

        private void OnDrawGizmos()
        {
            if (buildSlotMap != null)
            {
                Gizmos.color = Color.green;
                BoundsInt bounds = buildSlotMap.cellBounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }

            if (pathMap != null)
            {
                Gizmos.color = Color.red;
                BoundsInt bounds = pathMap.cellBounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }
}
