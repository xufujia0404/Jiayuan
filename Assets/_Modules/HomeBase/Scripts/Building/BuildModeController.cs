using UnityEngine;
using Sttop5.Shared.Player;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 建造模式控制器，管理建造流程：选择建筑→鼠标移动选择位置→点击确认按钮放置。
    /// 帕鲁风格自由建造：鼠标跟随 + 碰撞检测 + 90° 旋转 + UI按钮确认。
    /// </summary>
    public class BuildModeController : MonoBehaviour
    {
        public enum BuildState
        {
            Idle,
            Placing
        }

        [Header("引用")]
        [SerializeField] private PlacementManager _placementManager;
        [SerializeField] private BuildingManager _buildingManager;

        [Header("UI")]
        [SerializeField] private GameObject _buildModeHintPanel;

        [Header("配置")]
        [SerializeField] private float _ghostYOffset = 0.1f;

        private BuildState _state = BuildState.Idle;
        private BuildingData _currentBuilding;
        private GhostPreview _ghost;
        private int _currentRotation = 0;
        private Vector3 _currentWorldPos;
        private bool _canPlace = false;
        private Camera _mainCamera;

        public BuildState State => _state;
        public bool IsPlacing => _state == BuildState.Placing;
        public bool CanPlace => _canPlace;

        public event System.Action<BuildState> OnStateChanged;

        private void Start()
        {
            _mainCamera = Camera.main;
            AutoFindAndBindPanel();
        }

        /// <summary>
        /// 自动查找并绑定建造提示面板和按钮事件。
        /// </summary>
        private void AutoFindAndBindPanel()
        {
            // 如果 Inspector 中已赋值则跳过
            if (_buildModeHintPanel != null)
            {
                BindButtonEvents();
                return;
            }

            // 自动查找"建造模式提示"面板
            var canvas = Camera.main?.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            var panelTransform = canvas.transform.Find("建造模式提示");
            if (panelTransform == null) return;

            _buildModeHintPanel = panelTransform.gameObject;
            Debug.Log("[BuildMode] 自动绑定面板：建造模式提示");

            BindButtonEvents();
        }

        /// <summary>
        /// 绑定面板上的按钮事件。
        /// </summary>
        private void BindButtonEvents()
        {
            if (_buildModeHintPanel == null) return;

            var confirmBtn = _buildModeHintPanel.transform.Find("BtnConfirm")?.GetComponent<UnityEngine.UI.Button>();
            var rotateBtn = _buildModeHintPanel.transform.Find("BtnRotate")?.GetComponent<UnityEngine.UI.Button>();
            var cancelBtn = _buildModeHintPanel.transform.Find("BtnCancel")?.GetComponent<UnityEngine.UI.Button>();

            if (confirmBtn != null)
            {
                confirmBtn.onClick.RemoveAllListeners();
                confirmBtn.onClick.AddListener(ConfirmPlacement);
                Debug.Log("[BuildMode] BtnConfirm 已绑定");
            }
            if (rotateBtn != null)
            {
                rotateBtn.onClick.RemoveAllListeners();
                rotateBtn.onClick.AddListener(RotateBuilding);
                Debug.Log("[BuildMode] BtnRotate 已绑定");
            }
            if (cancelBtn != null)
            {
                cancelBtn.onClick.RemoveAllListeners();
                cancelBtn.onClick.AddListener(CancelBuildMode);
                Debug.Log("[BuildMode] BtnCancel 已绑定");
            }
        }

        private void Update()
        {
            if (_state != BuildState.Placing || _ghost == null) return;

            UpdateGhostPosition();
            HandleInput();
        }

        #region 进入/退出建造模式

        /// <summary>
        /// 进入建造模式，开始放置指定建筑。
        /// </summary>
        public void EnterBuildMode(BuildingData data)
        {
            if (data == null) return;

            if (_state == BuildState.Placing)
                CancelBuildMode();

            _currentBuilding = data;
            _currentRotation = 0;
            _state = BuildState.Placing;

            CreateGhost(data);
            ShowBuildModeHint(true);
            OnStateChanged?.Invoke(_state);

            Debug.Log($"[BuildMode] 进入建造模式: {data.buildingName}");
        }

        /// <summary>
        /// 取消建造模式。
        /// </summary>
        public void CancelBuildMode()
        {
            if (_ghost != null)
            {
                _ghost.Cleanup();
                Destroy(_ghost.gameObject);
                _ghost = null;
            }

            _currentBuilding = null;
            _state = BuildState.Idle;
            _canPlace = false;
            ShowBuildModeHint(false);
            OnStateChanged?.Invoke(_state);

            Debug.Log("[BuildMode] 退出建造模式");
        }

        #endregion

        #region 幽灵预览

        private void CreateGhost(BuildingData data)
        {
            GameObject ghostObj;

            if (data.prefab != null)
            {
                ghostObj = Instantiate(data.prefab);
            }
            else
            {
                ghostObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ghostObj.transform.localScale = new Vector3(data.footprintSize.x, 0.5f, data.footprintSize.y);
                var renderer = ghostObj.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = GetPlaceholderColor(data.buildingType);
                renderer.sharedMaterial = mat;
            }

            ghostObj.name = $"Ghost_{data.buildingName}";
            _ghost = ghostObj.AddComponent<GhostPreview>();
            _ghost.Initialize(data);
        }

        private void UpdateGhostPosition()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null || _placementManager == null) return;

            // 射线检测鼠标在地面的位置
            if (!_placementManager.RaycastGround(_mainCamera, Input.mousePosition, out Vector3 hitPoint))
                return;

            _currentWorldPos = hitPoint;
            _currentWorldPos.y = _ghostYOffset;

            // 更新幽灵位置（自由跟随，不吸附）
            _ghost.UpdatePosition(_currentWorldPos, _currentRotation);

            // 碰撞检测
            Vector3 footprint = GetEffectiveFootprint();
            _canPlace = _placementManager.IsPositionAvailable(_currentWorldPos, footprint, _currentRotation)
                && HasEnoughResources();
            _ghost.SetValid(_canPlace);
        }

        private void HandleInput()
        {
            // 右键或 Esc 取消
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelBuildMode();
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                RotateBuilding();
            }
        }

        /// <summary>
        /// 旋转建筑 90°，可由 UI 按钮或快捷键调用。
        /// </summary>
        public void RotateBuilding()
        {
            _currentRotation = (_currentRotation + 90) % 360;
            Debug.Log($"[BuildMode] 旋转: {_currentRotation}°");
        }

        #endregion

        #region 放置确认

        /// <summary>
        /// 确认放置当前建筑，由 UI 确认按钮调用。
        /// </summary>
        public void ConfirmPlacement()
        {
            if (_currentBuilding == null || _ghost == null || !_canPlace) return;

            if (!HasEnoughResources())
            {
                Debug.LogWarning("[BuildMode] 资源不足");
                return;
            }

            // 放置建筑
            var building = _buildingManager.PlaceBuilding(_currentBuilding, _currentWorldPos);

            if (building != null)
            {
                building.transform.rotation = Quaternion.Euler(0f, _currentRotation, 0f);
                building.SetPlacementInfo(_currentWorldPos, _currentRotation);

                Debug.Log($"[BuildMode] 建筑已放置: {_currentBuilding.buildingName} at {_currentWorldPos}");
            }

            // 清理幽灵，继续放置同类建筑
            if (_ghost != null)
            {
                _ghost.Cleanup();
                Destroy(_ghost.gameObject);
                _ghost = null;
            }
            CreateGhost(_currentBuilding);
        }

        #endregion

        #region 辅助

        /// <summary>
        /// 获取考虑旋转后的有效占地面积。
        /// </summary>
        public Vector3 GetEffectiveFootprint()
        {
            if (_currentBuilding == null) return Vector3.one;

            var size = _currentBuilding.footprintSize;
            if (_currentRotation == 90 || _currentRotation == 270)
                return new Vector3(size.y, 2f, size.x);
            return new Vector3(size.x, 2f, size.y);
        }

        private bool HasEnoughResources()
        {
            if (_currentBuilding == null) return false;
            var levelData = _currentBuilding.GetLevelData(1);
            var profile = PlayerProfile.Instance;
            return profile != null && profile.HasEnoughGold(levelData.buildCost);
        }

        private void ShowBuildModeHint(bool show)
        {
            if (_buildModeHintPanel != null)
                _buildModeHintPanel.SetActive(show);
        }

        private Color GetPlaceholderColor(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.ResourceGen: return new Color(0.2f, 0.8f, 0.3f, 0.6f);
                case BuildingType.Portal: return new Color(0.3f, 0.5f, 1f, 0.6f);
                case BuildingType.Storage: return new Color(0.9f, 0.7f, 0.2f, 0.6f);
                case BuildingType.Decoration: return new Color(0.9f, 0.4f, 0.7f, 0.6f);
                case BuildingType.Special: return new Color(1f, 0.5f, 0.1f, 0.6f);
                default: return new Color(1f, 1f, 1f, 0.6f);
            }
        }

        #endregion
    }
}
