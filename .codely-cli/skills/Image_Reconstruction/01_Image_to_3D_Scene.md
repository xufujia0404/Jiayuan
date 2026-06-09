# Image to 3D Unity Scene Expert Instructions

Reconstruct a 2D image as a 3D Unity scene by converting Three.js scene data into Unity objects.

## 1. Scene Generation
- Use `Unity.GenerateSceneCodeFromImage` to get Three.js/HTML code from an attached image or Texture2D asset.
- Create a new empty scene before reconstruction to avoid overlaps.

## 2. Coordinate System Conversion (CRITICAL)
Three.js (Right-handed) -> Unity (Left-handed). Failure to convert results in mirrored scenes.
- **The Flip-X Rule**:
    - **Positions**: Negate X -> `new Vector3(-threeX, threeY, threeZ)`.
    - **Rotations**: Negate Y and Z components when converting radians to degrees.
    - **Camera**: Apply the same Flip-X rule to both Camera position and LookAt target.

## 3. Geometry Mapping
| Three.js | Unity Primitive | Scale Adjustment |
|---|---|---|
| Box | Cube | (w, h, d) |
| Sphere | Sphere | (r*2, r*2, r*2) |
| Cylinder | Cylinder | (rb*2, **h/2**, rb*2) |
| Plane | Quad | (w, h, 1) |

## 4. Materials & Rendering
- **Shader**: Always use `Universal Render Pipeline/Lit`.
- **Colors**: Convert hex colors using `ColorUtility.TryParseHtmlString`.
- **Smoothness**: `1.0 - roughness`.

## 5. Camera & Scene View
- Adjust the existing **Main Camera** (don't create a new one).
- Match FOV, Near/Far planes, and Orthographic settings.
- Use `AlignViewToObject` to sync the Editor Scene View with the new camera position.
