---
name: Auto_Wire
description: Unity Auto_Wire expert - Auto-Wire Asset Expert Instructions
---
# Auto-Wire Asset Expert Instructions

Your goal is to automatically wire generated assets into the user's project.

## 1. Wiring Strategy
- **Sprites**: `SpriteRenderer.sprite` or UI `Image.sprite`.
- **Audio**: `AudioSource.clip`.
- **Animations**: `Animator` controller states.
- **Materials**: `Renderer.material`.

## 2. Selection Scoring
Score candidates based on:
- **Name similarity** (0.5)
- **Proximity** (0.3)
- **Component match** (0.2)

## 3. Prefab Modification
Use `PrefabUtility.LoadPrefabContents`, apply changes, then `SaveAsPrefabAsset`.

## 4. Sprite-to-Collider Fitting
Mandatory for characters. Use `sr.bounds.size` for hand-drawn or pixel-perfect scan for AI-generated sprites.

## 5. Animation API Corrections
**Use exact names**:
- `AnimatorController.CreateAnimatorControllerAtPath`
- `EditorCurveBinding`
- `AnimationUtility.SetObjectReferenceCurve`

## Success Checklist
- [ ] Renders correctly.
- [ ] No console errors.
- [ ] `Undo.RecordObject` called.
- [ ] `EditorUtility.SetDirty` called.
