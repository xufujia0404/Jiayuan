using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Data;
using TowerDefense.Utils;
using TowerDefense.Core;

namespace TowerDefense.Enemy
{
    [RequireComponent(typeof(EnemyMovement), typeof(EnemyHealth))]
    public class Enemy : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private EnemyData _enemyData;
        
        [Header("Health Bar")]
        [SerializeField] private Slider _healthSlider;
        [SerializeField] private Image _healthFillImage;
        [SerializeField] private Color _lowHealthColor = Color.red;
        [SerializeField] private Color _mediumHealthColor = Color.yellow;
        [SerializeField] private Color _highHealthColor = Color.green;
        [SerializeField] private float _healthBarOffsetY = 1.5f;
        
        private EnemyMovement _movement;
        private EnemyHealth _health;
        private Canvas _healthCanvas;
        
        public EnemyData Data => _enemyData;
        public EnemyMovement Movement => _movement;
        public EnemyHealth Health => _health;
        public bool IsDead => _health != null && _health.CurrentHealth <= 0;
        
        private int _birthWaveId = -1;  // 敌人归属的波次ID
        
        // 给 EnemySpawner 调用的
        public void SetBirthWaveId(int waveId)
        {
            _birthWaveId = waveId;
        }
        
        private void Awake()
        {
            _movement = GetComponent<EnemyMovement>();
            _health = GetComponent<EnemyHealth>();
            
            // 获取血条画布
            if (_healthSlider != null)
            {
                _healthCanvas = _healthSlider.GetComponentInParent<Canvas>();
                _healthSlider.value = 1f;
            }
            
            if (_enemyData != null)
            {
                Initialize(_enemyData);
            }
        }
        
        public void Initialize(EnemyData data)
        {
            _enemyData = data;
            
            if (_health != null)
            {
                _health.Initialize(data.stats.maxHealth, data.stats.goldReward);
                _health.OnHealthChanged += UpdateHealthBar;
                _health.OnDeath += OnDeath;
            }
            
            if (_movement != null)
            {
                _movement.SetSpeed(data.stats.moveSpeed);
            }
            
            // 初始化血条
            UpdateHealthBar(data.stats.maxHealth, data.stats.maxHealth);
        }
        
        public void StartMovement(PathCreator pathCreator)
        {
            if (_movement != null)
            {
                _movement.FollowPath(pathCreator);
            }
        }
        
        public void OnReachedEnd()
        {
            // 敌人走到终点也统计为波次敌人不再场
            EnemySpawner.OnEnemyKilled(gameObject, _birthWaveId);
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TakeDamage(_enemyData.stats.lifeDamage);
            }
            EventBus.Publish(new EnemyReachEndEvent { Enemy = gameObject, Damage = _enemyData.stats.lifeDamage });
            Destroy(gameObject);
        }
        
        public void OnDeath()
        {
            // 波次统计-带上GameObject给HashSet去重
            EnemySpawner.OnEnemyKilled(gameObject, _birthWaveId);
            
            // 解绑事件
            if (_health != null)
            {
                _health.OnHealthChanged -= UpdateHealthBar;
                _health.OnDeath -= OnDeath;
            }
            
            // 隐藏血条
            if (_healthCanvas != null)
            {
                _healthCanvas.enabled = false;
            }
            
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddGold(_enemyData.stats.goldReward);
                GameManager.Instance.AddKill();
            }
            EventBus.Publish(new EnemyDeathEvent { Enemy = gameObject, Reward = _enemyData.stats.goldReward });
            Destroy(gameObject);
        }
        
        private void Update()
        {
            // 更新血条位置，始终在敌人头顶
            UpdateHealthBarPosition();
        }
        
        private void UpdateHealthBar(int currentHealth, int maxHealth)
        {
            if (_healthSlider == null) return;
            
            float healthPercent = (float)currentHealth / maxHealth;
            _healthSlider.value = healthPercent;
            
            // 根据血量改变颜色
            UpdateHealthBarColor(healthPercent);
            
            // 当血量低于最大值时显示血条
            if (_healthCanvas != null)
            {
                _healthCanvas.enabled = healthPercent < 1f;
            }
        }
        
        private void UpdateHealthBarColor(float healthPercent)
        {
            if (_healthFillImage == null) return;
            
            if (healthPercent <= 0.3f)
            {
                _healthFillImage.color = _lowHealthColor;
            }
            else if (healthPercent <= 0.6f)
            {
                _healthFillImage.color = _mediumHealthColor;
            }
            else
            {
                _healthFillImage.color = _highHealthColor;
            }
        }
        
        private void UpdateHealthBarPosition()
        {
            if (_healthSlider == null || _healthCanvas == null) return;
            
            // 将世界坐标转换为屏幕坐标
            Vector3 worldPosition = transform.position;
            worldPosition.y += _healthBarOffsetY;
            
            // 如果是世界空间画布，直接设置位置
            if (_healthCanvas.renderMode == RenderMode.WorldSpace)
            {
                _healthSlider.transform.position = worldPosition;
            }
            else
            {
                // 如果是屏幕空间画布，转换坐标
                Vector3 screenPosition = Camera.main.WorldToScreenPoint(worldPosition);
                _healthSlider.transform.position = screenPosition;
            }
        }
        
        private void OnDestroy()
        {
            // 确保解绑事件
            if (_health != null)
            {
                _health.OnHealthChanged -= UpdateHealthBar;
                _health.OnDeath -= OnDeath;
            }
        }
        
        public void TakeDamage(int damage)
        {
            if (_health != null)
            {
                _health.TakeDamage(damage);
            }
        }
        
        public void TakeDamage(int damage, DamageType damageType)
        {
            if (_health != null)
            {
                _health.TakeDamage(damage, damageType);
            }
        }
        
        public void ApplySlow(float slowFactor, float duration)
        {
            if (_movement != null)
            {
                _movement.Slow(slowFactor, duration);
            }
        }
        
        public void ApplyStun(float duration)
        {
            if (_movement != null)
            {
                _movement.Slow(0f, duration);
            }
        }
    }
}