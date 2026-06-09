# Input System (New) Guide

## Asset Setup
- **Create**: `InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();`
- **Map**: `var map = asset.AddActionMap("Player");`
- **Action**: `var moveAction = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");`
- **Binding**: `moveAction.AddCompositeBinding("2DVector").AddBinding("<Keyboard>/w", "up")...`

## PlayerInput Component
- **Notification Behaviors**:
    - **SendMessages**: Calls `OnMove`, `OnJump` on the same GameObject.
    - **InvokeCSharpEvents**: Use `playerInput.onActionTriggered`.
    - **InvokeUnityEvents**: Wire up via the Inspector.

## Scripting (Generated Wrapper)
```csharp
private GameInputs controls;
void Awake() => controls = new GameInputs();
void OnEnable() => controls.Player.Enable();
void OnDisable() => controls.Player.Disable();

void Update() {
    Vector2 move = controls.Player.Move.ReadValue<Vector2>();
}
```

## Troubleshooting
- **Greyed out input**: Ensure the Action Map is Enabled (`map.Enable()`).
- **Conflict**: Remove `StandaloneInputModule` from the `EventSystem`.
- **Active Handling**: Ensure "Both" or "Input System Package (New)" is selected in Project Settings.
