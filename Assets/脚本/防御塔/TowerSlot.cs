using UnityEngine;
using UnityEngine.Events;
using TowerDefense.Core;
using TowerDefense.Data;
using TowerDefense.UI;

namespace TowerDefense.Tower
{
    /// <summary>
    /// 塔槽位组件 - 管理塔的放置、移除、升级和售卖
    /// </summary>
    public class TowerSlot : MonoBehaviour
    {
        [Header("Slot Settings")]
        [Tooltip("是否已被占用")]
        [SerializeField] private bool _isOccupied = false;
        
        [Tooltip("当前塔实例")]
        [SerializeField] private Tower _currentTower;
        
        [Tooltip("高亮效果物体")]
        [SerializeField] private GameObject _highlight;
        
        [Tooltip("建造指示器")]
        [SerializeField] private GameObject _buildIndicator;

        [Header("Slot Type")]
        [Tooltip("槽位类型")]
        [SerializeField] private LevelData.SlotType _slotType = LevelData.SlotType.Normal;

        [Header("Spawn Settings")]
        [Tooltip("塔的Z轴位置")]
        [SerializeField] private float _towerZPosition = -1f;
        
        [Tooltip("塔的缩放")]
        [SerializeField] private Vector3 _towerScale = Vector3.one;

        [Header("Build Button Settings")]
        [Tooltip("可以建造的塔列表")]
        [SerializeField] private TowerData[] buildableTowers;

        [Header("Events")]
        [Tooltip("塔放置事件")]
        public UnityEvent OnTowerPlaced;
        
        [Tooltip("塔移除事件")]
        [SerializeField] private UnityEvent OnTowerRemoved;
        
        [Tooltip("槽位选中事件")]
        public UnityEvent OnSlotSelected;

        /// <summary>
        /// 是否已被占用
        /// </summary>
        public bool IsOccupied => _isOccupied;
        
        /// <summary>
        /// 当前塔实例
        /// </summary>
        public Tower CurrentTower => _currentTower;
        
        /// <summary>
        /// 槽位类型
        /// </summary>
        public LevelData.SlotType SlotType => _slotType;

        /// <summary>
        /// 是否处于悬停状态
        /// </summary>
        private bool _isHovered = false;
        
        /// <summary>
        /// 是否处于选中状态
        /// </summary>
        private bool _isSelected = false;

        /// <summary>
        /// 初始化组件
        /// </summary>
        private void Awake()
        {
           // Debug.Log($"TowerSlot {gameObject.name} initialized");
            
            // 初始化高亮和建造指示器状态
            if (_highlight != null)
            {
                _highlight.SetActive(false);
            }
            if (_buildIndicator != null)
            {
                _buildIndicator.SetActive(!_isOccupied);
            }
        }

        /// <summary>
        /// 鼠标进入槽位
        /// </summary>
        private void OnMouseEnter()
        {
            if (!_isOccupied)
            {
                _isHovered = true;
                // Debug.Log($"Mouse entered slot: {gameObject.name}");
                
                // 显示高亮效果
                if (_highlight != null)
                {
                    _highlight.SetActive(true);
                }
            }
        }

        /// <summary>
        /// 鼠标离开槽位
        /// </summary>
        private void OnMouseExit()
        {
            _isHovered = false;
            // 非选中状态下隐藏高亮
            if (_highlight != null && !_isSelected)
            {
                _highlight.SetActive(false);
            }
        }

        void Start()
        {
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider != null)
            {
                collider.radius = 1f;
            }
        }
        
        /// <summary>
        /// 鼠标点击槽位
        /// </summary>
        private void OnMouseDown()
        {
            // 预览模式下点击槽位 = 确认放置，由 TowerPlacementPreview 处理
            var preview = TowerPlacementPreview.Instance;
            if (preview != null && preview.IsActive)
            {
                return;
            }

            Debug.Log($"Slot clicked: {gameObject.name}, IsOccupied: {_isOccupied}");
            
            // 取消之前的选中状态（不隐藏面板，面板常驻）
            DeselectAllSlots();
            HideTowerInfoPanel();
            
            if (!_isOccupied)
            {
                SelectSlot();
                ShowTowerSelectPanel();
            }
            else
            {
                SelectSlot();
                ShowTowerInfoPanel();
            }
        }

        /// <summary>
        /// 选中槽位
        /// </summary>
        public void SelectSlot()
        {
            _isSelected = true;
            OnSlotSelected?.Invoke();
                // Debug.Log($"Slot selected: {gameObject.name}");

            // 如果有塔，显示攻击范围
            if (_isOccupied && _currentTower != null)
            {
                _currentTower.ShowRange();
            }
        }

