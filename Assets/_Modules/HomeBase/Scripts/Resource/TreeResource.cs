using UnityEngine;
using TowerDefense.Core;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 挂在可砍伐的树上，管理血量、木头奖励和被砍倒效果。
    /// </summary>
    public class TreeResource : MonoBehaviour
    {
        [Header("属性")]
        [SerializeField] private float _maxHealth = 30f;
        [SerializeField] private int _woodReward = 10;
        [SerializeField] private int _goldReward = 5;

        [Header("效果")]
        [SerializeField] private float _shrinkDuration = 0.5f;
        [SerializeField] private GameObject _stumpPrefab;

        private float _currentHealth;
        private bool _isDead;

        public float HealthPercent => _currentHealth / _maxHealth;
        public bool IsDead => _isDead;
        public Vector3 ChopPosition => transform.position;

        public System.Action<TreeResource> OnTreeChopped;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        /// <summary>砍一斧，返回是否还活着</summary>
        public bool TakeChopDamage(float damage)
        {
            if (_isDead) return false;

            _currentHealth -= damage;

            if (_currentHealth <= 0)
            {
                Die();
                return false;
            }

            // 被砍时轻微摇晃
            StartCoroutine(ShakeRoutine());
            return true;
        }

        private void Die()
        {
            _isDead = true;
            OnTreeChopped?.Invoke(this);

            // 发放奖励
            EventBus.Publish(new TreeChoppedEvent
            {
                TreePosition = transform.position,
                WoodAmount = _woodReward,
                GoldAmount = _goldReward
            });

            // 缩小消失
            StartCoroutine(ShrinkAndDestroy());
        }

        private System.Collections.IEnumerator ShakeRoutine()
        {
            Vector3 original = transform.position;
            float elapsed = 0f;
            while (elapsed < 0.15f)
            {
                float offsetX = Mathf.Sin(elapsed * 80f) * 0.03f;
                transform.position = original + new Vector3(offsetX, 0, 0);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = original;
        }

        private System.Collections.IEnumerator ShrinkAndDestroy()
        {
            Vector3 originalScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < _shrinkDuration)
            {
                float t = elapsed / _shrinkDuration;
                transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 留下树桩
            if (_stumpPrefab != null)
            {
                Instantiate(_stumpPrefab, transform.position, transform.rotation);
            }

            Destroy(gameObject);
        }
    }
}
