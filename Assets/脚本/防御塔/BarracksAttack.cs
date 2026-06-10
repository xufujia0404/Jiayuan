using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TowerDefense.Data;
using TowerDefense.Enemy;
using TowerDefense.Utils;

namespace TowerDefense.Tower
{
    public class BarracksAttack : TowerAttack
    {
        [Header("Barracks Settings")]
        [SerializeField] private SoldierData _soldierData;
        [SerializeField] private float _spawnRadius = 0.5f;

        [Header("Rally Point")]
        [SerializeField] private Transform _rallyPoint;
        [SerializeField] private float _rallyPointRange = 3f;
        [SerializeField] private GameObject _rallyPointMarker;
        [SerializeField] private bool _rallyPointDraggable = true;

        private List<Soldier> _soldiers = new List<Soldier>();
        private int _maxSoldiers = 3;
        private bool _soldiersSpawned = false;

        public List<Soldier> Soldiers => _soldiers;
        public Transform RallyPoint => _rallyPoint;
        public float RallyPointRange => _rallyPointRange;

        public int AliveSoldierCount
        {
            get
            {
                int count = 0;
                foreach (var s in _soldiers)
                    if (s != null && s.IsAlive) count++;
                return count;
            }
        }

        public override void Initialize(Tower tower)
        {
            base.Initialize(tower);

            int oldMax = _maxSoldiers;

            // Determine max soldiers from tower data
            if (_tower.Data != null && _tower.Data.soldierCountPerLevel != null
                && _tower.Data.soldierCountPerLevel.Length > 0)
            {
                int levelIndex = Mathf.Clamp(_tower.CurrentLevel - 1, 0, _tower.Data.soldierCountPerLevel.Length - 1);
                _maxSoldiers = _tower.Data.soldierCountPerLevel[levelIndex];
            }
            else
            {
                _maxSoldiers = _tower.CurrentLevel switch
                {
                    1 => 3,
                    2 => 5,
                    3 => 7,
                    _ => 3
                };
            }

            if (_soldierData == null && _tower.Data != null)
                _soldierData = _tower.Data.soldierData;

            SetupRallyPoint();

            // 升级后补充新士兵
            if (_soldiersSpawned && _maxSoldiers > oldMax)
            {
                SpawnAdditionalSoldiers(_maxSoldiers - oldMax);
            }
        }

        /// <summary>
        /// 补充指定数量的新士兵（升级时调用）。
        /// </summary>
        private void SpawnAdditionalSoldiers(int count)
        {
            ClearDeadSoldiers();
            Vector3 basePos = _rallyPoint != null ? _rallyPoint.position : transform.position;

            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, 2f * Mathf.PI);
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * _spawnRadius,
                    Mathf.Sin(angle) * _spawnRadius,
                    0f
                );
                Vector3 spawnPos = basePos + offset;

                GameObject soldierObj = Instantiate(_soldierData.soldierPrefab, spawnPos, Quaternion.identity);
                Soldier soldier = soldierObj.GetComponent<Soldier>();
                if (soldier == null) soldier = soldierObj.AddComponent<Soldier>();

