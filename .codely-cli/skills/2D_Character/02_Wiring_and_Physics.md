# 2D Character Wiring and Physics

## 1. Pixel-Perfect Collider Fitting
AI-generated sprites often have large transparent margins and off-center characters. Standard `bounds.size` fitting will fail.

**The Workflow:**
1. Temporarily set `isReadable = true` on the texture.
2. Scan `GetPixels32()` for the bounding box of non-transparent pixels (alpha > 10).
3. Calculate the **World Size** and the **Offset** from the pivot to the visual center.
4. Apply these to the `CapsuleCollider2D.size` and `CapsuleCollider2D.offset`.
5. Update the `GroundCheck` child position based on the new collider bottom.

## 2. Animator Controller Setup
Use the `UnityEditor.Animations` namespace.

**Correct APIs:**
- `AnimatorController.CreateAnimatorControllerAtPath(path)`
- `EditorCurveBinding` for sprite curves.
- `AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes)`

**State Machine Logic:**
- Create parameters: `IsRunning` (Bool), `IsGrounded` (Bool).
- Transitions:
    - **Idle <-> Run**: Based on `IsRunning`.
    - **Any -> Jump**: Based on `!IsGrounded`.
    - **Jump -> Land**: Based on `IsGrounded`.
- Set `hasExitTime = false` for responsive gameplay.

## 3. Movement Controller (Unity 6+)
Use `Rigidbody2D.linearVelocity` (replaces legacy `velocity`).

**Key Features:**
- **Input Handling**: Support both Legacy and New Input System via `#if` defines.
- **Ground Detection**: Use `Physics2D.OverlapCircle` at the `GroundCheck` position.
- **Sprite Flipping**: Toggle `SpriteRenderer.flipX` based on move direction.
- **Damping**: Adjust gravity scale and jump force for a "snappy" feel.
