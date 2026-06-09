using UnityEngine;

namespace TowerDefense.Enemy
{
    public class EnemyAnimation : MonoBehaviour
    {
        [Header("Animator")]
        [SerializeField] private Animator _animator;
        
        [Header("Animator Parameter Names")]
        [SerializeField] private string _speedParam = "Speed";
        [SerializeField] private string _getHitTrigger = "GetHit";
        [SerializeField] private string _deathTrigger = "Death";
        [SerializeField] private string _isMovingParam = "IsMoving";
        
        private Enemy _enemy;
        private EnemyHealth _health;
        private EnemyMovement _movement;
        
        private int _speedHash;
        private int _getHitHash;
        private int _deathHash;
        private int _isMovingHash;
        
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _health = GetComponent<EnemyHealth>();
            _movement = GetComponent<EnemyMovement>();
            
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }
            
            // 计算参数哈希
            _speedHash = Animator.StringToHash(_speedParam);
            _getHitHash = Animator.StringToHash(_getHitTrigger);
            _deathHash = Animator.StringToHash(_deathTrigger);
            _isMovingHash = Animator.StringToHash(_isMovingParam);
        }
        
        private void OnEnable()
        {
            if (_health != null)
            {
                _health.OnHealthChanged += OnHealthChanged;
                _health.OnDeath += OnDeath;
            }
        }
        
        private void OnDisable()
        {
            if (_health != null)
            {
                _health.OnHealthChanged -= OnHealthChanged;
                _health.OnDeath -= OnDeath;
            }
        }
        
        private void Update()
        {
            UpdateMovementAnimation();
        }
        
        private void UpdateMovementAnimation()
        {
            if (_animator == null || _movement == null) return;
            if (_health != null && _health.CurrentHealth <= 0) return;
            
            bool isMoving = _movement.IsMoving;
            
            // 设置移动参数
            _animator.SetBool(_isMovingHash, isMoving);
            _animator.SetFloat(_speedHash, isMoving ? 1f : 0f);
        }
        
        private void OnHealthChanged(int currentHealth, int maxHealth)
        {
            if (currentHealth < maxHealth && currentHealth > 0)
            {
                PlayGetHitAnimation();
            }
        }
        
        private void OnDeath()
        {
            PlayDeathAnimation();
        }
        
        public void PlayGetHitAnimation()
        {
            if (_animator == null) return;
            if (_health != null && _health.CurrentHealth <= 0) return;
            
            // 触发受击动画
            _animator.SetTrigger(_getHitHash);
        }
        
        public void PlayDeathAnimation()
        {
            if (_animator == null) return;
            
            // 触发死亡动画
            _animator.SetTrigger(_deathHash);
            
            // 确保死亡时不再移动
            if (_movement != null)
            {
                _movement.StopMovement();
            }
        }
    }
}
