# Baked & Environment Lighting — Tool Reference

## Environment Lighting (RenderSettings)

RenderSettings is a static class. Use `Unity.RunCommand` to read or modify.

### Set Environment Lighting (Recipe)

```csharp
RenderSettings.ambientMode = AmbientMode.Flat;
RenderSettings.ambientSkyColor = new Color(0.4f, 0.3f, 0.25f);
RenderSettings.ambientIntensity = 1.0f;
DynamicGI.UpdateEnvironment();
```

## Emissive Materials

### Enable Emission (Recipe)

```csharp
mat.EnableKeyword("_EMISSION");
mat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.4f) * 2f); // HDR intensity
mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
```

## Light Probes & Baking

- **LightProbeGroup**: Set `probePositions` (Vector3[]) programmatically.
- **Bake**: Use `Lightmapping.Bake()` (Synchronous - freezes editor).
- **ContributeGI**: Set `StaticEditorFlags.ContributeGI` on MeshRenderers before baking.
