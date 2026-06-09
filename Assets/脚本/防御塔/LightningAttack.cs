using UnityEngine;
using TowerDefense.Data;
using TowerDefense.Enemy;

namespace TowerDefense.Tower
{
    public class LightningAttack : TowerAttack
    {
        [Header("Lightning Settings")]
        [SerializeField] private int _segments = 12;
        [SerializeField] private float _arcAmplitude = 0.3f;
        [SerializeField] private float _jitterSpeed = 20f;
        [SerializeField] private float _beamWidth = 0.12f;
        [SerializeField] private float _glowWidth = 0.3f;

        [Header("Colors")]
        [SerializeField] private Color _coreColor = new Color(0.9f, 0.7f, 1f, 1f);
        [SerializeField] private Color _glowColor = new Color(0.5f, 0.1f, 0.9f, 0.6f);

        [Header("Damage Timing")]
        [SerializeField] private float _damageInterval = 0.2f;

        private LineRenderer _coreLine;
        private LineRenderer _glowLine;
        private float _damageTimer;
        private Enemy.Enemy _trackedTarget;
        private bool _isFiring;
        private Vector3[] _linePoints;

        protected override void Update()
        {
            base.Update();

            if (_isFiring)
            {
                float range = _tower != null ? _tower.CurrentStats.attackRange : float.MaxValue;
                bool outOfRange = _trackedTarget != null &&
                    Vector3.Distance(transform.position, _trackedTarget.transform.position) > range;

                if (_trackedTarget == null || _trackedTarget.IsDead || outOfRange)
                {
                    StopLightning();
                    return;
                }

                UpdateLightningVisual();
            }
        }

        public override void Attack(Enemy.Enemy target)
        {
            if (target == null || target.IsDead)
            {
                StopLightning();
                return;
            }

            _trackedTarget = target;

            if (!_isFiring)
            {
                StartLightning();
                _isFiring = true;
                _damageTimer = 0f;
            }

            _damageTimer += Time.deltaTime;
            if (_damageTimer >= _damageInterval)
            {
                _damageTimer = 0f;
                DealDamage(target);
            }
        }

        public override bool CanAttack()
        {
            return _tower != null && _tower.CurrentTarget != null && !_tower.CurrentTarget.IsDead;
        }

        private void DealDamage(Enemy.Enemy target)
        {
            if (target == null || target.IsDead) return;
            float dmg = _tower.CurrentStats.damage;
            target.Health.TakeDamage(Mathf.RoundToInt(dmg), DamageType.Magic);
        }

        private void StartLightning()
        {
            _linePoints = new Vector3[_segments];

            if (_coreLine == null)
            {
                GameObject coreObj = new GameObject("LightningCore");
                coreObj.transform.SetParent(transform);
                coreObj.transform.localPosition = Vector3.zero;
                _coreLine = coreObj.AddComponent<LineRenderer>();
                _coreLine.material = new Material(Shader.Find("Sprites/Default"));
                _coreLine.startColor = _coreColor;
                _coreLine.endColor = _coreColor;
                _coreLine.startWidth = _beamWidth;
                _coreLine.endWidth = _beamWidth;
                _coreLine.positionCount = _segments;
                _coreLine.sortingOrder = 10;
                _coreLine.useWorldSpace = true;
            }

            if (_glowLine == null)
            {
                GameObject glowObj = new GameObject("LightningGlow");
                glowObj.transform.SetParent(transform);
                glowObj.transform.localPosition = Vector3.zero;
                _glowLine = glowObj.AddComponent<LineRenderer>();
                _glowLine.material = new Material(Shader.Find("Sprites/Default"));
                _glowLine.startColor = _glowColor;
                _glowLine.endColor = _glowColor;
                _glowLine.startWidth = _glowWidth;
                _glowLine.endWidth = _glowWidth;
                _glowLine.positionCount = _segments;
                _glowLine.sortingOrder = 9;
                _glowLine.useWorldSpace = true;
            }

            _coreLine.enabled = true;
            _glowLine.enabled = true;
        }

        private void StopLightning()
        {
            _isFiring = false;
            _trackedTarget = null;
            if (_coreLine != null) _coreLine.enabled = false;
            if (_glowLine != null) _glowLine.enabled = false;
        }

        private void UpdateLightningVisual()
        {
            if (_trackedTarget == null || _trackedTarget.IsDead)
            {
                StopLightning();
                return;
            }

            Vector3 start = FirePoint != null ? FirePoint.position : transform.position;
            Vector3 end = _trackedTarget.transform.position;
            Vector3 dir = end - start;

            for (int i = 0; i < _segments; i++)
            {
                float t = (float)i / (_segments - 1);
                Vector3 point = Vector3.Lerp(start, end, t);

                if (i > 0 && i < _segments - 1)
                {
                    Vector3 perpendicular = new Vector3(-dir.y, dir.x, 0).normalized;
                    float noise = Mathf.PerlinNoise(i * 1.7f + Time.time * _jitterSpeed, 0f) - 0.5f;
                    float amplitude = _arcAmplitude * (1f - Mathf.Abs(t - 0.5f) * 2f);
                    point += perpendicular * noise * amplitude * 2f;
                }

                _linePoints[i] = point;
            }

            _coreLine.SetPositions(_linePoints);
            _glowLine.SetPositions(_linePoints);

            _coreLine.startColor = _coreColor;
            _coreLine.endColor = _coreColor;
            _glowLine.startColor = _glowColor;
            _glowLine.endColor = _glowColor;
        }

        private void OnDisable()
        {
            StopLightning();
        }

        private void OnDestroy()
        {
            if (_coreLine != null) Destroy(_coreLine.gameObject);
            if (_glowLine != null) Destroy(_glowLine.gameObject);
        }
    }
}
