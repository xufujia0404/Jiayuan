using System.Collections;
using UnityEngine;
using TowerDefense.Data;
using TowerDefense.Enemy;
using TowerDefense.Utils;

namespace TowerDefense.Tower
{
    public enum SoldierState
    {
        Idle,
        MovingToEnemy,
        Attacking,
        Returning
    }

    public class Soldier : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Animator _animator;

        private static readonly int RunHash = Animator.StringToHash("Run");
        private static readonly int IdleHash = Animator.StringToHash("Idle");

        private SoldierData _data;
        private Transform _homePoint;
        private int _currentHealth;
        private Enemy.Enemy _target;
        private SoldierState _state = SoldierState.Idle;
        private float _attackCooldown = 0f;
        private float _enemyAttackCooldown = 0f;
        private bool _isAlive = true;
        private bool _isBlocking = false;
        private float _healTimer = 0f;
        private float _healInterval = 1f;

        public bool IsAlive => _isAlive;
        public bool IsBlocking => _isBlocking;
        public SoldierState State => _state;
        public Enemy.Enemy CurrentTarget => _target;
        public int CurrentHealth => _currentHealth;
        public float HealthPercent => _data != null ? (float)_currentHealth / _data.maxHealth : 0f;

        public System.Action<Soldier> OnSoldierDeath;
        public System.Action<Soldier> OnSoldierReturned;

        public void Initialize(SoldierData data, Transform homePoint)
        {
            _data = data;
            _homePoint = homePoint;
            _currentHealth = data.maxHealth;
            _isAlive = true;
            _isBlocking = false;
            _state = SoldierState.Idle;
            _target = null;
            _attackCooldown = 0f;
            _healTimer = 0f;

            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_animator == null)
                _animator = GetComponent<Animator>();

