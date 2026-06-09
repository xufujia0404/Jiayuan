using UnityEngine;
using Sttop5.Shared.Core;
using Sttop5.Shared.Player;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 建筑基类，所有建筑类型继承此类。
    /// 管理建筑的等级、升级和拆除。
    /// </summary>
    public class BuildingBase : MonoBehaviour
    {
        [Header("建筑配置")]
        [SerializeField] protected BuildingData _data;

        protected int _currentLevel = 1;
        protected bool _isPlaced = false;
        protected float _productionTimer;
        protected int _rotation;

        /// <summary>建筑被点击时触发，参数为被点击的建筑实例。</summary>
        public event System.Action<BuildingBase> OnBuildingClicked;

        #region 属性

        public BuildingData Data => _data;
        public int CurrentLevel => _currentLevel;
        public bool IsPlaced => _isPlaced;
        public bool IsMaxLevel => _currentLevel >= _data.MaxLevel;
        public BuildingLevel CurrentLevelData => _data.GetLevelData(_currentLevel);
        public Vector3 WorldPosition => transform.position;
        public int Rotation => _rotation;

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化建筑。
        /// </summary>
        public virtual void Initialize(BuildingData data)
        {
            _data = data;
            _currentLevel = 1;
            _isPlaced = true;
            _productionTimer = 0f;
        }

        /// <summary>
        /// 从存档恢复建筑状态。
        /// </summary>
        public virtual void RestoreFromSave(BuildingData data, int level)
        {
            _data = data;
            _currentLevel = Mathf.Clamp(level, 1, data.MaxLevel);
            _isPlaced = true;
            _productionTimer = 0f;
        }

        /// <summary>
        /// 设置建筑的放置位置和旋转（自由建造）。
        /// </summary>
        public void SetPlacementInfo(Vector3 worldPos, int rotation)
        {
            _rotation = rotation;
        }

        #endregion

        #region 升级与拆除

        /// <summary>
        /// 升级建筑。
        /// </summary>
        public virtual bool Upgrade()
        {
            if (IsMaxLevel)
            {
                Debug.LogWarning($"[BuildingBase] 建筑已满级: {_data.buildingName}");
                return false;
            }

            var levelData = _data.GetLevelData(_currentLevel + 1);
            var profile = PlayerProfile.Instance;
            if (profile == null || !profile.HasEnoughGold(levelData.upgradeCost))
            {
                Debug.LogWarning($"[BuildingBase] 金币不足，无法升级: {_data.buildingName}");
                return false;
            }

            profile.SpendGold(levelData.upgradeCost, "building_upgrade");
            _currentLevel++;
            OnUpgraded();
            Debug.Log($"[BuildingBase] 建筑升级: {_data.buildingName} Lv.{_currentLevel}");
            return true;
        }

        /// <summary>
        /// 拆除建筑，返还部分资源。
        /// </summary>
        public virtual int Demolish()
        {
            int refund = GetSellValue();
            var profile = PlayerProfile.Instance;
            profile?.AddGold(refund, "building_demolish");
            _isPlaced = false;
            Debug.Log($"[BuildingBase] 建筑拆除: {_data.buildingName}, 返还 {refund} 金币");
            Destroy(gameObject);
            return refund;
        }

        /// <summary>
        /// 获取出售价值（建造费用的 60%）。
        /// </summary>
        public int GetSellValue()
        {
            int totalCost = 0;
            for (int i = 1; i <= _currentLevel; i++)
            {
                var ld = _data.GetLevelData(i);
                totalCost += i == 1 ? ld.buildCost : ld.upgradeCost;
            }
            return Mathf.RoundToInt(totalCost * 0.6f);
        }

        #endregion

        #region 子类重写

        /// <summary>升级后的回调。</summary>
        protected virtual void OnUpgraded() { }

        /// <summary>每帧更新（用于资源产出等）。</summary>
        protected virtual void BuildingUpdate() { }

        #endregion

        private void OnMouseDown()
        {
            if (!_isPlaced) return;
            OnBuildingClicked?.Invoke(this);
        }

        private void Update()
        {
            // 场景中预放置的建筑：如果已有 _data 但 _isPlaced 为 false，自动标记
            if (!_isPlaced && _data != null)
            {
                _isPlaced = true;
            }

            if (!_isPlaced) return;
            BuildingUpdate();
        }
    }
}
