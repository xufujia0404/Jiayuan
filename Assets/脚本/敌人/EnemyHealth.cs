using UnityEngine;
using UnityEngine.UI;
using TowerDefense.Data;
using TowerDefense.UI;

namespace TowerDefense.Enemy
{
    public class EnemyHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private int _maxHealth = 100;
        [SerializeField] private int _goldReward = 10;
        
        [Header("UI Settings")]
        [SerializeField] private GameObject _healthBarPrefab;
        [SerializeField] private Vector3 _healthBarOffset = new Vector3(0, 1, 0);
        
        private int _currentHealth;
        private Slider _healthBarSlider;
        private Transform _healthBarTransform;
        private EnemyData _ownerData;
        
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public float HealthPercentage => (float)_currentHealth / _maxHealth;
        
        public System.Action<int, int> OnHealthChanged;
        public System.Action OnDeath;
        
        private void Awake()
        {
            _currentHealth = _maxHealth;
            
            if (_healthBarPrefab != null)
            {
                CreateHealthBar();
            }
        }
        
        public void Initialize(int maxHealth, int goldReward)
        {
            _maxHealth = maxHealth;
            _currentHealth = maxHealth;
            _goldReward = goldReward;
            
            if (_healthBarSlider != null)
            {
                _healthBarSlider.maxValue = _maxHealth;
                _healthBarSlider.value = _currentHealth;
            }
        }

        public void SetOwnerData(EnemyData data)
        {
            _ownerData = data;
        }

        private float GetResistance(DamageType damageType)
        {
            if (_ownerData == null) return 0f;

            return damageType switch
            {
                DamageType.Physical => _ownerData.stats.physicalResistance,
                DamageType.Magic => _ownerData.stats.magicResistance,
                DamageType.Explosion => _ownerData.stats.explosionResistance,
                DamageType.Fire => Mathf.Min(_ownerData.stats.magicResistance, _ownerData.stats.explosionResistance),
                DamageType.Ice => _ownerData.stats.magicResistance,
                DamageType.Poison => Mathf.Min(_ownerData.stats.physicalResistance, _ownerData.stats.magicResistance),
                _ => 0f
            };
        }
        
        private void CreateHealthBar()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;
            
            GameObject healthBarObj = Instantiate(_healthBarPrefab);
            healthBarObj.transform.SetParent(canvas.transform, false);
            
            _healthBarTransform = healthBarObj.transform;
            _healthBarSlider = healthBarObj.GetComponentInChildren<Slider>();
            
            if (_healthBarSlider != null)
            {
                _healthBarSlider.maxValue = _maxHealth;
                _healthBarSlider.value = _currentHealth;
            }
        }
        
        public void TakeDamage(int damage)
        {
            TakeDamage(damage, DamageType.Physical);
        }
        
        public void TakeDamage(int damage, DamageType damageType)
        {
            // 根据伤害类型计算抗性减免
            float resistance = GetResistance(damageType);
            int actualDamage = Mathf.RoundToInt(damage * (1f - resistance));
            actualDamage = Mathf.Max(1, actualDamage); // 至少造成1点伤害

            // 魔法免疫：魔法伤害归零
            if (damageType == DamageType.Magic && _ownerData != null && _ownerData.stats.isMagicImmune)
            {
                actualDamage = 0;
                DamageText.SpawnImmune(transform.position);
            }

            if (actualDamage > 0)
            {
                DamageText.Spawn(transform.position, actualDamage, damageType);
            }

            _currentHealth -= actualDamage;
            _currentHealth = Mathf.Max(0, _currentHealth);
            
            // 触发血量变化事件
            OnHealthChanged?.Invoke(_currentHealth, _maxHealth);
            
            UpdateHealthBar();
            
            if (_currentHealth <= 0)
            {
                Die();
            }
        }
        
        private void UpdateHealthBar()
        {
            if (_healthBarTransform != null && _healthBarSlider != null)
            {
                _healthBarSlider.value = _currentHealth;
                
                Vector3 worldPos = Camera.main.WorldToScreenPoint(transform.position + _healthBarOffset);
                _healthBarTransform.position = worldPos;
            }
        }
        
        private void Die()
        {
            Destroy(_healthBarTransform?.gameObject);
            // 触发死亡事件
            OnDeath?.Invoke();
            SendMessage("OnDeath", SendMessageOptions.DontRequireReceiver);
        }
        
        private void OnDestroy()
        {
            Destroy(_healthBarTransform?.gameObject);
        }
    }
}