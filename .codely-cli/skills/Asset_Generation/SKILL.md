---
name: Asset_Generation
description: Unity Asset_Generation expert - Generate Asset Expert Instructions
---
# Generate Asset Expert Instructions

Expert guidance for high-quality AI asset generation.

## 1. Model Selection (MANDATORY)
- **Action**: Always call `GetModels` first.
- **Constraint**: Every `GenerateAsset` call **MUST** include a `modelId`.
- **Capabilities**: Match model keywords (`SupportsSprites`, `Model3d`, etc.) to asset type.

## 2. waitForCompletion (CRITICAL)
- **Rule**: ALWAYS set `waitForCompletion=true` for agentic workflows.
- **Why**: `false` returns an empty placeholder (transparent black). Placeholders cannot be used as references or shown to users.

## 3. Style & Consistency
- **Prompting**: Use concrete style terms (e.g., "pixel art", "low poly"). Avoid vague "match the project".
- **Reference Image**: Always use `FileInstanceID` (Texture2D) for `referenceImageInstanceId`.
- **Spritesheets**: Reference image MUST be a single concept, not a grid.

## 4. Parameters
- **Resolution**: Default to **1024x1024**.
- **Loop**: Always set `loop=false` for spritesheets (Unity handles the loop).
- **Slow Motion**: Use for Walk/Run cycles to ensure clean frame sampling.

## 5. Background Removal
- **Requirement**: Sprite sheet generators produce backgrounds.
- **Step**: Call `RemoveSpriteBackground` in a **SEPARATE** turn after generation.

## 6. Safety
- **No Domain Reloads**: Do NOT create scripts in the same response as asset generation.
- **Interrupted Downloads**: Use `ManageInterruptedAssetGenerations` to fix stuck downloads.
