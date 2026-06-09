using System.Collections;
using UnityEngine;
using TowerDefense.Data;
using TowerDefense.Enemy;

namespace TowerDefense.Tower
{
    public class TowerAttack : MonoBehaviour
    {
        [Header("Attack Settings")]
        [SerializeField] private Transform _firePoint;
        [SerializeField] private bool _autoFire = true;

        protected Transform FirePoint => _firePoint;

        protected Tower _tower;
        protected float _attackCooldown = 0f;
        protected bool _canAttack = true;

        public bool CanAttackValue => _canAttack && _attackCooldown <= 0f;

        public virtual void Initialize(Tower tower)
        {
            _tower = tower;
            _attackCooldown = 0f;
        }

        protected virtual void Update()
        {
            if (_attackCooldown > 0)
            {
                _attackCooldown -= Time.deltaTime;
            }
        }

        public virtual bool CanAttack()
        {
            return _attackCooldown <= 0f && _tower.CurrentTarget != null;
        }

        /// <summary>
        /// Returns the effective attack range for targeting purposes.
        /// Projectile towers use their stats range; barracks use soldier engage range.
        /// </summary>
        public virtual float GetEffectiveAttackRange()
        {
            return _tower != null ? _tower.CurrentStats.attackRange : 0f;
        }

        public virtual void Attack(Enemy.Enemy target)
        {
            if (target == null || target.IsDead) return;

            TowerData.TowerStats stats = _tower.CurrentStats;

            switch (_tower.Data.towerType)
            {
                case TowerType.Archer:
                    FireProjectile(target);
                    break;
                case TowerType.Mage:
                    FireMagicAttack(target);
                    break;
                case TowerType.Barracks:
                    SpawnSoldiers(target);
                    break;
                case TowerType.Artillery:
                    FireExplosive(target);
                    break;
                case TowerType.Sword:
                    FirePierceProjectile(target);
                    break;
            }

            // Barracks don't use cooldown - soldiers continuously auto-assign
            if (_tower.Data.towerType != TowerType.Barracks)
            {
                _attackCooldown = 1f / stats.attackSpeed;
            }

            if (_tower.GetComponent<Animator>() != null)
            {
                _tower.GetComponent<Animator>().SetTrigger("Attack");
            }
        }

        protected virtual void FireProjectile(Enemy.Enemy target)
        {
            Debug.Log($"FireProjectile called");
            Debug.Log($"Projectile prefab: " + _tower.Data.projectilePrefab);
            Debug.Log($"FirePoint: " + _firePoint);
            
            if (_tower.Data.projectilePrefab == null || _firePoint == null)
            {
                if (_tower.Data.projectilePrefab == null)
                {
                    Debug.LogError("Projectile prefab is null!");
                }
                return;
            }

            GameObject projectileObj = Instantiate(
                _tower.Data.projectilePrefab,
                _firePoint.position,
                Quaternion.identity
            );

            Projectile projectile = projectileObj.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Initialize(target, _tower.CurrentStats.damage, DamageType.Physical);
            }
        }

        protected virtual void FireMagicAttack(Enemy.Enemy target)
        {
            if (_tower.Data.projectilePrefab == null || _firePoint == null) return;

            GameObject projectileObj = Instantiate(
                _tower.Data.projectilePrefab,
                _firePoint.position,
                Quaternion.identity
            );

            Projectile projectile = projectileObj.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Initialize(target, _tower.CurrentStats.damage, DamageType.Magic);
            }
        }

        protected virtual void FireExplosive(Enemy.Enemy target)
        {
            if (_tower.Data.projectilePrefab == null || _firePoint == null) return;

            GameObject projectileObj = Instantiate(
                _tower.Data.projectilePrefab,
                _firePoint.position,
                Quaternion.identity
            );

            Projectile projectile = projectileObj.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Initialize(
                    target,
                    _tower.CurrentStats.damage,
                    DamageType.Explosion,
                    _tower.CurrentStats.splashRadius,
                    0,
                    _tower.CurrentStats.maxTargets
                );
            }
        }

        protected virtual void SpawnSoldiers(Enemy.Enemy target)
        {
        }

        protected virtual void FirePierceProjectile(Enemy.Enemy target)
        {
            if (_tower.Data.projectilePrefab == null || _firePoint == null) return;

            TowerData.TowerStats stats = _tower.CurrentStats;
            
            int pierceCount = stats.pierceCount;
            if (pierceCount <= 0)
            {
                int towerLevel = _tower.CurrentLevel;
                pierceCount = towerLevel switch
                {
                    1 => 3,
                    2 => 5,
                    3 => 7,
                    _ => 3
                };
            }

            GameObject projectileObj = Instantiate(
                _tower.Data.projectilePrefab,
                _firePoint.position,
                Quaternion.identity
            );

            Projectile projectile = projectileObj.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Initialize(target, stats.damage, DamageType.Physical, 0f, pierceCount);
            }
        }

        public void SetFirePoint(Transform firePoint)
        {
            _firePoint = firePoint;
        }
    }
}
