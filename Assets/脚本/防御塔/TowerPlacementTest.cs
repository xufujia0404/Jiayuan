using UnityEngine;
using TowerDefense.Tower;
using TowerDefense.Data;

public class TowerPlacementTest : MonoBehaviour
{
    [SerializeField] private TowerData towerData;
    
    /// <summary>
    /// 当前选中的槽位
    /// </summary>
    private TowerSlot _selectedSlot;

    private void Update()
    {
        // 检测鼠标点击槽位
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);
            
            if (hit.collider != null)
            {
                TowerSlot slot = hit.collider.GetComponent<TowerSlot>();
                if (slot != null)
                {
                    // 取消之前选中的槽位高亮
                    if (_selectedSlot != null)
                    {
                        _selectedSlot.DeselectSlot();
                    }
                    
                    // 选中新槽位
                    _selectedSlot = slot;
                    _selectedSlot.SelectSlot();
                    
                    Debug.Log($"Selected slot: {_selectedSlot.name}");
                }
            }
        }
    }

    /// <summary>
    /// 放置塔按钮回调
    /// </summary>
    public void OnPlaceTowerButton()
    {
        if (towerData == null)
        {
            Debug.LogError("Tower Data is not assigned!");
            return;
        }

        // 如果没有选中槽位，提示玩家
        if (_selectedSlot == null)
        {
            Debug.LogWarning("⚠️ Please click on a tower slot first!");
            return;
        }

        // 在选中的槽位放置塔
        bool success = _selectedSlot.PlaceTower(towerData);
        
        if (success)
        {
            Debug.Log($"✅ Tower placed successfully on slot: {_selectedSlot.name}");
            
            // 取消选中状态
            _selectedSlot.DeselectSlot();
            _selectedSlot = null;
        }
        else
        {
            Debug.LogWarning("❌ Failed to place tower! Check gold or slot status.");
        }
    }

    /// <summary>
    /// 获取当前选中的槽位
    /// </summary>
    public TowerSlot GetSelectedSlot()
    {
        return _selectedSlot;
    }
}