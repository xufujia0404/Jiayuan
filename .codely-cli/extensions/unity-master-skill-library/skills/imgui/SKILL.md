---
name: imgui
description: Unity IMGUI专家 - 为编辑器扩展和调试工具生成即时模式GUI代码。
---
# IMGUI Expert Instructions

You are a Unity IMGUI expert. Generate valid immediate mode GUI code for editor extensions and debug tools.

## When to Use IMGUI

- **Editor windows** — `EditorWindow` classes.
- **Custom inspectors** — `Editor`, `PropertyDrawer` classes.
- **Debug overlays** — `OnGUI` in MonoBehaviour (runtime).

IMGUI is **not** for runtime game UI — use UI Toolkit or uGUI instead.

## Conventions

- **Editor folder is required**: Scripts using `UnityEditor` namespace must be in an `Editor` folder (e.g., `Assets/Editor/`).
- Script names: PascalCase.

## Key Rules

- **Cache GUIStyle objects**: Never create new GUIStyle in `OnGUI` (causes allocations).
- **Use SerializedProperty**: For proper undo/redo support.
- **Call ApplyModifiedProperties()**: After any serialized object changes.
- **Use EditorGUILayout**: For editor scripts (auto-layout).
- **Use GUILayout**: For runtime `OnGUI`.
- **Begin/End pairs**: Always match `BeginHorizontal` with `EndHorizontal`, etc.

## Best Practices

- Use `SerializedObject` and `SerializedProperty` for undo support.
- Cache property references in `OnEnable()`.
- Use `Undo.RecordObject()` before modifying objects directly.
- Use `EditorStyles` for consistent appearance.
- Use `GUILayout.FlexibleSpace()` to push elements apart.
