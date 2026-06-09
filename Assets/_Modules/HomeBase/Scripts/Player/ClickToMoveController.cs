using UnityEngine;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 鼠标点地移动角色控制器：左键点击地面移动、右键拖拽旋转视角、滚轮缩放。
    /// 自动驱动 Animator 参数（Speed、IsGrounded、Hit、Die、Interact）。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ClickToMoveController : MonoBehaviour
    {
        [Header("移动")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _rotationSpeed = 8f;
        [SerializeField] private float _stopDistance = 0.3f;

        [Header("摄像机")]
        [SerializeField] private float _orbitSpeed = 150f;
        [SerializeField] private float _zoomSpeed = 5f;
        [SerializeField] private float _minZoomDist = 4f;
        [SerializeField] private float _maxZoomDist = 20f;

        private CharacterController _cc;
        private Animator _anim;
        private Camera _cam;
        private Vector3 _targetPos;
        private bool _hasTarget;
        private float _currentYaw;
        private float _camDistance;
        private bool _isRightDragging;
        private int _groundLayerMask;
        private float _currentSpeed;
        private bool _wasGrounded;
        private bool _isPaused;

        // 交互目标
        private Vector3 _interactionTarget;
        private float _interactionStopDist;
        private bool _hasInteractionTarget;

        private float _pitch = 35f;

        // Animator parameter IDs
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int AnimHit = Animator.StringToHash("Hit");
        private static readonly int AnimDie = Animator.StringToHash("Die");
        private static readonly int AnimInteract = Animator.StringToHash("Interact");

        public bool IsMoving => _currentSpeed > 0.1f;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _anim = GetComponent<Animator>();
            _cam = GetComponentInChildren<Camera>();
            _targetPos = transform.position;
            _currentYaw = transform.eulerAngles.y;
            _groundLayerMask = LayerMask.GetMask("Default");

            if (_cam != null)
            {
                _cam.transform.SetParent(null);
                _pitch = Mathf.Clamp(_pitch, 10f, 80f);
                _camDistance = Mathf.Clamp(12f, _minZoomDist, _maxZoomDist);
            }
        }

        private void Update()
        {
            if (!_isPaused)
            {
                HandleClickMove();
                MoveCharacter();
            }
            else
            {
                // 暂停时仍施加重力
                _cc.Move(new Vector3(0, -9.8f, 0) * Time.deltaTime);
                _currentSpeed = 0f;
                _hasTarget = false;
            }

            // 交互目标移动
            if (_hasInteractionTarget)
            {
                MoveToInteractionTarget();
            }

            HandleOrbit();
            HandleZoom();
            UpdateAnimator();
            UpdateCamera();
        }

        #region 移动

        private void HandleClickMove()
        {
            if (Input.GetMouseButtonDown(0) && !_isRightDragging)
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;
                Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out RaycastHit hit, 200f))
                {
                    // 树木由 PlayerInteractor 处理，这里跳过
                    if (hit.collider.CompareTag("Tree"))
                        return;

                    if (hit.collider.CompareTag("Interactable"))
                        return;

                    if (((1 << hit.collider.gameObject.layer) & _groundLayerMask) != 0)
                    {
                        _targetPos = hit.point;
                        _hasTarget = true;
                        _hasInteractionTarget = false;
                    }
                }
            }
        }

        private void MoveCharacter()
        {
            Vector3 dir = _targetPos - transform.position;
            dir.y = 0f;

            if (dir.magnitude < _stopDistance)
            {
                _hasTarget = false;
                _currentSpeed = 0f;
                _cc.Move(new Vector3(0, -9.8f, 0) * Time.deltaTime);
                return;
            }

            // 旋转朝向目标
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);

            // 平滑加速/减速
            _currentSpeed = Mathf.Lerp(_currentSpeed, _moveSpeed, 6f * Time.deltaTime);

            // 移动
            Vector3 motion = dir.normalized * (_currentSpeed * Time.deltaTime);
            motion.y = -9.8f * Time.deltaTime;
            _cc.Move(motion);
        }

        #endregion

        #region 交互移动

        /// <summary>设置交互目标位置和停止距离，角色会自动走向目标</summary>
        public void SetInteractionTarget(Vector3 target, float stopDistance)
        {
            _interactionTarget = target;
            _interactionStopDist = stopDistance;
            _hasInteractionTarget = true;
            _hasTarget = false;
        }

        private void MoveToInteractionTarget()
        {
            Vector3 dir = _interactionTarget - transform.position;
            dir.y = 0f;

            if (dir.magnitude < _interactionStopDist)
            {
                _hasInteractionTarget = false;
                _currentSpeed = 0f;
                return;
            }

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, _rotationSpeed * Time.deltaTime);

            _currentSpeed = Mathf.Lerp(_currentSpeed, _moveSpeed, 6f * Time.deltaTime);

            Vector3 motion = dir.normalized * (_currentSpeed * Time.deltaTime);
            motion.y = -9.8f * Time.deltaTime;
            _cc.Move(motion);
        }

        /// <summary>暂停移动（砍树等交互时调用）</summary>
        public void PauseMovement()
        {
            _isPaused = true;
            _hasTarget = false;
            _hasInteractionTarget = false;
        }

        /// <summary>恢复移动</summary>
        public void ResumeMovement()
        {
            _isPaused = false;
        }

        #endregion

        #region 动画

        private void UpdateAnimator()
        {
            if (_anim == null) return;

            _anim.SetFloat(AnimSpeed, _currentSpeed);

            bool isGrounded = _cc.isGrounded;
            if (isGrounded != _wasGrounded)
                _anim.SetBool(AnimGrounded, isGrounded);
            _wasGrounded = isGrounded;
        }

        /// <summary>播放受击动画</summary>
        public void PlayHit() => _anim?.SetTrigger(AnimHit);

        /// <summary>播放死亡动画</summary>
        public void PlayDie() => _anim?.SetTrigger(AnimDie);

        /// <summary>播放交互动画</summary>
        public void PlayInteract() => _anim?.SetTrigger(AnimInteract);

        #endregion

        #region 摄像机

        private void HandleOrbit()
        {
            if (Input.GetMouseButtonDown(1))
                _isRightDragging = true;
            if (Input.GetMouseButtonUp(1))
                _isRightDragging = false;

            if (!_isRightDragging) return;

            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            _currentYaw += mouseX * _orbitSpeed * Time.deltaTime;
            _pitch -= mouseY * _orbitSpeed * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch, 10f, 80f);
        }

        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _camDistance -= scroll * _zoomSpeed;
                _camDistance = Mathf.Clamp(_camDistance, _minZoomDist, _maxZoomDist);
            }
        }

        private void UpdateCamera()
        {
            if (_cam == null) return;

            float pitchRad = _pitch * Mathf.Deg2Rad;
            float height = _camDistance * Mathf.Sin(pitchRad);
            float back = _camDistance * Mathf.Cos(pitchRad);

            Quaternion yawRot = Quaternion.Euler(0, _currentYaw, 0);
            Vector3 offset = yawRot * new Vector3(0, height, -back);
            Vector3 targetPos = transform.position + offset;

            _cam.transform.position = Vector3.Lerp(_cam.transform.position, targetPos, 8f * Time.deltaTime);
            _cam.transform.LookAt(transform.position + Vector3.up * 1f);
        }

        #endregion
    }
}
