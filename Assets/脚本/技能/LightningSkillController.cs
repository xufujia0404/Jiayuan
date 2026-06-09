using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TowerDefense.Enemy;

/// <summary>
/// Controls the Lightning skill in 2D: shows range circle on activation,
/// follows mouse, releases lightning on click with screen flash and camera shake.
/// All positions and effects operate on the XY plane (Z=0).
/// </summary>
public class LightningSkillController : MonoBehaviour
{
    [Header("Skill Settings")]
    [SerializeField] private float _skillRange = 5f;
    [SerializeField] private int _damage = 50;
    [SerializeField] private int _boltCount = 6;
    [SerializeField] private float _boltInterval = 0.06f;
    [SerializeField] private float _boltHeight = 18f;
    [SerializeField] private float _cooldown = 30f;

    [Header("References")]
    [SerializeField] private GameObject _rangeIndicator;
    [SerializeField] private ParticleSystem _lightningImpactPrefab;
    [SerializeField] private GameObject _lightningBoltPrefab;
    [SerializeField] private Button _skillButton;
    [SerializeField] private Camera _mainCamera;

    [Header("Audio")]
    [SerializeField] private AudioClip _thunderClip;
    [SerializeField] private AudioSource _audioSource;

    [Header("Screen Flash")]
    [SerializeField] private float _flashDuration = 0.15f;
    [SerializeField] private Color _flashColor = new Color(0.4f, 0.6f, 1f, 0.6f);

    [Header("Cooldown Display")]
    [SerializeField] private Text _cooldownText;

    [Header("Camera Shake")]
    [SerializeField] private float _shakeIntensity = 0.4f;
    [SerializeField] private float _shakeDuration = 0.3f;

    [Header("Ground Ring")]
    [SerializeField] private float _ringExpandSpeed = 15f;
    [SerializeField] private float _ringMaxRadius = 8f;
    [SerializeField] private float _ringDuration = 0.8f;

    private bool _skillActive;
    private bool _onCooldown;
    private Vector3 _targetPos;
    private LineRenderer _rangeLR;

    private Vector3 _camOriginalPos;
    private float _shakeTimer;

    private Image _flashImage;
    private float _flashTimer;

