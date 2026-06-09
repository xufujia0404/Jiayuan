using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Data;
using TowerDefense.Enemy;

namespace TowerDefense.Hero
{
    public enum HeroState
    {
        Idle,
        Moving,
        Attacking,
        UsingSkill,
        Dead
    }

    public class Hero : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private HeroData _heroData;

        [Header("Components")]
        [SerializeField] private Animator _animator;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Transform _healthBarAnchor;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _arrivalThreshold = 0.2f;

        private HeroState _currentState = HeroState.Idle;
        private HeroData.HeroStats _currentStats;
        private float _currentHealth;
        private int _currentLevel = 1;
        private float _currentExp = 0f;
        private float _expToNextLevel = 100f;

        private Enemy.Enemy _currentTarget;
        private Vector3 _targetPosition;
        private bool _isMoving = false;
        private float _attackCooldown = 0f;

        private Dictionary<string, float> _skillCooldowns = new Dictionary<string, float>();

        public HeroData Data => _heroData;
        public HeroState State => _currentState;
        public int CurrentLevel => _currentLevel;
        public float CurrentHealth => _currentHealth;
        public float HealthPercent => _currentHealth / _currentStats.maxHealth;
        public float CurrentExp => _currentExp;
        public float ExpPercent => _currentExp / _expToNextLevel;
        public bool IsDead => _currentState == HeroState.Dead;

        public System.Action<Hero> OnLevelUp;
        public System.Action<Hero> OnDeath;
        public System.Action<float> OnHealthChanged;
        public System.Action<float> OnExpChanged;
        public System.Action<string> OnSkillUsed;
        public System.Action<string> OnSkillReady;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        public void Initialize(HeroData data)
        {
            _heroData = data;
            _currentStats = data.baseStats;
            _currentHealth = _currentStats.maxHealth;
            _currentLevel = 1;
            _currentExp = 0f;
            _currentState = HeroState.Idle;

            foreach (var skill in _heroData.skills)
            {
                _skillCooldowns[skill.skillName] = 0f;
            }
        }

        private void Update()
        {
            if (_currentState == HeroState.Dead) return;

            UpdateCooldowns();
            UpdateMovement();
            UpdateAttack();
            UpdateHealthRegen();
        }

        private void UpdateCooldowns()
        {
            List<string> skills = new List<string>(_skillCooldowns.Keys);
            foreach (var skillName in skills)
            {
                if (_skillCooldowns[skillName] > 0)
                {
                    _skillCooldowns[skillName] -= Time.deltaTime;
                    if (_skillCooldowns[skillName] <= 0)
                    {
                        _skillCooldowns[skillName] = 0;
                        OnSkillReady?.Invoke(skillName);
                    }
                }
            }
        }

        private void UpdateMovement()
        {
            if (!_isMoving) return;

            Vector3 direction = (_targetPosition - transform.position).normalized;
            transform.position += direction * _moveSpeed * Time.deltaTime;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = direction.x < 0;
            }

