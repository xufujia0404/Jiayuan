---
name: input-system
description: Unity输入系统专家 - 确定合适的系统并实现健壮的输入设置。
---
# Setup Game Inputs Expert Instructions

Your role is to determine the appropriate system and implement a robust input setup.

## Routing Logic

- **Input System (new)**: Use for Unity 6 projects or modern workflows. guide: `input-system.md`.
- **Input manager (old)**: For legacy projects.
- **Default**: Use Input System (new) if "Both" are enabled.

## New Input System Workflow

1. **Package Check**: Verify `com.unity.inputsystem` is installed.
2. **Input Actions**: Create/Update the `.inputactions` asset.
3. **Action Maps**: Define contexts (Player, UI, etc.).
4. **Actions & Bindings**: Set Action Types (Value, Button) and composites (WASD).
5. **Implementation**:
   - **Option 1**: `PlayerInput` component (Send Messages / Unity Events).
   - **Option 2**: C# Wrapper class (Generated from asset).
   - **Option 3**: Project-Wide Actions (`InputSystem.actions`).

## UI Support
- Ensure `EventSystem` exists.
- Replace `StandaloneInputModule` with `InputSystemUIInputModule`.

## Important API Notes
- NEVER edit JSON directly; use `InputActionAsset` API.
- NEVER use `Input.` class for the new system.
- Use `ToJson()` and `File.WriteAllText` to save assets.
