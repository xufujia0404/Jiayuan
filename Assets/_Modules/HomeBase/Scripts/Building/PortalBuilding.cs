using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 传送门建筑，点击后切换到目标场景。
    /// </summary>
    public class PortalBuilding : BuildingBase
    {
        [Header("传送门特效")]
        [SerializeField] private Color _portalColor = new Color(0.3f, 0.5f, 1f, 0.8f);
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _pulseAmount = 0.1f;

        private SpriteRenderer _renderer;
        private float _timer;
        private bool _isHovered = false;
        private Vector3 _baseScale;

        public string TargetModuleId => _data != null ? _data.targetModuleId : "";
        public string TargetSceneName => _data != null ? _data.targetSceneName : "";

        protected override void OnUpgraded()
        {
        }

        private void Start()
        {
            if (_data != null && !_isPlaced)
            {
                _isPlaced = true;
            }

            _renderer = GetComponent<SpriteRenderer>();
            _baseScale = transform.localScale;
            if (_renderer != null)
            {
                _renderer.color = _portalColor;
            }
        }

        private new void Update()
        {
            if (!_isPlaced && _data != null)
            {
                _isPlaced = true;
            }

            _timer += Time.deltaTime;

            if (_renderer != null)
            {
                float pulse = 1f + Mathf.Sin(_timer * _pulseSpeed) * _pulseAmount;
                transform.localScale = _baseScale * pulse;
            }
        }

        /// <summary>
        /// 点击传送门，切换到目标场景。
        /// </summary>
        public void OnPortalClicked()
        {
            string sceneName = TargetSceneName;
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning("[PortalBuilding] 未配置目标场景");
                return;
            }

            Debug.Log($"[PortalBuilding] 进入传送门: {_data.buildingName} → 场景 {sceneName}");
            SceneManager.LoadScene(sceneName);
        }

        private void OnMouseDown()
        {
            if (!_isPlaced) return;
            OnPortalClicked();
        }

        private void OnMouseEnter()
        {
            _isHovered = true;
            if (_renderer != null)
                _renderer.color = new Color(_portalColor.r, _portalColor.g, _portalColor.b, 1f);
        }

        private void OnMouseExit()
        {
            _isHovered = false;
            if (_renderer != null)
                _renderer.color = _portalColor;
        }
    }
}
