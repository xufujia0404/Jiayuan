using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Procedural 2D lightning bolt with branching forks using LineRenderers.
/// All positions on XY plane (Z=0). Branches spread horizontally.
/// </summary>
public class LightningBolt : MonoBehaviour
{
    [Header("Main Bolt")]
    [SerializeField] private int _segments = 12;
    [SerializeField] private float _jitter = 1.5f;
    [SerializeField] private float _duration = 0.45f;
    [SerializeField] private float _width = 0.5f;

    [Header("Branches")]
    [SerializeField] private int _branchCount = 3;
    [SerializeField] private int _branchSegments = 5;
    [SerializeField] private float _branchJitter = 0.8f;
    [SerializeField] private float _branchWidth = 0.2f;
    [SerializeField] private float _branchLength = 0.35f;

    [Header("Colors")]
    [SerializeField] private Color _coreColor = new Color(0.85f, 0.92f, 1f, 1f);
    [SerializeField] private Color _glowColor = new Color(0.4f, 0.6f, 1f, 0.6f);
    [SerializeField] private Color _tipColor = new Color(0.3f, 0.5f, 1f, 0.1f);

    private LineRenderer _mainLR;
    private List<LineRenderer> _branchLRs = new List<LineRenderer>();
    private float _timer;
    private Vector3 _origin;
    private Vector3 _target;

    public void Fire(Vector3 from, Vector3 to)
    {
        _origin = from;
        _target = to;

        // Main bolt
        _mainLR = CreateLineRenderer("MainBolt", _width, _width * 0.25f, _coreColor, _tipColor);
        GeneratePath2D(_mainLR, from, to, _segments, _jitter);

        // Branches
        for (int i = 0; i < _branchCount; i++)
        {
            float t = Random.Range(0.2f, 0.7f);
            Vector3 branchRoot = Vector3.Lerp(from, to, t);
            // Perpendicular in 2D: rotate direction 90 degrees
            Vector2 dir2D = (to - from);
            Vector2 perp2D = new Vector2(-dir2D.y, dir2D.x).normalized;
            branchRoot += (Vector3)(perp2D * Random.Range(-_jitter, _jitter) * 0.5f);

            // Branch end: spread sideways and slightly toward target direction
            Vector3 branchEnd = branchRoot + (Vector3)(perp2D * Random.Range(-1f, 1f) + new Vector2(dir2D.x * 0.2f, dir2D.y * 0.1f).normalized) * Vector3.Distance(from, to) * _branchLength;

            var branchLR = CreateLineRenderer("Branch_" + i, _branchWidth, _branchWidth * 0.2f, _glowColor, _tipColor);
            GeneratePath2D(branchLR, branchRoot, branchEnd, _branchSegments, _branchJitter);
            _branchLRs.Add(branchLR);
        }

        _timer = _duration;
        StartCoroutine(AnimateBolt());
    }

    private LineRenderer CreateLineRenderer(string name, float startWidth, float endWidth, Color startColor, Color endColor)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(transform);
        var lr = obj.AddComponent<LineRenderer>();
        var mat = new Material(Shader.Find("Mobile/Particles/Additive"));
        if (mat == null) mat = new Material(Shader.Find("Unlit/Color"));
        lr.material = mat;
        lr.startWidth = startWidth;
        lr.endWidth = endWidth;
        lr.startColor = startColor;
        lr.endColor = endColor;
        lr.useWorldSpace = true;
        lr.sortingOrder = 10; // Render on top of sprites
        lr.positionCount = 0;
        return lr;
    }

    private void GeneratePath2D(LineRenderer lr, Vector3 from, Vector3 to, int segments, float jitter)
    {
        lr.positionCount = segments + 1;
        // 2D perpendicular: swap (dx,dy) -> (-dy, dx)
        Vector2 dir = to - from;
        Vector2 perp = new Vector2(-dir.y, dir.x).normalized;
        if (perp == Vector2.zero) perp = Vector2.right;

        lr.SetPosition(0, from);
        for (int i = 1; i < segments; i++)
        {
            float t = (float)i / segments;
            Vector3 pos = Vector3.Lerp(from, to, t);
            pos += (Vector3)(perp * Random.Range(-jitter, jitter));
            pos.z = 0f; // Stay on Z=0
            lr.SetPosition(i, pos);
        }
        lr.SetPosition(segments, to);
    }

    private IEnumerator AnimateBolt()
    {
        while (_timer > 0f)
        {
            _timer -= Time.deltaTime;
            float alpha = Mathf.Clamp01(_timer / _duration);

            // Main bolt flicker
            _mainLR.startColor = new Color(_coreColor.r, _coreColor.g, _coreColor.b, alpha);
            _mainLR.endColor = new Color(_tipColor.r, _tipColor.g, _tipColor.b, alpha * 0.3f);

            // Re-jitter for flicker effect
            GeneratePath2D(_mainLR, _origin, _target, _segments, _jitter * alpha);

            // Branches fade faster
            foreach (var branch in _branchLRs)
            {
                branch.startColor = new Color(_glowColor.r, _glowColor.g, _glowColor.b, alpha * 0.6f);
                branch.endColor = new Color(_tipColor.r, _tipColor.g, _tipColor.b, alpha * 0.1f);
            }

            yield return null;
        }
        Destroy(gameObject);
    }
}
