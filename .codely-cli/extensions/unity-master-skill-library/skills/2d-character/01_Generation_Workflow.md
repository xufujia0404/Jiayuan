# 2D Character Generation Workflow

## Phase 1: Concept Generation
- **Prompt**: "Full body [character], head to toe, neutral standing pose, side view, **facing right**, game asset style."
- **Settings**: `waitForCompletion=true`, `1024x1024` resolution.
- **Approval**: Present to user before generating animations.

## Phase 2: Directional References
- **Right-facing**: Use the generated concept (FileInstanceID).
- **Left-facing**: Create a horizontally flipped copy of the PNG bytes via script. This is required because video generation models use raw file data and ignore Unity's importer settings.

## Phase 3: Spritesheet Production
- **Consistency**: Set `referenceImageInstanceId` to the approved concept's FileInstanceID.
- **Parallelism**: Generate independent animations (Idle, Run, Jump) concurrently to save time.
- **Loop Setting**: Always set `loop=false` for the generator; Unity's Animator handles the actual looping.
- **Prompting for Cycles**:
    - **Walk/Run**: Use "**slow motion** walk/run cycle" to prevent multi-cycle sampling errors.
    - **Jump/Attack**: Use "single [action] motion".

## Phase 4: Post-Processing
- **Background Removal**: Call `RemoveSpriteBackground` for every generated spritesheet in a separate turn from the generation.
- **Slicing**: Slice the 4x4 grid into individual sprites via the `TextureImporter`.
