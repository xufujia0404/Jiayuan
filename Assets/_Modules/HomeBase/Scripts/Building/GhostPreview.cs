using UnityEngine;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 建筑预览幽灵，跟随鼠标在网格上移动，显示可放置状态（绿/红）。
    /// 使用材质实例（而非直接改 sharedMaterial）避免污染项目材质文件。
    /// </summary>
    public class GhostPreview : MonoBehaviour
    {
        [Header("材质")]
        [SerializeField] private Material _validMaterial;
        [SerializeField] private Material _invalidMaterial;

        private Renderer[] _renderers;
        /// <summary>每个 renderer 对应的运行时材质实例，不会污染项目文件。</summary>
        private Material[] _ghostMaterials;
        private bool _isShowingValid = true;

        public BuildingData Data { get; private set; }
        public int Rotation { get; private set; }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
        }

        /// <summary>
        /// 用建筑数据初始化幽灵。
        /// </summary>
        public void Initialize(BuildingData data)
        {
            Data = data;

            if (data.prefab != null)
            {
                _renderers = GetComponentsInChildren<Renderer>();
            }

            // 为每个 Renderer 创建独立的材质实例，避免污染原始材质
            _ghostMaterials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _ghostMaterials[i] = new Material(_renderers[i].sharedMaterial);
                _renderers[i].sharedMaterial = _ghostMaterials[i];
            }

            SetTransparency(0.6f);
            SetValid(true);
        }

        /// <summary>
        /// 更新幽灵位置和旋转（自由放置，无网格）。
        /// </summary>
        public void UpdatePosition(Vector3 worldPos, int rotation)
        {
            transform.position = worldPos;
            Rotation = rotation;
            transform.rotation = Quaternion.Euler(0f, rotation, 0f);
        }

        /// <summary>
        /// 设置预览状态：可放置（绿色）或不可放置（红色）。
        /// </summary>
        public void SetValid(bool isValid)
        {
            if (_isShowingValid == isValid) return;
            _isShowingValid = isValid;

            Material template = isValid ? _validMaterial : _invalidMaterial;
            if (template != null)
            {
                foreach (var r in _renderers)
                {
                    var inst = new Material(template);
                    r.sharedMaterial = inst;
                }
            }
            else
            {
                foreach (var r in _renderers)
                {
                    if (r.sharedMaterial != null)
                    {
                        var c = r.sharedMaterial.color;
                        r.sharedMaterial.color = isValid
                            ? new Color(0.5f, 1f, 0.5f, 0.6f)
                            : new Color(1f, 0.3f, 0.3f, 0.6f);
                    }
                }
            }
        }

        /// <summary>
        /// 清理幽灵，销毁运行时创建的材质实例。
        /// </summary>
        public void Cleanup()
        {
            if (_ghostMaterials != null)
            {
                foreach (var mat in _ghostMaterials)
                {
                    if (mat != null) Destroy(mat);
                }
            }
        }

        private void SetTransparency(float alpha)
        {
            foreach (var r in _renderers)
            {
                if (r.sharedMaterial != null)
                {
                    var c = r.sharedMaterial.color;
                    r.sharedMaterial.color = new Color(c.r, c.g, c.b, alpha);
                }
            }
        }

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
