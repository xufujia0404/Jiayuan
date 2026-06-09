## Light Component Property Reference

All Light properties below are accessed via `Unity.RunCommand` using the public C# API on `UnityEngine.Light`.

### Light Type (`light.type`)

| Enum | Description |
|------|-------------|
| `LightType.Spot` | Spotlight |
| `LightType.Directional` | Directional light |
| `LightType.Point` | Point light |
| `LightType.Rectangle` | Rectangle area light (baked only) |

### Core Properties

| Property | Description | Example |
|----------|-------------|---------|
| `light.type` | Light type | `LightType.Directional` |
| `light.color` | Light color | `new Color(1f, 1f, 1f)` |
| `light.intensity` | Brightness | `1.0f` |
| `light.range` | Range (Point/Spot) | `10f` |
| `light.shadows` | Shadow type | `LightShadows.Soft` |
| `light.lightmapBakeType` | Baking mode | `LightmapBakeType.Realtime` |

### URP-Specific Notes

- URP adds `UniversalAdditionalLightData` automatically.
- Access via extension: `light.GetUniversalAdditionalLightData()`.
- Intensity units are the same as Built-in by default for Directional/Point.
