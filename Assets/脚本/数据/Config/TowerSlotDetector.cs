using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using TowerDefense.Tower;

public class TowerSlotDetector : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Tilemap buildableTilemap;
    [SerializeField] private TileBase buildableTile;
    [SerializeField] private GameObject towerSlotPrefab;
    [SerializeField] private Transform towerSlotsContainer;
    
    [Header("Gizmo Settings")]
    [SerializeField] private Color slotColor = Color.green;
    
    [ContextMenu("Detect Tower Slots")]
    public void DetectTowerSlots()
    {
        if (buildableTilemap == null || towerSlotPrefab == null) return;
        
        ClearExistingSlots();
        
        BoundsInt bounds = buildableTilemap.cellBounds;
        TileBase[] allTiles = buildableTilemap.GetTilesBlock(bounds);
        
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                TileBase tile = buildableTilemap.GetTile(cell);
                
                if (tile != null && tile == buildableTile)
                {
                    CreateTowerSlot(cell);
                }
            }
        }
        
        Debug.Log($"Created {towerSlotsContainer.childCount} tower slots");
    }
    
    private void CreateTowerSlot(Vector3Int cell)
    {
        Vector3 worldPos = buildableTilemap.CellToWorld(cell);
        worldPos += new Vector3(0.5f, 0.5f, 0);
        
        GameObject slotObj = Instantiate(towerSlotPrefab, worldPos, Quaternion.identity);
        slotObj.name = $"TowerSlot_{cell.x}_{cell.y}";
        slotObj.transform.SetParent(towerSlotsContainer);
        
        TowerSlot slot = slotObj.GetComponent<TowerSlot>();
        if (slot != null)
        {
            Debug.Log($"Created tower slot at {cell}");
        }
    }
    
    [ContextMenu("Clear Tower Slots")]
    public void ClearExistingSlots()
    {
        for (int i = towerSlotsContainer.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(towerSlotsContainer.GetChild(i).gameObject);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (buildableTilemap == null) return;
        
        BoundsInt bounds = buildableTilemap.cellBounds;
        
        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                TileBase tile = buildableTilemap.GetTile(cell);
                
                if (tile != null && tile == buildableTile)
                {
                    Vector3 worldPos = buildableTilemap.CellToWorld(cell);
                    worldPos += new Vector3(0.5f, 0.5f, 0);
                    
                    Gizmos.color = slotColor;
                    Gizmos.DrawWireSphere(worldPos, 0.4f);
                }
            }
        }
    }
}