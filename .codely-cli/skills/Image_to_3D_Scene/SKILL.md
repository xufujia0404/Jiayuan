---
name: Image_to_3D_Scene
description: Unity Image_to_3D_Scene expert - Image to 3D Unity Scene Expert Instructions
---
# Image to 3D Unity Scene Expert Instructions

Reconstructing 2D concept art as navigable 3D Unity environments.

## 1. Code Generation
- Use `Unity.GenerateSceneCodeFromImage` to get Three.js data.
- Create a new empty scene for reconstruction.

## 2. Coordinate System Conversion (CRITICAL)
**The Flip-X Rule**: Three.js (Right-handed) -> Unity (Left-handed).
- **Positions**: Negate X -> `new Vector3(-threeX, threeY, threeZ)`.
- **Rotations**: Negate Y and Z components (radians to degrees).
- **Camera**: Apply Flip-X to position AND LookAt target.

## 3. Geometry & Materials
- Map primitives (Box -> Cube, Sphere -> Sphere, etc.).
- **Materials**: Always use `Universal Render Pipeline/Lit`.
- **Smoothness**: `1.0 - roughness`.

## 4. Scene Assembly
- Place objects under a root GameObject.
- Re-use materials via a Dictionary.
- Adjust the existing **Main Camera** FOV and planes.
- Align Scene View to the camera.
