using UnityEngine;
using TowerDefense.Core;
using TowerDefense.Data;

namespace Sttop5.Modules.HomeBase
{
    /// <summary>
    /// 挂在 Knight 上，处理点击树木→走过去→自动砍树的完整交互流程。
    /// 与 ClickToMoveController 协同工作：砍树时暂停普通点击移动。
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("砍树参数")]
        [SerializeField] private float _chopRange = 2f;
        [SerializeField] private float _chopDamage = 10f;
        [SerializeField] private float _chopInterval = 0.8f;

        private ClickToMoveController _moveController;
        private CharacterController _cc;
        private Animator _anim;
        private Camera _cam;

        private TreeResource _targetTree;
        private bool _isChopping;
        private float _chopTimer;

        // Animator parameter IDs
        private static readonly int AnimInteract = Animator.StringToHash("Interact");
        private static readonly int AnimSpeed = Animator.StringToHash("Speed");

        public bool IsChopping => _isChopping;

        private void Awake()
        {
            _moveController = GetComponent<ClickToMoveController>();
            _cc = GetComponent<CharacterController>();
            _anim = GetComponent<Animator>();
            // Camera 被 ClickToMoveController 解除了父子关系，用 Camera.main 查找
            _cam = Camera.main;
        }

        private void Update()
        {
            HandleTreeClick();

            if (_isChopping && _targetTree != null)
            {
                UpdateChopping();
            }
        }

        private void HandleTreeClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (_cam == null) return;

            // 点在 UI 上时不处理
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                return;

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return;

            // 通过 Tag 查找可砍伐的树
            if (!hit.collider.CompareTag("Tree")) return;
            TreeResource tree = hit.collider.GetComponentInParent<TreeResource>();
            if (tree == null || tree.IsDead) return;

            // 设置目标树，开始走向它
            _targetTree = tree;
            _isChopping = false;
            _chopTimer = 0f;

            // 让移动控制器走向树
            _moveController.SetInteractionTarget(tree.ChopPosition, _chopRange);
        }

        private void UpdateChopping()
        {
            // 目标树已被砍倒
            if (_targetTree == null || _targetTree.IsDead)
            {
                StopChopping();
                return;
            }

            // 检查距离，太远则走过去
            float dist = Vector3.Distance(transform.position, _targetTree.ChopPosition);
            if (dist > _chopRange + 0.3f)
            {
                // 还在走向树
                _moveController.SetInteractionTarget(_targetTree.ChopPosition, _chopRange);
                return;
            }

            // 面向树
            Vector3 dir = (_targetTree.ChopPosition - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    8f * Time.deltaTime);
            }

            // 开始/继续砍树
            if (!_isChopping)
            {
                _isChopping = true;
                _moveController.PauseMovement();
            }

            _chopTimer += Time.deltaTime;
            if (_chopTimer >= _chopInterval)
            {
                _chopTimer = 0f;
                ChopOnce();
            }
        }

        private void ChopOnce()
        {
            if (_anim != null)
                _anim.SetTrigger(AnimInteract);

            if (_targetTree != null && !_targetTree.IsDead)
            {
                bool stillAlive = _targetTree.TakeChopDamage(_chopDamage);
                if (!stillAlive)
                {
                    // 树倒了，发奖励
                    GiveReward();
                    StopChopping();
                }
            }
        }

        private void GiveReward()
        {
            // 通过 EventBus 发放金币
            var wallet = PlayerWallet.Instance;
            if (wallet != null)
            {
                wallet.AddGold(5);
            }
        }

        private void StopChopping()
        {
            _isChopping = false;
            _targetTree = null;
            _chopTimer = 0f;
            _moveController.ResumeMovement();
        }

        /// <summary>外部取消交互（如点击地面移动时）</summary>
        public void CancelInteraction()
        {
            StopChopping();
        }
    }
}