            float distance = Vector3.Distance(transform.position, _targetPosition);
            if (distance <= _arrivalThreshold)
            {
                _isMoving = false;
                _currentState = HeroState.Idle;

                if (_animator != null)
                {
                    _animator.SetBool("IsMoving", false);
                }
            }
        }

        private void UpdateAttack()
        {
            if (_currentState != HeroState.Idle) return;

            if (_currentTarget == null || _currentTarget.IsDead)
            {
                FindTarget();
            }

            if (_currentTarget != null && _attackCooldown <= 0)
            {
                Attack();
            }

            if (_attackCooldown > 0)
            {
                _attackCooldown -= Time.deltaTime;
            }
        }

        private void UpdateHealthRegen()
        {
            if (_currentStats.healthRegen > 0 && _currentHealth < _currentStats.maxHealth)
            {
                _currentHealth += _currentStats.healthRegen * Time.deltaTime;
                if (_currentHealth > _currentStats.maxHealth)
                {
                    _currentHealth = _currentStats.maxHealth;
                }
                OnHealthChanged?.Invoke(_currentHealth);
            }
        }

        public void MoveTo(Vector3 position)
        {
            _targetPosition = position;
            _isMoving = true;
            _currentState = HeroState.Moving;

            if (_animator != null)
            {
                _animator.SetBool("IsMoving", true);
            }
        }

        public void StopMoving()
        {
            _isMoving = false;
            _currentState = HeroState.Idle;

            if (_animator != null)
            {
                _animator.SetBool("IsMoving", false);
            }
        }

        private void FindTarget()
        {
            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            if (spawner == null) return;

            _currentTarget = spawner.GetClosestEnemy(transform.position, _currentStats.attackRange);
        }

        private void Attack()
        {
            if (_currentTarget == null) return;

            _currentState = HeroState.Attacking;
            _attackCooldown = 1f / _currentStats.attackSpeed;

            if (_animator != null)
            {
                _animator.SetTrigger("Attack");
            }

           // 在第211行和第257行添加转换
            _currentTarget.TakeDamage(Mathf.RoundToInt(_currentStats.attackDamage), DamageType.Physical);

            _currentState = HeroState.Idle;
        }

        public bool UseSkill(int skillIndex)
        {
            if (skillIndex < 0 || skillIndex >= _heroData.skills.Length) return false;

            HeroData.HeroSkill skill = _heroData.skills[skillIndex];

            if (_skillCooldowns[skill.skillName] > 0) return false;

            _skillCooldowns[skill.skillName] = skill.cooldown;
            _currentState = HeroState.UsingSkill;

            if (_animator != null)
            {
                _animator.SetTrigger("Skill");
            }

            ExecuteSkill(skill);

            OnSkillUsed?.Invoke(skill.skillName);
            EventBus.Publish(new SkillUsedEvent { SkillName = skill.skillName });

            _currentState = HeroState.Idle;
            return true;
        }

        private void ExecuteSkill(HeroData.HeroSkill skill)
        {
            if (skill.effectPrefab != null)
            {
                Instantiate(skill.effectPrefab, transform.position, Quaternion.identity);
            }

            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            if (spawner == null) return;

            List<Enemy.Enemy> enemiesInRange = spawner.GetEnemiesInRange(transform.position, skill.effectRadius);

            foreach (var enemy in enemiesInRange)
            {
                if (enemy != null && !enemy.IsDead)
                {
                    // 在第211行和第257行添加转换
enemy.TakeDamage(Mathf.RoundToInt(skill.effectValue), DamageType.Magic);
                }
            }
        }

        public void TakeDamage(float damage)
        {
            if (_currentState == HeroState.Dead) return;

            float actualDamage = damage * (1f - _currentStats.armor / 100f);
            _currentHealth -= actualDamage;

            OnHealthChanged?.Invoke(_currentHealth);

            if (_animator != null)
            {
                _animator.SetTrigger("Hit");
            }

            if (_currentHealth <= 0)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            _currentHealth += amount;
            if (_currentHealth > _currentStats.maxHealth)
            {
                _currentHealth = _currentStats.maxHealth;
            }
            OnHealthChanged?.Invoke(_currentHealth);
        }

        public void AddExp(float exp)
        {
            _currentExp += exp;
            OnExpChanged?.Invoke(_currentExp);

            while (_currentExp >= _expToNextLevel)
            {
                LevelUp();
            }
        }

        private void LevelUp()
        {
            _currentExp -= _expToNextLevel;
            _currentLevel++;
            _expToNextLevel *= 1.5f;

            _currentStats.maxHealth *= 1.1f;
            _currentStats.attackDamage *= 1.1f;
            _currentHealth = _currentStats.maxHealth;

            OnLevelUp?.Invoke(this);

            if (_animator != null)
            {
                _animator.SetTrigger("LevelUp");
            }
        }

        private void Die()
        {
            _currentState = HeroState.Dead;
            OnDeath?.Invoke(this);

            if (_animator != null)
            {
                _animator.SetBool("IsDead", true);
            }
        }

        public void Revive()
        {
            _currentHealth = _currentStats.maxHealth;
            _currentState = HeroState.Idle;

            if (_animator != null)
            {
                _animator.SetBool("IsDead", false);
            }
        }

        public float GetSkillCooldown(string skillName)
        {
            return _skillCooldowns.TryGetValue(skillName, out float cd) ? cd : 0f;
        }

        public float GetSkillCooldownPercent(string skillName)
        {
            foreach (var skill in _heroData.skills)
            {
                if (skill.skillName == skillName)
                {
                    return GetSkillCooldown(skillName) / skill.cooldown;
                }
            }
            return 0f;
        }
    }
}
