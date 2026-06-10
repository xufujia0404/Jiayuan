using UnityEngine;
using System.Collections;
using TowerDefense.Enemy;

namespace TowerDefense.UI
{
    /// <summary>
    /// 伤害飘字：在敌人头顶弹出伤害数字，向上飘动并淡出。
    /// </summary>
    public class DamageText : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private float _duration = 0.8f;
        [SerializeField] private float _floatSpeed = 1.5f;
        [SerializeField] private float _scalePop = 1.3f;
        [SerializeField] private float _baseScale = 0.5f;

        private TextMesh _textMesh;
        private float _timer;

        public static void Spawn(Vector3 worldPos, int damage, DamageType type)
        {
            var obj = new GameObject("DamageText");
            obj.transform.position = worldPos + new Vector3(Random.Range(-0.3f, 0.3f), 0.5f, 0);

            var dt = obj.AddComponent<DamageText>();
            dt.Init(damage, type);
        }

        public static void SpawnImmune(Vector3 worldPos)
        {
            var obj = new GameObject("DamageText");
            obj.transform.position = worldPos + new Vector3(0, 0.5f, 0);

            var dt = obj.AddComponent<DamageText>();
            dt.InitImmune();
        }

        private void Init(int damage, DamageType type)
        {
            _textMesh = gameObject.AddComponent<TextMesh>();
            _textMesh.text = damage.ToString();
            _textMesh.fontSize = 16;
            _textMesh.fontStyle = FontStyle.Bold;
            _textMesh.anchor = TextAnchor.MiddleCenter;
            _textMesh.alignment = TextAlignment.Center;
            _textMesh.color = GetColor(type, damage);
            GetComponent<Renderer>().sortingOrder = 200;
            transform.localScale = Vector3.one * _baseScale * _scalePop;
        }

        private void InitImmune()
        {
            _textMesh = gameObject.AddComponent<TextMesh>();
            _textMesh.text = "免疫";
            _textMesh.fontSize = 12;
            _textMesh.fontStyle = FontStyle.Bold;
            _textMesh.anchor = TextAnchor.MiddleCenter;
            _textMesh.alignment = TextAlignment.Center;
            _textMesh.color = new Color(0.5f, 0.7f, 1f, 1f); // 蓝色
            GetComponent<Renderer>().sortingOrder = 200;

            transform.localScale = Vector3.one * _baseScale;
            _duration = 0.6f;
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            float t = _timer / _duration;

            // 向上飘
            transform.position += Vector3.up * _floatSpeed * Time.deltaTime;

            // 缩放回弹
            if (t < 0.2f)
            {
                float scaleT = t / 0.2f;
                transform.localScale = Vector3.Lerp(Vector3.one * _baseScale * _scalePop, Vector3.one * _baseScale, scaleT);
            }
            else
            {
                transform.localScale = Vector3.one * _baseScale;
            }

            // 淡出
            if (t > 0.5f)
            {
                float fadeT = (t - 0.5f) / 0.5f;
                Color c = _textMesh.color;
                c.a = 1f - fadeT;
                _textMesh.color = c;
            }

            if (_timer >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private Color GetColor(DamageType type, int damage)
        {
            return type switch
            {
                DamageType.Magic => new Color(0.6f, 0.4f, 1f),       // 紫色
                DamageType.Explosion => new Color(1f, 0.5f, 0.1f),    // 橙色
                DamageType.Fire => new Color(1f, 0.3f, 0.1f),         // 红橙
                DamageType.Ice => new Color(0.3f, 0.7f, 1f),         // 冰蓝
                DamageType.Poison => new Color(0.3f, 0.9f, 0.3f),    // 绿色
                _ => new Color(1f, 1f, 1f)                            // 物理白色
            };
        }
    }
}
