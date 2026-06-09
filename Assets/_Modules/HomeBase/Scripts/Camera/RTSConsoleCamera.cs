using UnityEngine;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 俯视策略摄像机控制（纯鼠标）：中键拖拽平移、右键拖拽旋转、滚轮缩放、边缘滚动。
    /// </summary>
    public class RTSConsoleCamera : MonoBehaviour
    {
        [Header("移动")]
        [SerializeField] private float _panSpeed = 1f;
        [SerializeField] private float _edgeScrollMargin = 20f;
        [SerializeField] private float _edgeScrollSpeed = 30f;

        [Header("旋转")]
        [SerializeField] private float _rotateSpeed = 100f;
        [SerializeField] private float _minPitch = 10f;
        [SerializeField] private float _maxPitch = 80f;

        [Header("缩放")]
        [SerializeField] private float _zoomSpeed = 60f;
        [SerializeField] private float _minHeight = 8f;
        [SerializeField] private float _maxHeight = 120f;
        [SerializeField] private float _groundOffset = 5f;

        [Header("平滑")]
        [SerializeField] private float _smoothSpeed = 8f;

        [Header("限制")]
        [SerializeField] private Vector2 _boundMin = new Vector2(-900f, -900f);
        [SerializeField] private Vector2 _boundMax = new Vector2(900f, 900f);

        private float _currentPitch;
        private float _currentYaw;
        private float _targetHeight;
        private Vector3 _pivotPoint;
        private bool _isRightDragging;
        private bool _isMiddleDragging;
        private Terrain _terrain;

        private void Start()
        {
            _terrain = Terrain.activeTerrain;

            Vector3 angles = transform.eulerAngles;
            _currentPitch = angles.x;
            _currentYaw = angles.y;

            float groundY = GetTerrainHeight(transform.position);
            Ray ray = new Ray(transform.position, transform.forward);
            Plane ground = new Plane(Vector3.up, new Vector3(0, groundY, 0));
            if (ground.Raycast(ray, out float dist))
                _pivotPoint = ray.GetPoint(dist);
            else
                _pivotPoint = transform.position + transform.forward * 50f;

            _pivotPoint.y = GetTerrainHeight(_pivotPoint);
            _targetHeight = transform.position.y - groundY;
            _targetHeight = Mathf.Clamp(_targetHeight, _minHeight, _maxHeight);
            ApplyToTransform();
        }

        private void LateUpdate()
        {
            HandleMiddleClickPan();
            HandleEdgeScroll();
            HandleRotation();
            HandleZoom();
            ClampPivot();
            ApplyToTransform();
        }

        #region 移动

        private void HandleMiddleClickPan()
        {
            if (Input.GetMouseButtonDown(2))
            {
                _isMiddleDragging = true;
            }
            if (Input.GetMouseButtonUp(2))
            {
                _isMiddleDragging = false;
            }

            if (!_isMiddleDragging) return;

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // 在摄像机 yaw 方向上平移
            Quaternion yawRot = Quaternion.Euler(0, _currentYaw, 0);
            Vector3 move = yawRot * new Vector3(-mouseX, 0, -mouseY) * _panSpeed;
            _pivotPoint += move;
        }

        private void HandleEdgeScroll()
        {
            if (_isRightDragging || _isMiddleDragging) return;

            Vector3 move = Vector3.zero;
            Vector3 mouse = Input.mousePosition;

            if (mouse.x <= _edgeScrollMargin && mouse.x >= 0) move += Vector3.left;
            if (mouse.x >= Screen.width - _edgeScrollMargin && mouse.x <= Screen.width) move += Vector3.right;
            if (mouse.y <= _edgeScrollMargin && mouse.y >= 0) move += Vector3.back;
            if (mouse.y >= Screen.height - _edgeScrollMargin && mouse.y <= Screen.height) move += Vector3.forward;

            if (move.sqrMagnitude > 0.01f)
            {
                Quaternion yawRot = Quaternion.Euler(0, _currentYaw, 0);
                _pivotPoint += yawRot * move.normalized * (_edgeScrollSpeed * Time.deltaTime);
            }
        }

        #endregion

        #region 旋转

        private void HandleRotation()
        {
            if (Input.GetMouseButtonDown(1))
            {
                _isRightDragging = true;
                UpdatePivotFromView();
            }
            if (Input.GetMouseButtonUp(1)) _isRightDragging = false;

            if (!_isRightDragging) return;

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            _currentYaw += mouseX * _rotateSpeed * Time.deltaTime;
            _currentPitch -= mouseY * _rotateSpeed * Time.deltaTime;
            _currentPitch = Mathf.Clamp(_currentPitch, _minPitch, _maxPitch);
        }

        private void UpdatePivotFromView()
        {
            float groundY = GetTerrainHeight(transform.position);
            Ray ray = new Ray(transform.position, transform.forward);
            Plane ground = new Plane(Vector3.up, new Vector3(0, groundY, 0));
            if (ground.Raycast(ray, out float dist))
                _pivotPoint = ray.GetPoint(dist);
            _pivotPoint.y = GetTerrainHeight(_pivotPoint);
        }

        #endregion

        #region 缩放

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _targetHeight -= scroll * _zoomSpeed;
                _targetHeight = Mathf.Clamp(_targetHeight, _minHeight, _maxHeight);
            }
        }

        #endregion

        private void ClampPivot()
        {
            _pivotPoint.x = Mathf.Clamp(_pivotPoint.x, _boundMin.x, _boundMax.x);
            _pivotPoint.z = Mathf.Clamp(_pivotPoint.z, _boundMin.y, _boundMax.y);
            _pivotPoint.y = GetTerrainHeight(_pivotPoint);
        }

        private void ApplyToTransform()
        {
            float pitchRad = _currentPitch * Mathf.Deg2Rad;
            float distance = _targetHeight / Mathf.Max(Mathf.Sin(pitchRad), 0.1f);

            Quaternion rot = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 offset = rot * Vector3.back * distance;
            Vector3 targetPos = _pivotPoint + offset;

            float groundY = GetTerrainHeight(targetPos);
            targetPos.y = Mathf.Max(targetPos.y, groundY + _groundOffset);

            transform.position = Vector3.Lerp(transform.position, targetPos, _smoothSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, _smoothSpeed * Time.deltaTime);
        }

        private float GetTerrainHeight(Vector3 worldPos)
        {
            if (_terrain != null)
            {
                Vector3 terrainLocal = worldPos - _terrain.transform.position;
                Vector3 normalized = new Vector3(
                    terrainLocal.x / _terrain.terrainData.size.x,
                    0f,
                    terrainLocal.z / _terrain.terrainData.size.z);

                if (normalized.x >= 0f && normalized.x <= 1f && normalized.z >= 0f && normalized.z <= 1f)
                    return _terrain.SampleHeight(worldPos) + _terrain.transform.position.y;
            }
            return 0f;
        }
    }
}
