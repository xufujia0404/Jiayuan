using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Data;
using TowerDefense.Enemy;

namespace TowerDefense.Tower
{
    public enum TowerState
    {
        Idle,
        Targeting,
        Attacking,
        Upgrading,
        Selling
    }

    public class Tower : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] protected TowerData _towerData;
        [SerializeField] protected TowerAttack _attack;
        [SerializeField] protected Animator _animator;
        [SerializeField] protected Transform _rotatePart;
        [SerializeField] protected SpriteRenderer _spriteRenderer;

        [Header("Visual")]
        [SerializeField] protected GameObject _rangeIndicator;
        [SerializeField] protected Color _rangeColor = new Color(0, 1, 0, 0.3f);
        [Tooltip("Rotation offset in degrees. Default -90 assumes sprite faces UP. Change to 0 if sprite faces RIGHT.")]
        [SerializeField] protected float _rotationOffset = -90f;

        protected int _currentLevel = 1;
        protected TowerState _currentState = TowerState.Idle;
        protected TowerData.TowerStats _currentStats;
        protected Enemy.Enemy _currentTarget;
        protected TowerSlot _slot;

        public TowerData Data => _towerData;
        public int CurrentLevel => _currentLevel;
        public TowerState State => _currentState;
        public Enemy.Enemy CurrentTarget => _currentTarget;
        public TowerData.TowerStats CurrentStats => _currentStats;
        public TowerSlot Slot => _slot;
        public bool IsMaxLevel => _currentLevel >= _towerData.levels.Length;

        public System.Action<Tower> OnUpgrade;
        public System.Action<Tower> OnSell;
        public System.Action<Enemy.Enemy> OnTargetAcquired;
        public System.Action OnTargetLost;

        protected virtual void Awake()
        {
            if (_attack == null) _attack = GetComponent<TowerAttack>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        public virtual void Initialize(TowerData data, TowerSlot slot)
        {
            _towerData = data;
            _slot = slot;
            _currentLevel = 1;
            _currentStats = data.levels[0];
            _currentState = TowerState.Idle;

            if (_attack != null)
            {
                _attack.Initialize(this);
            }

            UpdateRangeIndicator();
        }

        protected virtual void Update()
        {
            if (_currentState == TowerState.Upgrading || _currentState == TowerState.Selling)
                return;

            FindTarget();

            if (_currentTarget != null)
            {
                RotateTowardsTarget();
                Attack();
            }
        }

        protected virtual void FindTarget()
        {
            float actualAttackRange = _attack != null ? _attack.GetEffectiveAttackRange() : _currentStats.attackRange;
            
            if (_currentTarget != null && !_currentTarget.IsDead)
            {
                float distance = Vector3.Distance(transform.position, _currentTarget.transform.position);
                if (distance <= actualAttackRange)
                {
                    return;
                }
            }

            _currentTarget = FindBestTarget();

            if (_currentTarget != null)
            {
                _currentState = TowerState.Targeting;
                OnTargetAcquired?.Invoke(_currentTarget);
            }
            else
            {
                _currentState = TowerState.Idle;
                OnTargetLost?.Invoke();
            }
        }

        protected virtual Enemy.Enemy FindBestTarget()
        {
            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            if (spawner == null)
            {
                Debug.LogError("EnemySpawner not found!");
                return null;
            }

            float actualAttackRange = _attack != null ? _attack.GetEffectiveAttackRange() : _currentStats.attackRange;
            List<Enemy.Enemy> enemiesInRange = spawner.GetEnemiesInRange(transform.position, actualAttackRange);

            if (enemiesInRange.Count == 0) return null;

            Enemy.Enemy bestTarget = null;
            float bestPriority = float.MinValue;

            foreach (var enemy in enemiesInRange)
            {
                if (enemy == null || enemy.IsDead) continue;

                // 兵营塔无法攻击飞行敌人
                if (_towerData != null && _towerData.towerType == TowerType.Barracks)
                {
                    if (enemy.Data != null && enemy.Data.stats.isFlying) continue;
                }

                float priority = CalculateTargetPriority(enemy);
                if (priority > bestPriority)
                {
                    bestPriority = priority;
                    bestTarget = enemy;
                }
            }

            return bestTarget;
        }

        protected virtual float CalculateTargetPriority(Enemy.Enemy enemy)
        {
            EnemyMovement movement = enemy.GetComponent<EnemyMovement>();
            if (movement != null)
            {
                return movement.GetProgressPercent();
            }
            return 0f;
        }

        protected virtual void RotateTowardsTarget()
        {
            if (_currentTarget == null) return;
            if (_towerData != null && (_towerData.towerType == TowerType.Barracks || _towerData.towerType == TowerType.Mage)) return;

            Transform rotateTransform = GetRotateTransform();
            if (rotateTransform == null) return;

            Vector3 direction = _currentTarget.transform.position - rotateTransform.position;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + _rotationOffset;
            Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
            rotateTransform.rotation = Quaternion.Slerp(rotateTransform.rotation, targetRotation, 10f * Time.deltaTime);
        }

        /// <summary>
        /// Returns the transform that should rotate towards the target.
        /// Uses _rotatePart if it has a visual (renderer in children), 
        /// otherwise falls back to the sprite renderer's transform.
        /// </summary>
        protected Transform GetRotateTransform()
        {
            if (_rotatePart != null)
            {
                if (_rotatePart.GetComponent<Renderer>() != null || _rotatePart.GetComponentInChildren<Renderer>() != null)
                    return _rotatePart;
            }

            if (_spriteRenderer != null)
                return _spriteRenderer.transform;

            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
                return sr.transform;

            return _rotatePart;
        }

        protected virtual void Attack()
        {
            if (_attack == null) return;

            if (_attack.CanAttack())
            {
                _currentState = TowerState.Attacking;
                _attack.Attack(_currentTarget);
            }
        }

        public virtual bool Upgrade()
        {
            if (IsMaxLevel) return false;

            TowerData.TowerStats nextLevelStats = _towerData.levels[_currentLevel];
            if (!GameManager.Instance.SpendGold(nextLevelStats.upgradeCost)) return false;

            _currentLevel++;
            _currentStats = nextLevelStats;

            if (_animator != null)
            {
                _animator.SetTrigger("Upgrade");
            }

            UpdateRangeIndicator();
            OnUpgrade?.Invoke(this);
            EventBus.Publish(new TowerUpgradedEvent { Tower = gameObject, Level = _currentLevel });

            return true;
        }

        public virtual void Sell()
        {
            Debug.Log($"📤 Tower.Sell() called for {gameObject.name}");
            Debug.Log($"   sellValue: {_currentStats.sellValue}");
            Debug.Log($"   _slot: {_slot != null}");
            
            int sellValue = _currentStats.sellValue;
            GameManager.Instance.AddGold(sellValue);
            Debug.Log($"   ✅ Added {sellValue} gold");

            OnSell?.Invoke(this);
            EventBus.Publish(new TowerSoldEvent { Tower = gameObject, Refund = sellValue });

            if (_slot != null)
            {
                _slot.RemoveTower();
                Debug.Log($"   ✅ Called _slot.RemoveTower()");
            }

            Destroy(gameObject);
            Debug.Log($"   ✅ Tower destroyed");
        }

        public int GetUpgradeCost()
        {
            if (IsMaxLevel) return 0;
            return _towerData.levels[_currentLevel].upgradeCost;
        }

        public int GetSellValue()
        {
            return _currentStats.sellValue;
        }

        public void ShowRange()
        {
            if (_rangeIndicator != null)
            {
                _rangeIndicator.SetActive(true);
            }
        }

        public void HideRange()
        {
            if (_rangeIndicator != null)
            {
                _rangeIndicator.SetActive(false);
            }
        }

        protected void UpdateRangeIndicator()
        {
            if (_rangeIndicator != null)
            {
                float scale = _currentStats.attackRange * 2f;
                _rangeIndicator.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_towerData != null && _currentStats.attackRange > 0)
            {
                Gizmos.color = _rangeColor;
                Gizmos.DrawWireSphere(transform.position, _currentStats.attackRange);
            }
        }
    }
}
