# 2D Character Wiring and Physics

## 1. Pixel-Perfect Collider Fitting
AI sprites often have padding/offset issues.
- Temporarily set `isReadable = true`.
- Find bounds of non-transparent pixels (alpha > 10).
- Calculate **World Size** and **Pivot Offset**.
- Apply to `CapsuleCollider2D.size` and `.offset`.
- Re-position `GroundCheck` child at collider bottom.

## 2. Animator Setup
- Use `AnimatorController.CreateAnimatorControllerAtPath`.
- Use `EditorCurveBinding` for sprite properties.
- Set `hasExitTime = false` for responsive transitions.

## 3. Movement Controller (Unity 6+)
- Use `Rigidbody2D.linearVelocity` (replaces legacy `velocity`).
- Handle ground checks via `Physics2D.OverlapCircle`.
- Support both Input Systems via compiler defines.
