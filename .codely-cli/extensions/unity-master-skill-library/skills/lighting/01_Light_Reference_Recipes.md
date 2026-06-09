# Lighting Reference: Properties & Baking

## 💡 Light Component Properties (C# API)

| Property | Description | Example |
|----------|-------------|---------|
| `light.type` | `LightType.Directional`, `Point`, `Spot` | `LightType.Point` |
| `light.color` | `new Color(r, g, b, a)` | `new Color(1f, 0.9f, 0.8f)` |
| `light.intensity` | Brightness | `1.5f` |
| `light.range` | Distance (Point/Spot) | `10f` |
| `light.shadows` | `LightShadows.Hard`, `Soft`, `None` | `LightShadows.Soft` |
| `light.lightmapBakeType` | `Realtime`, `Baked`, `Mixed` | `LightmapBakeType.Baked` |

## 🌍 Environment Settings (RenderSettings)

`RenderSettings` is a static class. Use `RunCommand` to modify.

### Set Ambient Light (Recipe)
```csharp
RenderSettings.ambientMode = AmbientMode.Flat;
RenderSettings.ambientSkyColor = new Color(0.2f, 0.2f, 0.3f);
DynamicGI.UpdateEnvironment(); // Always call this
```

## ✨ Emissive Materials (Recipe)

```csharp
mat.EnableKeyword("_EMISSION");
mat.SetColor("_EmissionColor", Color.white * 2f); // HDR Intensity
mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
```

## 🧊 Baking GI
- **MeshRenderers**: Set `StaticEditorFlags.ContributeGI`.
- **Light Probes**: Use `LightProbeGroup` or **Adaptive Probe Volumes (APV)**.
- **Action**: `Lightmapping.Bake()` (Synchronous).
