using UnityEngine;
using UnityEngine.UI;

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
            _currentHealth -= damage;
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