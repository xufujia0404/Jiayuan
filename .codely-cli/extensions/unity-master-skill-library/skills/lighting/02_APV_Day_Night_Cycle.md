# APV Day-Night Cycle Guide (Unity 6 / URP)

Adaptive Probe Volumes (APV) with **Lighting Scenario Blending** is the modern way to handle time-of-day transitions.

## 🛠️ Setup Workflow
1. **Configure URP Asset**: Enable `m_LightProbeSystem = 1` (APV) and Scenario Blending via `SerializedObject`.
2. **Add ProbeVolume**: Add to scene, set `mode = ProbeVolume.Mode.Scene`.
3. **Create Scenarios**: e.g., "Day", "Night" in the Baking Set.
4. **Bake Individually**: 
   - Set scenario active -> Adjust lights -> `Lightmapping.Bake()`.
   - Repeat for each scenario.

## 🌓 Runtime Blending
Interpolate between two scenarios:
```csharp
ProbeReferenceVolume.instance.BlendLightingScenario("Night", 0.5f); // 50% Day/Night blend
```

## ⚠️ Important Notes
- Blending only affects **baked probe data** (indirect light).
- You must manually animate **realtime components** (Sun rotation, Skybox exposure) to match the blend factor.
- Runtime blending works between **two scenarios** at a time.
