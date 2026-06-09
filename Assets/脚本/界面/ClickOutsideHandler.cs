using UnityEngine;
using UnityEngine.EventSystems;
using TowerDefense.Tower;

namespace TowerDefense.UI
{
    public class ClickOutsideHandler : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TowerSlot[] _towerSlots;
        
        public void OnPointerClick(PointerEventData eventData)
        {
            HideAllPanels();
            DeselectAllSlots();
        }
        
        private void HideAllPanels()
        {
            TowerSelectPanel selectPanel = FindObjectOfType<TowerSelectPanel>();
            if (selectPanel != null)
            {
                selectPanel.Hide();
            }
            
            TowerInfoPanel infoPanel = FindObjectOfType<TowerInfoPanel>();
            if (infoPanel != null)
            {
                infoPanel.Hide();
            }
            
            if (BuildButtonManager.Instance != null)
            {
                BuildButtonManager.Instance.HideBuildButton();
            }
        }
        
        private void DeselectAllSlots()
        {
            if (_towerSlots == null || _towerSlots.Length == 0)
            {
                _towerSlots = FindObjectsOfType<TowerSlot>();
            }
            
            foreach (var slot in _towerSlots)
            {
                slot.DeselectSlot();
            }
        }
    }
}