            PlayIdleAnimation();
            gameObject.SetActive(true);
        }

        public void SetRallyPoint(Transform rallyPoint)
        {
            _homePoint = rallyPoint;
        }

        private void Update()
        {
            if (!_isAlive || _data == null) return;

            switch (_state)
            {
                case SoldierState.Idle:
                    FindEnemy();
                    HealOutOfCombat();
                    break;
                case SoldierState.MovingToEnemy:
                    MoveTowardsEnemy();
                    break;
                case SoldierState.Attacking:
                    AttackEnemy();
                    break;
                case SoldierState.Returning:
                    ReturnToRallyPoint();
                    break;
            }
        }

        private void FindEnemy()
        {
            Vector3 searchOrigin = _homePoint != null ? _homePoint.position : transform.position;

            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            if (spawner == null) return;

            var enemies = spawner.GetEnemiesInRange(searchOrigin, _data.engageRange);
            if (enemies.Count > 0)
            {
                float closestDist = float.MaxValue;
                Enemy.Enemy closest = null;
                foreach (var e in enemies)
                {
                    if (e == null || e.IsDead) continue;
                    // 士兵无法攻击飞行敌人
                    if (e.Data != null && e.Data.stats.isFlying) continue;
                    float dist = Vector2.Distance(transform.position, e.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = e;
                    }
                }

                if (closest != null)
                {
                    _target = closest;
                    _state = SoldierState.MovingToEnemy;
                    PlayRunAnimation();
                }
            }
        }

        private void MoveTowardsEnemy()
        {
            if (_target == null || _target.IsDead || _target.gameObject == null)
            {
                ReleaseTarget();
                _state = SoldierState.Returning;
                return;
            }

            Vector2 currentPos = transform.position;
            float dist = Vector2.Distance(currentPos, (Vector2)_target.transform.position);
            if (dist <= _data.attackRange)
            {
                _state = SoldierState.Attacking;
                BlockEnemy();
                return;
            }

            if (_homePoint != null)
            {
                float distFromHome = Vector2.Distance((Vector2)_target.transform.position, (Vector2)_homePoint.position);
                if (distFromHome > _data.engageRange * 1.5f)
                {
                    ReleaseTarget();
                    _state = SoldierState.Returning;
                    return;
                }
            }

            Vector2 direction = ((Vector2)_target.transform.position - currentPos).normalized;
            transform.position += (Vector3)(direction * _data.moveSpeed * Time.deltaTime);

            if (_spriteRenderer != null)
                _spriteRenderer.flipX = direction.x < 0;
        }

        private void AttackEnemy()
        {
            if (_target == null || _target.IsDead || _target.gameObject == null)
            {
                ReleaseTarget();
                _state = SoldierState.Returning;
                return;
            }

            float dist = Vector2.Distance(transform.position, _target.transform.position);
            if (dist > _data.attackRange * 2f)
            {
                ReleaseTarget();
                _state = SoldierState.MovingToEnemy;
                return;
            }

            _attackCooldown -= Time.deltaTime;
            if (_attackCooldown <= 0f)
            {
                _target.TakeDamage(_data.damage, DamageType.Physical);
                _attackCooldown = 1f / _data.attackSpeed;
            }

            EnemyAttacksSoldier();
        }

        private void EnemyAttacksSoldier()
        {
            if (_target == null || _target.IsDead) return;

            var enemyData = _target.Data;
            if (enemyData == null) return;

            _enemyAttackCooldown -= Time.deltaTime;
            if (_enemyAttackCooldown <= 0f)
            {
                int enemyDamage = enemyData.stats.attackDamage;
                if (enemyDamage <= 0)
                    enemyDamage = Mathf.Max(1, Mathf.CeilToInt(enemyData.stats.maxHealth * 0.05f));
                TakeDamage(enemyDamage);
                _enemyAttackCooldown = 1f;
            }
        }

        private void BlockEnemy()
        {
            if (_target == null || _isBlocking) return;

            // 飞行敌人无法被拦截
            if (_target.Data != null && _target.Data.stats.isFlying)
            {
                ReleaseTarget();
                _state = SoldierState.Returning;
                return;
            }

            _isBlocking = true;
            var movement = _target.GetComponent<EnemyMovement>();
            if (movement != null)
            {
                movement.StopMovement();
            }
        }

        private void ReleaseTarget()
        {
            if (_isBlocking && _target != null)
            {
                var movement = _target.GetComponent<EnemyMovement>();
                if (movement != null && _target.gameObject != null && !_target.IsDead)
                {
                    movement.ResumeMovement();
                }
            }

            _isBlocking = false;
            _target = null;
        }

        private void ReturnToRallyPoint()
        {
            if (_homePoint == null)
            {
                _state = SoldierState.Idle;
                return;
            }

            float dist = Vector2.Distance(transform.position, _homePoint.position);
            if (dist <= 0.3f)
            {
                _state = SoldierState.Idle;
                PlayIdleAnimation();
                OnSoldierReturned?.Invoke(this);
                return;
            }

            Vector2 direction = ((Vector2)_homePoint.position - (Vector2)transform.position).normalized;
            transform.position += (Vector3)(direction * _data.returnSpeed * Time.deltaTime);

            if (_spriteRenderer != null)
                _spriteRenderer.flipX = direction.x < 0;
        }

        private void HealOutOfCombat()
        {
            if (_data == null || _currentHealth >= _data.maxHealth) return;

            _healTimer += Time.deltaTime;
            if (_healTimer >= _healInterval)
            {
                _healTimer = 0f;
                _currentHealth = Mathf.Min(_currentHealth + 1, _data.maxHealth);
            }
        }

        public void TakeDamage(int damage)
        {
            if (!_isAlive) return;

            _currentHealth -= damage;
            if (_currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            _isAlive = false;
            ReleaseTarget();
            _state = SoldierState.Idle;
            OnSoldierDeath?.Invoke(this);
            gameObject.SetActive(false);
        }

        public void Respawn(Vector3 position)
        {
            _currentHealth = _data.maxHealth;
            _isAlive = true;
            _isBlocking = false;
            _state = SoldierState.Idle;
            _target = null;
            _attackCooldown = 0f;
            _enemyAttackCooldown = 0f;
            _healTimer = 0f;

            transform.SetParent(null);
            transform.position = position;

            PlayIdleAnimation();
            gameObject.SetActive(true);
        }

        public void EngageTarget(Enemy.Enemy target)
        {
            if (!_isAlive || target == null || target.IsDead) return;
            _target = target;
            _state = SoldierState.MovingToEnemy;
            PlayRunAnimation();
        }

        private void OnDestroy()
        {
            if (_isBlocking && _target != null)
            {
                var movement = _target.GetComponent<EnemyMovement>();
                if (movement != null && _target.gameObject != null && !_target.IsDead)
                {
                    movement.ResumeMovement();
                }
            }
        }

        private void PlayIdleAnimation()
        {
            if (_animator != null)
            {
                _animator.SetBool(RunHash, false);
                _animator.SetBool(IdleHash, true);
            }
        }

        private void PlayRunAnimation()
        {
            if (_animator != null)
            {
                _animator.SetBool(IdleHash, false);
                _animator.SetBool(RunHash, true);
            }
        }
    }
}
