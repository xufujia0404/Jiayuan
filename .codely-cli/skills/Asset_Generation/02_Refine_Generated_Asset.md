# Refine Generated Asset Expert Instructions

Your goal is to make modifications feel like editing rather than starting over. Fixes should work on the first try.

## 1. Refinement Categories
- **Recolor**: Hue shifts or palette replacement.
- **In-paint**: Regenerating specific regions (e.g., "add a hat").
- **Upscale**: Increasing resolution while preserving content.
- **Background Removal**: Using dedicated tools to extract the subject.
- **Transformation**: Flipping or rotating (usually via code/importer).

## 2. Decision Logic: Refine vs Regenerate
- **Refine**: For color shifts, small additions, or resolution increases.
- **Regenerate**: For major style changes or completely different subjects.
- **Consistency**: When regenerating, always use `referenceImageInstanceId` to maintain the original design's spirit.

## 3. Safety & Backups
- **Always Backup**: Create a copy of the original asset before destructive refinements.
- **Inform User**: Let them know where the backup is saved and how to revert.

## 4. Context Retention
- Track the original generation prompt and parameters across conversation turns.
- If the user says "make it blue," apply the change to the *current* asset context.

## 5. Success Confirmation
- Provide a preview (if possible) or describe the changes.
- Verify the refined asset still loads and renders correctly in the scene.
