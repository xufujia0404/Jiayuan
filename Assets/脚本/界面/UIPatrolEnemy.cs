using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public class UIPatrolEnemy : MonoBehaviour
    {
        [Header("Patrol Settings")]
        [SerializeField] private float _moveSpeed = 80f;
        [SerializeField] private float _patrolMinX = -700f;
        [SerializeField] private float _patrolMaxX = 700f;
        [SerializeField] private float _patrolY = -200f;
        [SerializeField] private float _idleChance = 0.3f;
        [SerializeField] private float _idleDurationMin = 1.5f;
        [SerializeField] private float _idleDurationMax = 4f;
        [SerializeField] private float _bobSpeed = 2f;
        [SerializeField] private float _bobAmount = 8f;

        [Header("Sprite")]
        [SerializeField] private Sprite _enemySprite;

        private RectTransform _rt;
        private Image _img;
        private int _direction = 1;
        private bool _isIdle;
        private float _idleTimer;
        private float _baseY;
        private float _randomOffset;

        private void Start()
        {
            _rt = GetComponent<RectTransform>();
            _img = GetComponent<Image>();
            _baseY = _patrolY + Random.Range(-50f, 50f);
            _randomOffset = Random.Range(0f, 100f);

            if (_enemySprite != null)
            {
                _img.sprite = _enemySprite;
                _img.SetNativeSize();
            }
            _img.raycastTarget = false;

            float startX = Random.Range(_patrolMinX, _patrolMaxX);
            _rt.anchoredPosition = new Vector2(startX, _baseY);

            _direction = Random.value > 0.5f ? 1 : -1;
            UpdateFlip();
        }

        private void Update()
        {
            if (_rt == null) return;

            if (_isIdle)
            {
                _idleTimer -= Time.deltaTime;
                if (_idleTimer <= 0f)
                {
                    _isIdle = false;
                    _direction = Random.value > 0.5f ? 1 : -1;
                    UpdateFlip();
                }
            }
            else
            {
                float moveAmount = _moveSpeed * Time.deltaTime * _direction;
                Vector2 pos = _rt.anchoredPosition;
                pos.x += moveAmount;

                if (pos.x > _patrolMaxX)
                {
                    pos.x = _patrolMaxX;
                    StartIdleOrTurn();
                }
                else if (pos.x < _patrolMinX)
                {
                    pos.x = _patrolMinX;
                    StartIdleOrTurn();
                }

                _rt.anchoredPosition = pos;
            }

            float bobOffset = Mathf.Sin((Time.time + _randomOffset) * _bobSpeed) * _bobAmount;
            Vector2 currentPos = _rt.anchoredPosition;
            currentPos.y = _baseY + bobOffset;
            _rt.anchoredPosition = currentPos;
        }

        private void StartIdleOrTurn()
        {
            if (Random.value < _idleChance)
            {
                _isIdle = true;
                _idleTimer = Random.Range(_idleDurationMin, _idleDurationMax);
            }
            else
            {
                _direction *= -1;
                UpdateFlip();
            }
        }

        private void UpdateFlip()
        {
            if (_img == null) return;
            Vector3 scale = _img.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * _direction;
            _img.transform.localScale = scale;
        }
    }
}
