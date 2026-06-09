using UnityEngine;
using UnityEditor;
using TowerDefense.Utils;

namespace TowerDefense.Editor
{
    [CustomEditor(typeof(PathCreator))]
    public class PathCreatorEditor : UnityEditor.Editor
    {
        private PathCreator _pathCreator;
        private int _selectedWaypoint = -1;
        private bool _editingPath = false;

        private void OnEnable()
        {
            _pathCreator = (PathCreator)target;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Path Tools", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Waypoint"))
            {
                Vector3 newPos = _pathCreator.WaypointCount > 0
                    ? _pathCreator.GetWaypoint(_pathCreator.WaypointCount - 1) + Vector3.right
                    : _pathCreator.transform.position;
                _pathCreator.AddWaypoint(newPos);
                EditorUtility.SetDirty(_pathCreator);
            }

            if (GUILayout.Button("Clear Path"))
            {
                _pathCreator.ClearPath();
                EditorUtility.SetDirty(_pathCreator);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Smooth Path"))
            {
            }

            if (GUILayout.Button("Reverse Path"))
            {
                EditorUtility.SetDirty(_pathCreator);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Waypoints: {_pathCreator.WaypointCount}");
            EditorGUILayout.LabelField($"Total Distance: {_pathCreator.GetTotalDistance():F2}");

            _editingPath = EditorGUILayout.Toggle("Edit Mode", _editingPath);

            if (_editingPath)
            {
                EditorGUILayout.HelpBox("Click in Scene to add waypoints. Shift+Click to remove.", MessageType.Info);
            }
        }

        private void OnSceneGUI()
        {
            if (!_editingPath) return;

            Event e = Event.current;

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                Vector3 mousePos = ray.origin;
                mousePos.z = 0;

                if (e.shift)
                {
                    if (_pathCreator.WaypointCount > 0)
                    {
                        _pathCreator.RemoveWaypoint(_pathCreator.WaypointCount - 1);
                    }
                }
                else
                {
                    _pathCreator.AddWaypoint(mousePos);
                }

                EditorUtility.SetDirty(_pathCreator);
                e.Use();
            }

            Handles.color = Color.yellow;
            for (int i = 0; i < _pathCreator.WaypointCount; i++)
            {
                Vector3 waypoint = _pathCreator.GetWaypoint(i);

                EditorGUI.BeginChangeCheck();
                var fmh_106_21_639139147708220621 = Quaternion.identity; Vector3 newWaypoint = Handles.FreeMoveHandle(
                    waypoint,
                    0.3f,
                    Vector3.zero,
                    Handles.CylinderHandleCap
                );

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_pathCreator, "Move Waypoint");
                    _pathCreator.MoveWaypoint(i, newWaypoint);
                    EditorUtility.SetDirty(_pathCreator);
                }
            }
        }
    }
}
