using UnityEngine;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 传送门特效控制器，管理光晕脉冲和粒子强度。
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class PortalEffectController : MonoBehaviour
    {
        [Header("脉冲配置")]
        [SerializeField] private float _pulseSpeed = 2f;
        [SerializeField] private float _minIntensity = 1f;
        [SerializeField] private float _maxIntensity = 3f;
        [SerializeField] private float _minRange = 1.5f;
        [SerializeField] private float _maxRange = 2.5f;

        private Light _light;
        private float _timer;

        private void Start()
        {
            _light = GetComponent<Light>();
        }

        private void Update()
        {
            if (_light == null) return;

            _timer += Time.deltaTime;
            float t = (Mathf.Sin(_timer * _pulseSpeed) + 1f) * 0.5f; // 0~1
            _light.intensity = Mathf.Lerp(_minIntensity, _maxIntensity, t);
            _light.range = Mathf.Lerp(_minRange, _maxRange, t);
        }
    }
}
