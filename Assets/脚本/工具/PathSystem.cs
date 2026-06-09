using UnityEngine;
using System.Collections.Generic;

public class PathSystem : MonoBehaviour
{
    [Header("Path Settings")]
    [SerializeField] private Transform[] _waypoints;
    [SerializeField] private bool _isClosedLoop = false;
    [SerializeField] private Color _pathColor = Color.cyan;
    
    public int WaypointCount => _waypoints.Length;
    
    private void Awake()
    {
        if (_waypoints == null || _waypoints.Length == 0)
        {
            Debug.LogWarning("No waypoints assigned to PathSystem!");
        }
    }
    
    public Vector3 GetWaypoint(int index)
    {
        if (_waypoints == null || index < 0 || index >= _waypoints.Length)
        {
            return Vector3.zero;
        }
        return _waypoints[index].position;
    }
    
    public Vector3[] GetAllWaypoints()
    {
        if (_waypoints == null) return new Vector3[0];
        
        Vector3[] points = new Vector3[_waypoints.Length];
        for (int i = 0; i < _waypoints.Length; i++)
        {
            points[i] = _waypoints[i].position;
        }
        return points;
    }
    
    public Vector3 GetStartPoint()
    {
        return GetWaypoint(0);
    }
    
    public Vector3 GetEndPoint()
    {
        return GetWaypoint(_waypoints.Length - 1);
    }
    
    private void OnDrawGizmos()
    {
        if (_waypoints == null || _waypoints.Length < 2) return;
        
        Gizmos.color = _pathColor;
        
        for (int i = 0; i < _waypoints.Length - 1; i++)
        {
            Gizmos.DrawLine(_waypoints[i].position, _waypoints[i + 1].position);
        }
        
        if (_isClosedLoop && _waypoints.Length > 1)
        {
            Gizmos.DrawLine(_waypoints[_waypoints.Length - 1].position, _waypoints[0].position);
        }
        
        Gizmos.color = Color.red;
        foreach (var waypoint in _waypoints)
        {
            Gizmos.DrawSphere(waypoint.position, 0.2f);
        }
    }
}