        /// <summary>
        /// 取消选中槽位
        /// </summary>
        public void DeselectSlot()
        {
            _isSelected = false;
            // 隐藏高亮效果
            if (_highlight != null)
            {
                _highlight.SetActive(false);
            }

            // 如果有塔，隐藏攻击范围
            if (_isOccupied && _currentTower != null)
            {
                _currentTower.HideRange();
            }
                // Debug.Log($"Slot deselected: {gameObject.name}");
        }

        /// <summary>
        /// 显示塔选择面板
        /// </summary>
        private void ShowTowerSelectPanel()
        {
            var panel = TowerSelectPanel.Instance;
            if (panel != null)
            {
                panel.Show(this);
                return;
            }

            // 备用：通过路径查找
            var panelObj = GameObject.Find("画布/塔选择面板");
            if (panelObj == null) panelObj = GameObject.Find("Canvas/TowerSelectPanel");
            if (panelObj != null)
            {
                panel = panelObj.GetComponent<TowerSelectPanel>();
            }
            if (panel == null)
            {
                panel = FindObjectOfType<TowerSelectPanel>();
            }
            if (panel != null)
            {
                panel.Show(this);
            }
        }
        
        /// <summary>
        /// 显示塔信息面板
        /// </summary>
        private void ShowTowerInfoPanel()
        {
            if (_currentTower == null) return;
            var infoPanel = TowerInfoPanel.Instance;
            if (infoPanel != null)
            {
                infoPanel.Show(_currentTower);
                return;
            }
            var infoObj = GameObject.Find("画布/塔信息面板");
            if (infoObj == null) infoObj = GameObject.Find("Canvas/TowerInfoPanel");
            if (infoObj != null)
            {
                infoPanel = infoObj.GetComponent<TowerInfoPanel>();
            }
            if (infoPanel == null)
            {
                infoPanel = FindObjectOfType<TowerInfoPanel>();
            }
            if (infoPanel != null)
            {
                infoPanel.Show(_currentTower);
            }
        }
        
        /// <summary>
        /// 取消所有槽位的选中状态
        /// </summary>
        private void DeselectAllSlots()
        {
            foreach (var slot in FindObjectsOfType<TowerSlot>())
            {
                if (slot._isSelected)
                {
                    slot.DeselectSlot();
                }
            }
        }

        /// <summary>
        /// 隐藏塔信息面板
        /// </summary>
        private void HideTowerInfoPanel()
        {
            var infoObj = GameObject.Find("画布/塔信息面板");
            if (infoObj == null) infoObj = GameObject.Find("Canvas/TowerInfoPanel");
            if (infoObj != null)
            {
                var infoPanel = infoObj.GetComponent<TowerInfoPanel>();
                if (infoPanel != null) infoPanel.Hide();
            }
            else
            {
                var ip = FindObjectOfType<TowerInfoPanel>();
                if (ip != null) ip.Hide();
            }
        }

        /// <summary>
        /// 隐藏所有UI面板
        /// </summary>
        private void HideAllPanels()
        {
            var selectObj = GameObject.Find("画布/塔选择面板");
            if (selectObj == null) selectObj = GameObject.Find("Canvas/TowerSelectPanel");
            if (selectObj != null)
            {
                var selectPanel = selectObj.GetComponent<TowerSelectPanel>();
                if (selectPanel != null) selectPanel.Hide();
            }
            else
            {
                var sp = FindObjectOfType<TowerSelectPanel>();
                if (sp != null) sp.Hide();
            }
            
            var infoObj = GameObject.Find("画布/塔信息面板");
            if (infoObj == null) infoObj = GameObject.Find("Canvas/TowerInfoPanel");
            if (infoObj != null)
            {
                var infoPanel = infoObj.GetComponent<TowerInfoPanel>();
                if (infoPanel != null) infoPanel.Hide();
            }
            else
            {
                var ip = FindObjectOfType<TowerInfoPanel>();
                if (ip != null) ip.Hide();
            }
            
            if (BuildButtonManager.Instance != null)
            {
                BuildButtonManager.Instance.HideBuildButton();
            }
        }

        /// <summary>
        /// 被管理器调用的建造按钮点击
        /// </summary>
        public void OnBuildButtonClickedByManager()
        {
                // Debug.Log("OnBuildButtonClickedByManager called!");
            
            // 尝试放置塔
            TowerData towerToBuild = GetTowerDataToBuild();
            
            if (towerToBuild != null)
            {
                bool success = PlaceTower(towerToBuild);
                
                if (success)
                {
                    // Debug.Log("✅ Tower built successfully!");
                }
                else
                {
                    // Debug.Log("❌ Failed to build tower!");
                }
            }
            else
            {
                // Debug.LogError("❌ No tower data to build!");
            }
        }

