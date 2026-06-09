using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense.Utils
{
    public class PathCreator : MonoBehaviour
    {
        [Header("Path Settings")]
        [SerializeField] private List<Vector3> _waypoints = new List<Vector3>();
        [SerializeField] private Color _pathColor = Color.red;
        [SerializeField] private float _waypointRadius = 0.3f;
        [SerializeField] private bool _showPath = true;

        [Header("Editing")]
        [SerializeField] private bool _editingMode = false;
        [SerializeField] private int _selectedWaypoint = -1;

        public List<Vector3> Waypoints => _waypoints;
        public int WaypointCount => _waypoints.Count;

        public void AddWaypoint(Vector3 position)
        {
            _waypoints.Add(position);
        }

        public void AddWaypointAtMouse()
        {
            Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0;
            AddWaypoint(mousePos);
        }

        public void RemoveWaypoint(int index)
        {
            if (index >= 0 && index < _waypoints.Count)
            {
                _waypoints.RemoveAt(index);
            }
        }

        public void ClearPath()
        {
            _waypoints.Clear();
        }

        public void MoveWaypoint(int index, Vector3 newPosition)
        {
            if (index >= 0 && index < _waypoints.Count)
            {
                _waypoints[index] = newPosition;
            }
        }

        public void InsertWaypoint(int index, Vector3 position)
        {
            if (index >= 0 && index <= _waypoints.Count)
            {
                _waypoints.Insert(index, position);
            }
        }

        public Vector3 GetWaypoint(int index)
        {
            if (index >= 0 && index < _waypoints.Count)
            {
                return _waypoints[index];
            }
            return Vector3.zero;
        }

        public float GetTotalDistance()
        {
            float distance = 0f;
            for (int i = 1; i < _waypoints.Count; i++)
            {
                distance += Vector3.Distance(_waypoints[i - 1], _waypoints[i]);
            }
            return distance;
        }

        public List<Vector3> GetSmoothPath(int subdivisions = 10)
        {
            if (_waypoints.Count < 2) return new List<Vector3>(_waypoints);

            List<Vector3> smoothPath = new List<Vector3>();

            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                Vector3 start = _waypoints[i];
                Vector3 end = _waypoints[i + 1];

                for (int j = 0; j < subdivisions; j++)
                {
                    float t = j / (float)subdivisions;
                    smoothPath.Add(Vector3.Lerp(start, end, t));
                }
            }

            smoothPath.Add(_waypoints[_waypoints.Count - 1]);

            return smoothPath;
        }

        private void OnDrawGizmos()
        {
            if (!_showPath || _waypoints.Count == 0) return;

            Gizmos.color = _pathColor;

            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                Gizmos.DrawLine(_waypoints[i], _waypoints[i + 1]);
            }

            for (int i = 0; i < _waypoints.Count; i++)
            {
                Gizmos.color = i == _selectedWaypoint ? Color.yellow : _pathColor;
                Gizmos.DrawWireSphere(_waypoints[i], _waypointRadius);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(_waypoints[i] + Vector3.up * 0.5f, i.ToString());
#endif
            }

            if (_waypoints.Count > 0)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(_waypoints[0], _waypointRadius * 1.2f);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(_waypoints[_waypoints.Count - 1], _waypointRadius * 1.2f);
            }
        }
    }
}
