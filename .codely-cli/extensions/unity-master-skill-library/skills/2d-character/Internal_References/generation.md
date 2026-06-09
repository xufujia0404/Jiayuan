# 2D Character Generation Workflow

## Phase 1: Concept
- **Prompt**: "Full body, head to toe, neutral standing pose, facing RIGHT".
- **Resolution**: Default to 1024x1024.
- **Reference**: approved concept provides `FileInstanceID` for animations.

## Phase 2: Spritesheets
- **Parallelism**: Generate Idle, Run, Jump sheet concurrently once concept is approved.
- **Prompting**: Use "slow motion" for Walk/Run to prevent cycle sampling errors.
- **Settings**: Always set `loop=false`. Unity Animator handles actual looping.

## Phase 3: Post-Processing
- **Background**: Call `RemoveSpriteBackground` for every sheet.
- **Slicing**: Slice 4x4 grid via TextureImporter metadata.
