using UnityEngine;
using TowerDefense.Data;
using TowerDefense.Core;

namespace TowerDefense.Tower
{
    /// <summary>
    /// 塔放置预览：选塔后在槽位上显示半透明塔+范围圈，确认后放置。
    /// </summary>
    public class TowerPlacementPreview : MonoBehaviour
    {
        private static TowerPlacementPreview _instance;
        public static TowerPlacementPreview Instance => _instance;

        [Header("Preview Settings")]
        [SerializeField] private Color _rangeColor = new Color(0.3f, 0.6f, 1f, 1f);
        [SerializeField] private Color _rangeInvalidColor = new Color(1f, 0.3f, 0.3f, 1f);
        [SerializeField] private float _previewAlpha = 0.5f;

        private GameObject _previewObject;
        private SpriteRenderer[] _previewRenderers;
        private GameObject _rangeCircle;
        private TowerData _previewData;
        private TowerSlot _targetSlot;
        private bool _isActive = false;

        public bool IsActive => _isActive;
        public TowerData PreviewData => _previewData;
        public TowerSlot TargetSlot => _targetSlot;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (!_isActive) return;

            // 右键或 Escape 取消预览
            if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                CancelPreview();
                return;
            }

            // 左键确认放置
            if (Input.GetMouseButtonDown(0))
            {
                ConfirmPlacement();
            }
        }

        /// <summary>
        /// 显示指定塔在指定槽位的放置预览。
        /// </summary>
        public void ShowPreview(TowerData towerData, TowerSlot slot)
        {
            if (towerData == null || slot == null) return;

            // 如果已经在预览同一个塔和槽位，直接确认
            if (_isActive && _previewData == towerData && _targetSlot == slot)
            {
                ConfirmPlacement();
                return;
            }

            ClearPreview();

            _previewData = towerData;
            _targetSlot = slot;
            _isActive = true;

            // 创建半透明塔预览
            CreatePreviewObject(towerData, slot);

            // 创建范围圈
            CreateRangeCircle(towerData, slot);

            // 刷新金币事件监听
            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
            EventBus.Subscribe<GoldChangedEvent>(OnGoldChanged);
        }

        /// <summary>
        /// 取消预览。
        /// </summary>
        public void CancelPreview()
        {
            ClearPreview();

            // 重新显示塔选择面板
            if (_targetSlot != null)
            {
                _targetSlot.SelectSlot();
            }
        }

        private void ConfirmPlacement()
        {
            if (_previewData == null || _targetSlot == null)
            {
                ClearPreview();
                return;
            }

            TowerData data = _previewData;
            TowerSlot slot = _targetSlot;

            ClearPreview();

            // 执行放置
            bool success = slot.PlaceTower(data);
            if (!success)
            {
                // 放置失败（金币不足等），重新显示选择面板
                slot.SelectSlot();
            }
        }

        private void ClearPreview()
        {
            _isActive = false;
            _previewData = null;
            _targetSlot = null;

            if (_previewObject != null)
            {
                Destroy(_previewObject);
                _previewObject = null;
            }

            if (_rangeCircle != null)
            {
                Destroy(_rangeCircle);
                _rangeCircle = null;
            }

            _previewRenderers = null;

            EventBus.Unsubscribe<GoldChangedEvent>(OnGoldChanged);
        }

        private void CreatePreviewObject(TowerData towerData, TowerSlot slot)
        {
            if (towerData.towerPrefab == null) return;

            Vector3 spawnPos = slot.transform.position;
            spawnPos.z = -1f;

            _previewObject = Instantiate(towerData.towerPrefab, spawnPos, Quaternion.identity);
            _previewObject.name = $"[Preview] {towerData.towerName}";

            // 移除所有脚本组件，预览不需要逻辑
            foreach (var comp in _previewObject.GetComponentsInChildren<MonoBehaviour>())
            {
                Destroy(comp);
            }

            // 移除碰撞体，防止干扰点击
            foreach (var col in _previewObject.GetComponentsInChildren<Collider2D>())
            {
                Destroy(col);
            }

            // 设置半透明
            _previewRenderers = _previewObject.GetComponentsInChildren<SpriteRenderer>();
            SetAlpha(_previewAlpha);
        }

        private void CreateRangeCircle(TowerData towerData, TowerSlot slot)
        {
            float range = towerData.levels[0].attackRange;
            if (range <= 0) return;

            _rangeCircle = new GameObject("[Preview] RangeCircle");
            _rangeCircle.transform.position = slot.transform.position;
            _rangeCircle.transform.SetParent(transform);

            // 外圈光晕（粗、半透明）
            var glow = _rangeCircle.AddComponent<LineRenderer>();
            glow.startWidth = 0.3f;
            glow.endWidth = 0.3f;
            glow.useWorldSpace = false;
            glow.loop = true;
            glow.sortingOrder = 99;
            glow.positionCount = 64;
            var glowColor = GetRangeColor();
            glowColor.a = 0.3f;
            glow.startColor = glowColor;
            glow.endColor = glowColor;
            glow.material = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < 64; i++)
            {
                float angle = i / 64f * Mathf.PI * 2f;
                glow.SetPosition(i, new Vector3(Mathf.Cos(angle) * range, Mathf.Sin(angle) * range, 0));
            }

            // 内圈实线（细、不透明）
            var line = _rangeCircle.AddComponent<LineRenderer>();
            line.startWidth = 0.15f;
            line.endWidth = 0.15f;
            line.useWorldSpace = false;
            line.loop = true;
            line.sortingOrder = 100;
            line.positionCount = 64;
            var lineColor = GetRangeColor();
            lineColor.a = 0.9f;
            line.startColor = lineColor;
            line.endColor = lineColor;
            line.material = new Material(Shader.Find("Sprites/Default"));
            for (int i = 0; i < 64; i++)
            {
                float angle = i / 64f * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * range, Mathf.Sin(angle) * range, 0));
            }
        }

        private Color GetRangeColor()
        {
            if (_previewData == null || GameManager.Instance == null) return _rangeColor;
            bool canAfford = GameManager.Instance.HasEnoughGold(_previewData.levels[0].cost);
            return canAfford ? _rangeColor : _rangeInvalidColor;
        }

        private void SetAlpha(float alpha)
        {
            if (_previewRenderers == null) return;
            foreach (var sr in _previewRenderers)
            {
                if (sr == null) continue;
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
        }

        private void OnGoldChanged(GoldChangedEvent e)
        {
            if (_rangeCircle != null)
            {
                var lrs = _rangeCircle.GetComponents<LineRenderer>();
                var baseColor = GetRangeColor();
                for (int i = 0; i < lrs.Length; i++)
                {
                    var c = baseColor;
                    c.a = i == 0 ? 0.3f : 0.9f; // 第一条=光晕, 第二条=实线
                    lrs[i].startColor = c;
                    lrs[i].endColor = c;
                }
            }
        }

    }
}
