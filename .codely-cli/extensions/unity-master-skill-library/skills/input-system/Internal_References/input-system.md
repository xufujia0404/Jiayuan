# Input System Reference

## Setup
- Verify `com.unity.inputsystem` is installed.
- Check "Active Input Handling" in Project Settings.

## Workflow
1. Create `.inputactions` asset.
2. Define Action Maps (e.g., Player, UI).
3. Add Actions (Value, Button) and Bindings (WASD).
4. **Option A**: Use `PlayerInput` component (Send Messages / Unity Events).
5. **Option B**: Generate C# Wrapper class.

## Critical APIs
- Use `InputActionAsset` API to edit (AddAction, AddBinding).
- Use `ToJson()` and `File.WriteAllText` to save.
- NEVER use legacy `Input` class for the new system.
