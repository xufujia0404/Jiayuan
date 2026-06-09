using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense.UI
{
    public class MovingClouds : MonoBehaviour
    {
        [Header("Cloud Settings")]
        [SerializeField] private int _cloudCount = 8;
        [SerializeField] private float _minSpeed = 25f;
        [SerializeField] private float _maxSpeed = 60f;
        [SerializeField] private float _minScale = 0.5f;
        [SerializeField] private float _maxScale = 1.6f;
        [SerializeField] private float _cloudAlpha = 0.55f;

        [Header("Spawn Range (UI coordinates)")]
        [SerializeField] private float _spawnY = 250f;
        [SerializeField] private float _yRandomRange = 350f;
        [SerializeField] private int _cloudSortOrder = -5;

        public bool IsPaused { get; set; }

        private RectTransform _canvasRect;
        private float _canvasWidth;
        private float _canvasHeight;

        private void Start()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                _canvasRect = canvas.GetComponent<RectTransform>();
                _canvasWidth = _canvasRect.rect.width;
                _canvasHeight = _canvasRect.rect.height;
            }
            else
            {
                _canvasWidth = 1920f;
                _canvasHeight = 1080f;
            }

            for (int i = 0; i < _cloudCount; i++)
            {
                CreateCloud(randomX: true);
            }
        }

        private void CreateCloud(bool randomX = false)
        {
            GameObject cloudObj = new GameObject("Cloud");
            cloudObj.transform.SetParent(transform, false);

            RectTransform rt = cloudObj.AddComponent<RectTransform>();

            Canvas cloudCanvas = cloudObj.AddComponent<Canvas>();
            cloudCanvas.overrideSorting = true;
            cloudCanvas.sortingOrder = _cloudSortOrder;

            Image cloudImg = cloudObj.AddComponent<Image>();

            Texture2D tex = GenerateCloudTexture();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            cloudImg.sprite = sprite;
            cloudImg.color = new Color(1f, 1f, 1f, _cloudAlpha);
            cloudImg.raycastTarget = false;

            float scale = Random.Range(_minScale, _maxScale);
            rt.localScale = new Vector3(scale, scale, 1f);

            float startX = randomX ? Random.Range(-_canvasWidth / 2, _canvasWidth / 2) : _canvasWidth / 2 + 200f;
            float startY = _spawnY + Random.Range(-_yRandomRange, _yRandomRange);
            rt.anchoredPosition = new Vector2(startX, startY);

            CloudMover mover = cloudObj.AddComponent<CloudMover>();
            float speed = Random.Range(_minSpeed, _maxSpeed);
            mover.Initialize(speed, _canvasWidth, rt, this);
        }

        private Texture2D GenerateCloudTexture()
        {
            int w = 256;
            int h = 128;
            Texture2D tex = new Texture2D(w, h);
            Color[] pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float nx = (float)x / w;
                    float ny = (float)y / h;

                    float d = Ellipse(nx, ny, 0.5f, 0.45f, 0.4f, 0.35f);
                    d = Mathf.Max(d, Ellipse(nx, ny, 0.3f, 0.55f, 0.22f, 0.25f));
                    d = Mathf.Max(d, Ellipse(nx, ny, 0.7f, 0.55f, 0.22f, 0.25f));
                    d = Mathf.Max(d, Ellipse(nx, ny, 0.5f, 0.6f, 0.28f, 0.2f));
                    d = Mathf.Max(d, Ellipse(nx, ny, 0.4f, 0.7f, 0.15f, 0.12f));
                    d = Mathf.Max(d, Ellipse(nx, ny, 0.6f, 0.7f, 0.15f, 0.12f));

                    float alpha = Mathf.SmoothStep(0f, 1f, d);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private float Ellipse(float nx, float ny, float cx, float cy, float rx, float ry)
        {
            float dx = (nx - cx) / rx;
            float dy = (ny - cy) / ry;
            return 1f - (dx * dx + dy * dy);
        }

        internal void OnCloudExitedScreen(CloudMover mover)
        {
            CreateCloud(randomX: false);
            Destroy(mover.gameObject);
        }

        internal class CloudMover : MonoBehaviour
        {
            private float _speed;
            private float _canvasWidth;
            private RectTransform _rt;
            private MovingClouds _owner;

            public void Initialize(float speed, float canvasWidth, RectTransform rt, MovingClouds owner)
            {
                _speed = speed;
                _canvasWidth = canvasWidth;
                _rt = rt;
                _owner = owner;
            }

            private void Update()
            {
                if (_rt == null || _owner.IsPaused) return;

                Vector2 pos = _rt.anchoredPosition;
                pos.x -= _speed * Time.deltaTime;
                _rt.anchoredPosition = pos;

                if (pos.x < -_canvasWidth / 2 - 300f)
                {
                    _owner.OnCloudExitedScreen(this);
                }
            }
        }
    }
}
