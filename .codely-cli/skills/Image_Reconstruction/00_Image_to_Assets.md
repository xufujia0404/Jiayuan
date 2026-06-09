# Image-to-Assets Expert Instructions

A five-step pipeline for turning a reference game scene image into individual, Unity-ready 2D sprite assets or 3D object assets.

## 1. Identify Assets & Grid Spec
- Analyze the reference image for distinct, placeable objects (trees, rocks, characters, etc.).
- Exclude background elements (sky, terrain) and UI.
- Compute an N×N grid based on the asset count (e.g., 7 assets -> 3x3 grid).

## 2. Generate Asset Sheets
- Generate two 1024x1024 sprite sheets with a white background and strict grid lines.
- **Model**: Use a model that supports high-quality sprite sheet generation (e.g., nano banana pro).
- **Prompting**: Specify the grid dimensions and list the assets to be placed in cells.

## 3. Selection & Slicing
- Choose the best asset sheet based on grid integrity (no assets touching lines) and style match.
- Slice the sheet into individual PNGs.
- **Naming**: Use descriptive names from the initial list (e.g., `tall_pine_tree.png`).

## 4. 2D to 3D Conversion (Optional)
- Use `SelectAssets` to let the user pick which sprites to convert.
- For selected sprites, generate 3D models using `GenerateMesh` (e.g., Rodin Hyper 3D model).
- Reference the original sprite for style and shape consistency.

## ⚠️ Critical Rules
- **Grid Check**: Disqualify any sheet where assets cross grid lines or multiple assets share a cell.
- **Backgrounds**: Generated sheets MUST have white backgrounds inside cells for clean slicing.
- **Descriptive Naming**: Ensure assets are uniquely named to avoid overwriting.