                soldier.Initialize(_soldierData, _rallyPoint);
                soldier.OnSoldierDeath += OnSoldierDeath;
                _soldiers.Add(soldier);
            }

            Debug.Log($"Barracks upgraded: spawned {count} additional soldiers (total: {_soldiers.Count})");
        }

        /// <summary>
        /// Setup the rally point. If not assigned, find the closest point on the enemy path.
        /// In Kingdom Rush, soldiers stand at the rally point and engage enemies from there.
        /// </summary>
        private void SetupRallyPoint()
        {
            // Create rally point object if not assigned
            if (_rallyPoint == null)
            {
                var rallyObj = new GameObject("RallyPoint");
                rallyObj.transform.SetParent(transform);
                rallyObj.transform.position = FindDefaultRallyPosition();
                _rallyPoint = rallyObj.transform;
            }

            // Create visual marker
            if (_rallyPointMarker == null)
            {
                _rallyPointMarker = CreateRallyPointMarker();
            }

            _rallyPointMarker.transform.SetParent(_rallyPoint);
            _rallyPointMarker.transform.localPosition = Vector3.zero;
            _rallyPointMarker.SetActive(true);
        }

        /// <summary>
        /// Find the default rally position: closest point on the enemy path to the tower.
        /// This is the Kingdom Rush behavior — soldiers rally near the path, not at the tower.
        /// </summary>
        private Vector3 FindDefaultRallyPosition()
        {
            var pathCreator = FindObjectOfType<PathCreator>();
            if (pathCreator != null && pathCreator.WaypointCount > 0)
            {
                Vector3 closestPoint = Vector3.zero;
                float closestDist = float.MaxValue;

                // Check interpolated path points for closest to tower
                var smoothPath = pathCreator.GetSmoothPath(20);
                foreach (var pathPoint in smoothPath)
                {
                    float dist = Vector3.Distance(transform.position, pathPoint);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closestPoint = pathPoint;
                    }
                }

                // Offset slightly off-path so soldiers aren't standing ON the path
                Vector3 awayFromPath = (transform.position - closestPoint).normalized;
                if (awayFromPath == Vector3.zero) awayFromPath = Vector3.up;
                closestPoint += awayFromPath * 0.8f;

                return closestPoint;
            }

            // Fallback: just offset from tower towards path center
            return transform.position + Vector3.right * _rallyPointRange * 0.7f;
        }

        private GameObject CreateRallyPointMarker()
        {
            var marker = new GameObject("RallyPointMarker");
            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(1f, 0.8f, 0f, 0.4f);
            sr.sortingOrder = -1;
            marker.transform.localScale = new Vector3(_rallyPointRange * 2f, _rallyPointRange * 2f, 1f);
            return marker;
        }

        private Sprite CreateCircleSprite()
        {
            int size = 64;
            var tex = new Texture2D(size, size);
            var center = new Vector2(size / 2f, size / 2f);
            float radius = size / 2f - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > radius - 1f && dist < radius + 1f)
                        tex.SetPixel(x, y, Color.white);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Move rally point to a new position (called by drag handler).
        /// Clamped within tower's attack range.
        /// </summary>
        public void MoveRallyPoint(Vector3 newPosition)
        {
            // Clamp to within attack range of tower
            float maxDist = GetEffectiveAttackRange();
            Vector3 dir = newPosition - transform.position;
            if (dir.magnitude > maxDist)
            {
                newPosition = transform.position + dir.normalized * maxDist;
            }

            _rallyPoint.position = newPosition;

            // Move idle soldiers to new rally point
            foreach (var soldier in _soldiers)
            {
                if (soldier != null && soldier.IsAlive && soldier.State == SoldierState.Idle)
                {
                    soldier.SetRallyPoint(_rallyPoint);
                }
            }
        }

        protected override void Update()
        {
            base.Update();
            AutoAssignTargets();
        }

        public override void Attack(Enemy.Enemy target)
        {
            if (target == null || target.IsDead) return;

            if (!_soldiersSpawned)
            {
                SpawnAllSoldiers();
                _soldiersSpawned = true;
            }

            AssignTarget(target);
        }

        protected override void SpawnSoldiers(Enemy.Enemy target)
        {
            if (_soldierData == null || _soldierData.soldierPrefab == null)
            {
                Debug.LogWarning("BarracksAttack: SoldierData or prefab is null!");
                return;
            }

            if (!_soldiersSpawned)
            {
                SpawnAllSoldiers();
                _soldiersSpawned = true;
            }

            AssignTarget(target);
        }

        private void SpawnAllSoldiers()
        {
            ClearDeadSoldiers();

            int toSpawn = _maxSoldiers - _soldiers.Count;
            Vector3 basePos = _rallyPoint != null ? _rallyPoint.position : transform.position;

            for (int i = 0; i < toSpawn; i++)
            {
                float angle = (2f * Mathf.PI * i) / _maxSoldiers;
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * _spawnRadius,
                    Mathf.Sin(angle) * _spawnRadius,
                    0f
                );
                Vector3 spawnPos = basePos + offset;

                GameObject soldierObj = Instantiate(_soldierData.soldierPrefab, spawnPos, Quaternion.identity);
                // Don't parent under tower — non-uniform parent scale breaks child positioning

                Soldier soldier = soldierObj.GetComponent<Soldier>();
                if (soldier == null)
                {
                    soldier = soldierObj.AddComponent<Soldier>();
                }

                soldier.Initialize(_soldierData, _rallyPoint);
                soldier.OnSoldierDeath += OnSoldierDeath;
                _soldiers.Add(soldier);
            }

            Debug.Log($"Barracks spawned {toSpawn} soldiers at rally point (total: {_soldiers.Count})");
        }

        private void AssignTarget(Enemy.Enemy target)
        {
            if (target == null || target.IsDead) return;

            foreach (var soldier in _soldiers)
            {
                if (soldier != null && soldier.IsAlive && soldier.State == SoldierState.Idle)
                {
                    soldier.EngageTarget(target);
                }
            }
        }

        /// <summary>
        /// Auto-assign idle soldiers to enemies within rally point range.
        /// In Kingdom Rush, soldiers detect enemies from their rally point, not the tower.
        /// </summary>
        private void AutoAssignTargets()
        {
            if (_tower == null) return;

            List<Soldier> idleSoldiers = new List<Soldier>();
            foreach (var s in _soldiers)
            {
                if (s != null && s.IsAlive && s.State == SoldierState.Idle)
                    idleSoldiers.Add(s);
            }

            if (idleSoldiers.Count == 0) return;

            EnemySpawner spawner = FindObjectOfType<EnemySpawner>();
            if (spawner == null) return;

            // Search from rally point, not tower position
            Vector3 searchOrigin = _rallyPoint != null ? _rallyPoint.position : transform.position;
            float searchRange = _soldierData != null ? _soldierData.engageRange : 3f;
            var enemiesInRange = spawner.GetEnemiesInRange(searchOrigin, searchRange);

            foreach (var soldier in idleSoldiers)
            {
                Enemy.Enemy bestTarget = null;
                float bestDist = float.MaxValue;

                foreach (var enemy in enemiesInRange)
                {
                    if (enemy == null || enemy.IsDead) continue;
                    // 士兵无法攻击飞行敌人
                    if (enemy.Data != null && enemy.Data.stats.isFlying) continue;

                    float dist = Vector3.Distance(soldier.transform.position, enemy.transform.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = enemy;
                    }
                }

                if (bestTarget != null)
                {
                    soldier.EngageTarget(bestTarget);
                }
            }
        }

        private void OnSoldierDeath(Soldier soldier)
        {
            soldier.OnSoldierDeath -= OnSoldierDeath;
            StartCoroutine(RespawnSoldierAfterDelay(soldier, _soldierData.respawnTime));
        }

        private IEnumerator RespawnSoldierAfterDelay(Soldier soldier, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (soldier != null && _tower != null)
            {
                Vector3 basePos = _rallyPoint != null ? _rallyPoint.position : transform.position;
                float angle = Random.Range(0f, 2f * Mathf.PI);
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle) * _spawnRadius,
                    Mathf.Sin(angle) * _spawnRadius,
                    0f
                );
                soldier.Respawn(basePos + offset);
                soldier.SetRallyPoint(_rallyPoint);
                soldier.OnSoldierDeath += OnSoldierDeath;
                Debug.Log("Barracks soldier respawned at rally point");
            }
        }

        private void ClearDeadSoldiers()
        {
            for (int i = _soldiers.Count - 1; i >= 0; i--)
            {
                if (_soldiers[i] == null)
                {
                    _soldiers.RemoveAt(i);
                }
            }
        }

        public override bool CanAttack()
        {
            return !_soldiersSpawned || AliveSoldierCount > 0;
        }

        public override float GetEffectiveAttackRange()
        {
            return _soldierData != null ? _soldierData.engageRange : 3f;
        }

        private void OnDestroy()
        {
            foreach (var soldier in _soldiers)
            {
                if (soldier != null)
                {
                    soldier.OnSoldierDeath -= OnSoldierDeath;
                    Destroy(soldier.gameObject);
                }
            }
            _soldiers.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            // Draw rally point range
            if (_rallyPoint != null)
            {
                Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
                Gizmos.DrawWireSphere(_rallyPoint.position, _rallyPointRange);
            }
        }
    }
}