    private void Start()
    {
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_rangeIndicator == null)
        {
            var ri = GameObject.Find("RangeIndicator");
            if (ri == null)
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                {
                    if (t.name == "RangeIndicator") { ri = t.gameObject; break; }
                }
            }
            if (ri != null) _rangeIndicator = ri;
        }
        if (_rangeIndicator != null)
        {
            _rangeIndicator.SetActive(false);
            _rangeLR = _rangeIndicator.GetComponent<LineRenderer>();
            if (_rangeLR != null) _rangeLR.useWorldSpace = false;
        }
        if (_skillButton == null)
        {
            var btnObj = GameObject.Find("SkillButton_Lightning");
            if (btnObj != null) _skillButton = btnObj.GetComponent<Button>();
        }
        if (_skillButton != null) _skillButton.onClick.AddListener(OnSkillButtonClicked);

        CreateFlashOverlay();
    }

    private void CreateFlashOverlay()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        var flashObj = new GameObject("ScreenFlash");
        flashObj.transform.SetParent(canvas.transform, false);
        var flashRect = flashObj.AddComponent<RectTransform>();
        flashRect.anchorMin = Vector2.zero;
        flashRect.anchorMax = Vector2.one;
        flashRect.offsetMin = Vector2.zero;
        flashRect.offsetMax = Vector2.zero;

        _flashImage = flashObj.AddComponent<Image>();
        _flashImage.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, 0f);
        _flashImage.raycastTarget = false;
    }

    private void Update()
    {
        if (_skillActive)
        {
            UpdateRangeIndicator();

            if (Input.GetMouseButtonDown(0))
            {
                ReleaseSkill();
            }
            if (Input.GetMouseButtonDown(1))
            {
                CancelSkill();
            }
        }

        // 2D camera shake (XY only)
        if (_shakeTimer > 0f)
        {
            _shakeTimer -= Time.deltaTime;
            float intensity = _shakeIntensity * (_shakeTimer / _shakeDuration);
            Vector2 offset = Random.insideUnitCircle * intensity;
            _mainCamera.transform.position = _camOriginalPos + new Vector3(offset.x, offset.y, 0f);
        }

        // Screen flash
        if (_flashTimer > 0f)
        {
            _flashTimer -= Time.deltaTime;
            float alpha = (_flashTimer / _flashDuration) * _flashColor.a;
            _flashImage.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, alpha);
        }
    }

    private void OnSkillButtonClicked()
    {
        if (_onCooldown) return;
        _skillActive = true;
        if (_rangeIndicator != null) _rangeIndicator.SetActive(true);
    }

    private void UpdateRangeIndicator()
    {
        // 2D: convert mouse to world point on Z=0 plane
        // z must be distance from camera to XY plane, otherwise returns camera position
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = Mathf.Abs(_mainCamera.transform.position.z);
        Vector3 mouseWorld = _mainCamera.ScreenToWorldPoint(mouseScreen);
        mouseWorld.z = 0f;
        _targetPos = mouseWorld;

        if (_rangeIndicator != null)
        {
            _rangeIndicator.transform.position = _targetPos;
        }

        // Pulse range indicator
        if (_rangeLR != null)
        {
            float pulse = 0.6f + 0.4f * Mathf.PingPong(Time.time * 3f, 1f);
            _rangeLR.startColor = new Color(0.3f, 0.6f, 1f, pulse);
            _rangeLR.endColor = new Color(0.3f, 0.6f, 1f, pulse);
        }
    }

    private void ReleaseSkill()
    {
        _skillActive = false;
        if (_rangeIndicator != null) _rangeIndicator.SetActive(false);

        _flashTimer = _flashDuration;
        _camOriginalPos = _mainCamera.transform.position;
        _shakeTimer = _shakeDuration;

        DealDamageToEnemies();
        StartCoroutine(SpawnLightningBolts());
        StartCoroutine(SpawnGroundRing());
        StartCoroutine(CooldownRoutine());
    }

    private void DealDamageToEnemies()
    {
        var hits = Physics2D.OverlapCircleAll(_targetPos, _skillRange);
        foreach (var hit in hits)
        {
            var enemy = hit.GetComponent<Enemy>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(_damage);
            }
        }
    }

    private void CancelSkill()
    {
        _skillActive = false;
        if (_rangeIndicator != null) _rangeIndicator.SetActive(false);
    }

    private IEnumerator SpawnLightningBolts()
    {
        // Impact particle at target
        if (_lightningImpactPrefab != null)
        {
            var impact = Instantiate(_lightningImpactPrefab, _targetPos, Quaternion.identity);
            impact.Play(true);
            Destroy(impact.gameObject, impact.main.duration + 1.5f);
        }

        if (_thunderClip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_thunderClip);
        }

        // Main bolt: origin is above the target on Y axis
        if (_lightningBoltPrefab != null)
        {
            Vector3 mainOrigin = new Vector3(_targetPos.x, _targetPos.y + _boltHeight, 0f);
            var boltObj = Instantiate(_lightningBoltPrefab);
            var bolt = boltObj.GetComponent<LightningBolt>();
            if (bolt != null) bolt.Fire(mainOrigin, _targetPos);
            else Destroy(boltObj);
        }

        yield return new WaitForSeconds(_boltInterval);

        // Surrounding bolts
        for (int i = 0; i < _boltCount - 1; i++)
        {
            // Random offset in 2D circle around target
            Vector2 offset2D = Random.insideUnitCircle * _skillRange * 0.7f;
            Vector3 boltTarget = new Vector3(_targetPos.x + offset2D.x, _targetPos.y + offset2D.y, 0f);
            Vector3 boltOrigin = new Vector3(boltTarget.x + Random.Range(-1f, 1f), boltTarget.y + _boltHeight + Random.Range(-3f, 3f), 0f);

            if (_lightningBoltPrefab != null)
            {
                var boltObj = Instantiate(_lightningBoltPrefab);
                var bolt = boltObj.GetComponent<LightningBolt>();
                if (bolt != null) bolt.Fire(boltOrigin, boltTarget);
                else Destroy(boltObj);
            }

            yield return new WaitForSeconds(_boltInterval);
        }
    }

    private IEnumerator SpawnGroundRing()
    {
        var ringObj = new GameObject("GroundRing");
        ringObj.transform.position = _targetPos;
        var lr = ringObj.AddComponent<LineRenderer>();
        var mat = new Material(Shader.Find("Mobile/Particles/Additive"));
        if (mat != null) lr.material = mat;
        lr.useWorldSpace = true;
        lr.startWidth = 0.3f;
        lr.endWidth = 0.1f;
        lr.loop = true;
        lr.sortingOrder = 1;

        int ringSegments = 48;
        lr.positionCount = ringSegments + 1;
        float elapsed = 0f;

        while (elapsed < _ringDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _ringDuration;
            float radius = Mathf.Lerp(0.5f, _ringMaxRadius, t);
            float alpha = 1f - t;

            lr.startColor = new Color(0.4f, 0.7f, 1f, alpha * 0.8f);
            lr.endColor = new Color(0.3f, 0.5f, 1f, alpha * 0.2f);

            // 2D circle on XY plane
            for (int i = 0; i <= ringSegments; i++)
            {
                float angle = (float)i / ringSegments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                lr.SetPosition(i, new Vector3(_targetPos.x + x, _targetPos.y + y, 0f));
            }

            yield return null;
        }

        Destroy(ringObj);
    }

    private IEnumerator CooldownRoutine()
    {
        _onCooldown = true;
        float remaining = _cooldown;
        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            if (_cooldownText != null)
                _cooldownText.text = Mathf.CeilToInt(remaining).ToString() + "s";
            yield return null;
        }
        if (_cooldownText != null)
            _cooldownText.text = "";
        _onCooldown = false;
    }
}
