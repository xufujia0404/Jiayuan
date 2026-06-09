using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Enemy;

namespace TowerDefense.Tower
{
    public class Projectile : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _speed = 10f;
        [SerializeField] private float _homingStrength = 5f;

        [Header("Pierce Settings (Sword Tower)")]
        [SerializeField] private int _maxPierceCount = 3;
        [SerializeField] private float _maxLifetime = 3f;
        [SerializeField] private float _hitCooldown = 0.2f;

        [Header("Impact Settings")]
        [SerializeField] private GameObject _impactEffect;
        [SerializeField] private float _impactDuration = 1f;

        [Header("Debug")]
        [SerializeField] private bool _showDebugLogs = false;

        private Enemy.Enemy _target;
        private float _damage;
        private DamageType _damageType;
        private float _splashRadius = 0f;
        private int _maxSplashTargets = 0;
        private bool _hasHit = false;

        private bool _isPierceMode = false;
        private List<Enemy.Enemy> _hitEnemies = new List<Enemy.Enemy>();
        private int _currentPierceCount = 0;
        private Vector3 _moveDirection;
        private float _lifetimeTimer = 0f;
        private float _lastHitTime = 0f;

        public void Initialize(Enemy.Enemy target, float damage, DamageType damageType, float splashRadius = 0f, int pierceCount = 0, int maxSplashTargets = 0)
        {
            _target = target;
            _damage = damage;
            _damageType = damageType;
            _splashRadius = splashRadius;
            _maxSplashTargets = maxSplashTargets;
            _hasHit = false;

            _isPierceMode = pierceCount > 0;

            if (_isPierceMode)
            {
                _maxPierceCount = pierceCount;
                if (target != null)
                {
                    _moveDirection = (target.transform.position - transform.position).normalized;
                }
                else
                {
                    _moveDirection = transform.up;
                }
                
                float angle = Mathf.Atan2(_moveDirection.y, _moveDirection.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

                if (_showDebugLogs)
                {
                    Debug.Log($"⚔️ 穿透模式: maxPierce={_maxPierceCount}, dir=({_moveDirection.x:F2},{_moveDirection.y:F2})");
                }
            }

            _hitEnemies.Clear();
            _currentPierceCount = 0;
            _lifetimeTimer = 0f;
            _lastHitTime = 0f;
        }

        private void Update()
        {
            if (_isPierceMode)
            {
                UpdatePierceMovement();
                return;
            }

            if (_target == null || _target.IsDead)
            {
                FindNewTarget();
                if (_target == null)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            MoveTowardsTarget();
        }

        private void UpdatePierceMovement()
        {
            _lifetimeTimer += Time.deltaTime;
            
            if (_lifetimeTimer >= _maxLifetime || _currentPierceCount >= _maxPierceCount)
            {
                if (_showDebugLogs)
                {
                    Debug.Log($"穿透结束: life={_lifetimeTimer:F1}s, hits={_currentPierceCount}");
                }
                DestroyProjectile();
                return;
            }

            transform.position += _moveDirection * _speed * Time.deltaTime;

            CheckPierceHits();
        }

        private void CheckPierceHits()
        {
            if (Time.time - _lastHitTime < _hitCooldown) return;

            float hitRange = 0.5f;
            
            Enemy.Enemy[] allEnemies = FindObjectsOfType<Enemy.Enemy>();
            List<Enemy.Enemy> enemiesInRange = new List<Enemy.Enemy>();
            
            foreach (var enemy in allEnemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                
                float dist = Vector3.Distance(transform.position, enemy.transform.position);
                if (dist <= hitRange)
                {
                    enemiesInRange.Add(enemy);
                }
            }

            if (_showDebugLogs && enemiesInRange.Count > 0)
            {
                Debug.Log($"范围内有 {enemiesInRange.Count} 个敌人, 已击中: {_hitEnemies.Count}");
            }

            foreach (var enemy in enemiesInRange)
            {
                if (_hitEnemies.Contains(enemy)) continue;

                if (_showDebugLogs)
                {
                    Debug.Log($"💥 击中敌人! {enemy.name}, 第 {_currentPierceCount + 1}/{_maxPierceCount} 次");
                }

                DealDamageToEnemy(enemy);
                _hitEnemies.Add(enemy);
                _currentPierceCount++;
                _lastHitTime = Time.time;

                PlayImpactEffect();

                if (_currentPierceCount >= _maxPierceCount)
                {
                    DestroyProjectile();
                    return;
                }
            }
        }

        private void MoveTowardsTarget()
        {
            if (_target == null) return;

            Vector3 targetPosition = _target.transform.position;
            Vector3 direction = (targetPosition - transform.position).normalized;

            transform.position += direction * _speed * Time.deltaTime;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

            float distance = Vector3.Distance(transform.position, targetPosition);
            if (distance <= 0.5f)
            {
                HitTarget();
            }
        }

        private void FindNewTarget()
        {
            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            if (spawner != null)
            {
                _target = spawner.GetClosestEnemy(transform.position, 500f);
            }
        }

        private void HitTarget()
        {
            if (_hasHit) return;
            _hasHit = true;

            if (_splashRadius > 0)
            {
                DealSplashDamage();
            }
            else
            {
                DealSingleTargetDamage();
            }

            PlayImpactEffect();
            Destroy(gameObject);
        }

        private void DealSingleTargetDamage()
        {
            if (_target != null && !_target.IsDead)
            {
                _target.Health.TakeDamage(Mathf.RoundToInt(_damage), _damageType);
            }
        }

        private void DealDamageToEnemy(Enemy.Enemy enemy)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.Health.TakeDamage(Mathf.RoundToInt(_damage), _damageType);
            }
        }

        private void DealSplashDamage()
        {
            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            if (spawner == null) return;

            List<Enemy.Enemy> enemiesInSplash = spawner.GetEnemiesInRange(transform.position, _splashRadius);

            // 按距离排序，近的优先
            enemiesInSplash.Sort((a, b) =>
            {
                float distA = Vector3.Distance(transform.position, a.transform.position);
                float distB = Vector3.Distance(transform.position, b.transform.position);
                return distA.CompareTo(distB);
            });

            int hitCount = 0;
            foreach (var enemy in enemiesInSplash)
            {
                if (enemy == null || enemy.IsDead) continue;

                // 如果设了上限，达到数量后停止
                if (_maxSplashTargets > 0 && hitCount >= _maxSplashTargets) break;

                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                float damageMultiplier = 1f - (distance / _splashRadius) * 0.5f;
                int damage = Mathf.RoundToInt(_damage * damageMultiplier);
                enemy.Health.TakeDamage(damage, _damageType);
                hitCount++;
            }
        }

        private void PlayImpactEffect()
        {
            if (_impactEffect != null)
            {
                GameObject effect = Instantiate(_impactEffect, transform.position, Quaternion.identity);
                Destroy(effect, _impactDuration);
            }
        }

        private void DestroyProjectile()
        {
            _hasHit = true;
            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_isPierceMode) return;
            
            Enemy.Enemy enemy = other.GetComponent<Enemy.Enemy>();
            if (enemy != null && enemy == _target)
            {
                HitTarget();
            }
        }
    }
}
