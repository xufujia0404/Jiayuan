## Important 3.1 API Notes
* **Namespace:** `Unity.Cinemachine` is primary.
* **Splines:** Uses `UnityEngine.Splines`.
* **Targeting (CRITICAL):**
  * DO NOT use `SetComponentProperty` for targets.
  * MUST USE: `Unity.RunCommand` to set `.Follow` and `.LookAt` properties via code.
* **2D Rule**: For stable 2D, set `LookAt = null;`. Use only `Follow`.
* **Depth (Grey Screen Fix)**:
    * 2D projects require a Z-offset (typically `-10`) in `CinemachineFollow`.
* **Tracking Modes**:
    * Binding modes are in `Unity.Cinemachine.TargetTracking.BindingMode`.
