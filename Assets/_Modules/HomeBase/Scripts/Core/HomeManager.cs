using System;
using UnityEngine;
using Sttop5.Shared.Core;
using Sttop5.Shared.Save;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 家园主控制器，管理家园场景的初始化和生命周期。
    /// 作为 GameModule 注册到 ModuleManager。
    /// </summary>
    public class HomeManager : GameModule
    {
        private const string MODULE_ID = "home";
        private const string MODULE_DISPLAY_NAME = "家园";
        private const string SAVE_KEY = "homebase";

        [Header("组件引用")]
        [SerializeField] private BuildingManager _buildingManager;
        [SerializeField] private ResourceManager _resourceManager;
        [SerializeField] private BuildingInfoPanel _buildingInfoPanel;

        [Header("初始建筑")]
        [SerializeField] private BuildingData[] _starterBuildings;
        [SerializeField] private Vector3[] _starterPositions;

        private bool _isLoaded = false;

        private void Awake()
        {
            _moduleId = MODULE_ID;
            _displayName = MODULE_DISPLAY_NAME;
            _sceneName = gameObject.scene.name;
        }

        private void Start()
        {
            InitializeHome();
        }

        private void InitializeHome()
        {
            if (_isLoaded) return;

            // 注册到 ModuleManager
            var moduleManager = ModuleManager.Instance;
            if (moduleManager != null)
            {
                moduleManager.RegisterModule(this);
            }

            // 尝试从存档加载
            LoadHomeSave();

            // 如果没有建筑，放置初始建筑
            if (_buildingManager.Buildings.Count == 0)
            {
                PlaceStarterBuildings();
            }

            // 注册建筑点击事件
            foreach (var building in _buildingManager.Buildings)
            {
                if (building != null)
                    building.OnBuildingClicked += OnBuildingClicked;
            }

            // 新放置的建筑也自动注册
            _buildingManager.OnBuildingPlaced += (b) =>
            {
                if (b != null) b.OnBuildingClicked += OnBuildingClicked;
            };

            _isLoaded = true;
            Debug.Log("[HomeManager] 家园初始化完成");
        }

        private void PlaceStarterBuildings()
        {
            if (_starterBuildings == null || _starterPositions == null) return;

            int count = Mathf.Min(_starterBuildings.Length, _starterPositions.Length);
            for (int i = 0; i < count; i++)
            {
                if (_starterBuildings[i] != null)
                {
                    _buildingManager.PlaceBuilding(_starterBuildings[i], _starterPositions[i]);
                }
            }

            Debug.Log($"[HomeManager] 放置 {count} 个初始建筑");
        }

        #region 存档

        public override string GetModuleSaveData()
        {
            var saveData = new HomeSaveData();
            _resourceManager?.SaveToSave(saveData);
            saveData.buildings = _buildingManager?.ExportSaveData();
            return JsonUtility.ToJson(saveData);
        }

        public override void RestoreModuleSaveData(string jsonData)
        {
            if (string.IsNullOrEmpty(jsonData)) return;

            try
            {
                var saveData = JsonUtility.FromJson<HomeSaveData>(jsonData);
                _resourceManager?.LoadFromSave(saveData);
                _buildingManager?.RestoreFromSaveData(saveData.buildings);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HomeManager] 恢复存档失败: {e.Message}");
            }
        }

        private void LoadHomeSave()
        {
            var saveSystem = SaveSystem.Instance;
            if (saveSystem == null) return;

            string json = saveSystem.LoadModuleData(SAVE_KEY);
            if (!string.IsNullOrEmpty(json))
            {
                RestoreModuleSaveData(json);
                Debug.Log("[HomeManager] 家园存档已加载");
            }
        }

        private void SaveHomeData()
        {
            var saveSystem = SaveSystem.Instance;
            if (saveSystem == null) return;

            string json = GetModuleSaveData();
            saveSystem.SaveModuleData(SAVE_KEY, json, 1);
        }

        #endregion

        #region 模块生命周期

        public override void Activate()
        {
            base.Activate();
            Debug.Log("[HomeManager] 家园已激活");
        }

        public override void Deactivate()
        {
            SaveHomeData();
            base.Deactivate();
            Debug.Log("[HomeManager] 家园已停用");
        }

        #endregion

        #region 按钮回调

        /// <summary>
        /// 收集所有资源按钮回调。
        /// </summary>
        public void OnCollectAllClicked()
        {
            _resourceManager?.CollectAllResources();
        }

        /// <summary>
        /// 建筑被点击时的回调，打开建筑信息弹窗。
        /// </summary>
        private void OnBuildingClicked(BuildingBase building)
        {
            if (_buildingInfoPanel != null)
                _buildingInfoPanel.Show(building);
        }

        #endregion

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) SaveHomeData();
        }

        private void OnApplicationQuit()
        {
            SaveHomeData();
        }
    }
}
