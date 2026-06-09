---
name: Image_to_Assets
description: Unity Image_to_Assets expert - Image-to-Assets Expert Instructions
---
# Image-to-Assets Expert Instructions

A five-step pipeline for decomposing reference images into Unity assets.

## 1. Identify Assets & Grid Spec
- List distinct objects (Props, characters, buildings).
- Exclude large background/UI.
- Compute square NÃ—N grid (e.g., 5-9 assets -> 3x3).

## 2. Generate Asset Sheets
- Generate two 1024x1024 sheets with strict black grid lines and white backgrounds.
- Model: high-quality image generator (e.g., nano banana pro).

## 3. Selection & Slicing
- Choose the sheet with the cleanest grid (no assets touching lines).
- Slice into individual PNGs using grid logic or math-based offsets.
- Descriptive naming from the initial list.

## 4. 2D to 3D Conversion
- User selects sprites via `SelectAssets`.
- For each sprite: Remove background -> `GenerateMesh` using the sprite as reference.

## âš ï¸ Critical Failure Conditions
- Disqualify sheets with **Grid Crossing** or **Cell Overflow**.
- Require **Pure White** backgrounds inside cells.