        /// <summary>
        /// 获取要建造的塔数据
        /// </summary>
        private TowerData GetTowerDataToBuild()
        {
            // 优先使用配置的塔列表
            if (buildableTowers != null && buildableTowers.Length > 0)
            {
                    // Debug.Log($"Using configured tower: {buildableTowers[0].towerName}");
                return buildableTowers[0];
            }
            
            // 尝试从Resources/Data/Towers/加载
            Debug.Log("Trying to load from Resources/Data/Towers/");
            TowerData[] towerDatas1 = Resources.LoadAll<TowerData>("Data/Towers");
            if (towerDatas1.Length > 0)
            {
                     Debug.Log($"Found {towerDatas1.Length} tower data assets in Data/Towers/");
                return towerDatas1[0];
            }
            
            // 尝试从Resources/Data/加载
            Debug.Log("Trying to load from Resources/Data/");
            TowerData[] towerDatas2 = Resources.LoadAll<TowerData>("Data");
            if (towerDatas2.Length > 0)
            {
                Debug.Log($"Found {towerDatas2.Length} tower data assets in Data/");
                return towerDatas2[0];
            }
            
                Debug.LogError("❌ No tower data found!");
            return null;
        }

        /// <summary>
        /// 在槽位放置塔
        /// </summary>
        /// <param name="towerData">塔数据</param>
        /// <returns>是否放置成功</returns>
        public bool PlaceTower(TowerData towerData)
        {
            if (_isOccupied) return false;
            
            int cost = towerData.levels[0].cost;
            if (!GameManager.Instance.SpendGold(cost)) return false;

            Vector3 spawnPos = transform.position;
            spawnPos.z = _towerZPosition;
            
            GameObject towerObj = Instantiate(towerData.towerPrefab, spawnPos, Quaternion.identity);
            if (towerObj == null) return false;
            
            if (_towerScale != Vector3.one)
            {
                towerObj.transform.localScale = _towerScale;
            }
            
            _currentTower = towerObj.GetComponent<Tower>();
            if (_currentTower != null)
            {
                _currentTower.Initialize(towerData, this);
            }

            _isOccupied = true;

            if (_buildIndicator != null)
            {
                _buildIndicator.SetActive(false);
            }
            if (_highlight != null)
            {
                _highlight.SetActive(false);
            }

            OnTowerPlaced?.Invoke();
            EventBus.Publish(new TowerPlacedEvent { Tower = towerObj, Cost = cost });

            return true;
        }

        /// <summary>
        /// 移除塔（不返还金币）
        /// </summary>
        public void RemoveTower()
        {
            _currentTower = null;
            _isOccupied = false;

            // 显示建造指示器
            if (_buildIndicator != null)
            {
                _buildIndicator.SetActive(true);
            }

            OnTowerRemoved?.Invoke();
        }

        /// <summary>
        /// 售卖塔（返还部分金币）
        /// </summary>
        public void SellTower()
        {
            if (!_isOccupied || _currentTower == null) return;

            _currentTower.Sell();
        }

        /// <summary>
        /// 升级塔
        /// </summary>
        public void UpgradeTower()
        {
            if (!_isOccupied || _currentTower == null) return;

            _currentTower.Upgrade();
        }

        /// <summary>
        /// 获取塔的售卖价值
        /// </summary>
        /// <returns>售卖金币数</returns>
        public int GetTowerSellValue()
        {
            if (!_isOccupied || _currentTower == null) return 0;
            return _currentTower.GetSellValue();
        }

        /// <summary>
        /// 获取塔的升级费用
        /// </summary>
        /// <returns>升级金币数</returns>
        public int GetTowerUpgradeCost()
        {
            if (!_isOccupied || _currentTower == null) return 0;
            return _currentTower.GetUpgradeCost();
        }

        /// <summary>
        /// 检查塔是否可以升级
        /// </summary>
        /// <returns>是否可升级</returns>
        public bool CanUpgradeTower()
        {
            if (!_isOccupied || _currentTower == null) return false;
            return !_currentTower.IsMaxLevel;
        }

        /// <summary>
        /// 绘制Gizmos（编辑器模式下）
        /// </summary>
        private void OnDrawGizmos()
        {
            // 绿色表示可建造，红色表示已占用
            Gizmos.color = _isOccupied ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
    }
}