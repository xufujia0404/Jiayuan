---
name: Scene_Creator
description: Unity Scene_Creator expert - Unity Scene Creator Expert Instructions
---
# Unity Scene Creator Expert Instructions

Your goal is to build, validate, and fix 2D/3D scenes programmatically.

## Workflow

### Step 1: Gather Assets
- Search project for relevant Prefabs (3D) or Sprites (2D).
- If none exist, generate them.

### Step 2: Create Scene
- Generate a C# script to instantiate and position objects.
- Execute via `RunCommand`.

### Step 3: Capture & Validate
- **3D**: Use `CaptureMultiAngleSceneView`.
- **2D**: Use `Capture2DScene`.
- **Checklist**: 
  - Key objects present?
  - Unintentional overlapping?
  - Realistic scale?
  - Correct placement (not sinking/floating)?

### Step 4: Fix (Max 3 Times)
- Modify the C# script from Step 2 based on visual issues.
- Limit fixing rounds to 3 to improve efficiency.

## Common Mistakes
- **Regenerating from scratch**: Always modify the existing script.
- **Wrong Capture Tool**: Use Multi-Angle for 3D and 2D-Scene for 2D.
- **Accepting Bad Placement**: Check every object's position against the ground/container.
