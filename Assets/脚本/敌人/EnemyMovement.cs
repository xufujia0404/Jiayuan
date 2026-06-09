using UnityEngine;
using TowerDefense.Utils;

namespace TowerDefense.Enemy
{
    public class EnemyMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float _speed = 2f;
        [SerializeField] private float _rotationSpeed = 5f;
        
        [Header("Rotation Settings")]
        [Tooltip("旋转模式：None=不旋转, FlipX=只左右翻转, Full=全方向旋转")]
        [SerializeField] private RotationMode _rotationMode = RotationMode.Full;
        
        public enum RotationMode
        {
            None,       
            FlipX,      
            Full        
        }
        
        private PathCreator _currentPath;
        private int _currentWaypointIndex;
        private Vector3[] _waypoints;
        private bool _isMoving = false;
        private float _currentSpeed = 0f;
        
        public bool IsMoving => _isMoving;
        public float CurrentSpeed => _currentSpeed;
        
        private void Update()
        {
            if (_isMoving && _waypoints != null && _waypoints.Length > 0)
            {
                MoveTowardsWaypoint();
            }
        }
        
        public void FollowPath(PathCreator pathCreator)
        {
            _currentPath = pathCreator;
            _waypoints = pathCreator.Waypoints.ToArray();
            _currentWaypointIndex = 0;
            _isMoving = true;
            
            if (_waypoints.Length > 0)
            {
                transform.position = _waypoints[0];
            }
        }
        
        /// <summary>
        /// Resume movement from current position without resetting to path start.
        /// Used when soldiers release blocked enemies.
        /// </summary>
        public void ResumeMovement()
        {
            if (_waypoints != null && _waypoints.Length > 0)
            {
                _isMoving = true;
            }
        }
        
        private void MoveTowardsWaypoint()
        {
            if (_currentWaypointIndex >= _waypoints.Length)
            {
                _isMoving = false;
                _currentSpeed = 0f;
                SendMessage("OnReachedEnd", SendMessageOptions.DontRequireReceiver);
                return;
            }
            
            Vector3 target = _waypoints[_currentWaypointIndex];
            target.z = transform.position.z;
            
            Vector3 direction = (target - transform.position).normalized;
            _currentSpeed = _speed;
            float step = _speed * Time.deltaTime;
            Vector3 newPosition = Vector3.MoveTowards(transform.position, target, step);
            transform.position = newPosition;
            
            // 根据旋转模式处理朝向
            if (direction.magnitude > 0.01f)
            {
                switch (_rotationMode)
                {
                    case RotationMode.Full:
                        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                        Quaternion targetRotation = Quaternion.AngleAxis(angle, Vector3.forward);
                        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
                        break;
                        
                    case RotationMode.FlipX:
                        Vector3 localScale = transform.localScale;
                        if (direction.x > 0.01f)
                        {
                            localScale.x = Mathf.Abs(localScale.x);  
                        }
                        else if (direction.x < -0.01f)
                        {
                            localScale.x = -Mathf.Abs(localScale.x); 
                        }
                        transform.localScale = localScale;
                        break;
                        
                    case RotationMode.None:
                    default:
                        break;
                }
            }
            
            // 检查是否到达当前路径点
            if (Vector3.Distance(transform.position, target) < 0.15f)
            {
                _currentWaypointIndex++;
            }
        }
        
        public void SetSpeed(float speed)
        {
            _speed = speed;
        }
        
        public float GetSpeed()
        {
            return _speed;
        }
        
        public void StopMovement()
        {
            _isMoving = false;
            _currentSpeed = 0f;
        }
        
        public float GetProgressPercent()
        {
            if (_waypoints == null || _waypoints.Length < 2) return 0f;
            
            float totalDistance = 0f;
            for (int i = 0; i < _waypoints.Length - 1; i++)
            {
                totalDistance += Vector3.Distance(_waypoints[i], _waypoints[i + 1]);
            }
            
            float currentDistance = 0f;
            for (int i = 0; i < _currentWaypointIndex; i++)
            {
                currentDistance += Vector3.Distance(_waypoints[i], _waypoints[i + 1]);
            }
            
            if (_currentWaypointIndex < _waypoints.Length - 1)
            {
                currentDistance += Vector3.Distance(transform.position, _waypoints[_currentWaypointIndex]);
            }
            
            return Mathf.Clamp01(currentDistance / totalDistance);
        }
        
        public void Slow(float slowFactor, float duration)
        {
            StartCoroutine(SlowCoroutine(slowFactor, duration));
        }
        
        private System.Collections.IEnumerator SlowCoroutine(float slowFactor, float duration)
        {
            float originalSpeed = _speed;
            _speed *= slowFactor;
            
            yield return new WaitForSeconds(duration);
            
            _speed = originalSpeed;
        }
        
        private void OnDrawGizmos()
        {
            if (_waypoints != null)
            {
                for (int i = _currentWaypointIndex; i < _waypoints.Length - 1; i++)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(_waypoints[i], _waypoints[i + 1]);
                }
            }
        }
    }
}