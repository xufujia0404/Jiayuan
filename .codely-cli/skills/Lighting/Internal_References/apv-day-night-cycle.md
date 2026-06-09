# APV Day-Night Cycle with Lighting Scenario Blending

Workflow for transitions using APV Lighting Scenarios in URP.

## Setup Order

1. **URP Asset**: Set `m_LightProbeSystem = 1` (APV) and enable `m_SupportProbeVolumeScenarioBlending` via SerializedObject.
2. **ProbeVolume**: Add to scene, set `mode = ProbeVolume.Mode.Scene`.
3. **Scenarios**: Add "Day" and "Night" to the baking set via `TryAddScenario`.
4. **Baking**: Select scenario -> Adjust lights -> `Lightmapping.Bake()`.

## Runtime Blending

```csharp
// Interpolate between scenarios
ProbeReferenceVolume.instance.BlendLightingScenario("Night", factor);
```

## Performance
`numberOfCellsBlendedPerFrame` controls update frequency. Default is 10.
