---
name: Style_Matching
description: Unity Style_Matching expert - Match Project Style Expert Instructions
---
# Match Project Style Expert Instructions

Ensuring visual harmony between generated assets and the project.

## 1. Visual Style Extraction
- **Look at Assets**: Use `GetImageAssetContent` to analyze color palette, shading style, and outlines.
- **Screenshot**: Proactively analyze the current scene for overall mood and lighting.

## 2. Style Descriptors
- **Pixel Art**: "limited palette, crisp edges, no anti-aliasing".
- **Hand-Painted**: "soft edges, visible brushstrokes".
- **Realistic**: "photorealistic, PBR, high detail".

## 3. Application Methods
- **Reference ID (Best)**: Pass a representative project asset ID to `referenceImageInstanceId`.
- **Enhanced Prompt**: Inject extracted keywords into the generation prompt.
- **Palette**: Specify hex codes or color names explicitly.

## 4. Ambiguity
- If project has mixed styles, **ASK** the user which one to follow. Do not guess.
