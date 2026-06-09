# Unity Master Skill Library

## 概述

本扩展当前提供 **34 个专业技能 + 12 个子代理**，覆盖关卡抽取、试玩广告制作、游戏开发工作流、开发进化分析、AI 资产生成、AI 资产生产、UI 系统、环境渲染、游戏机制九大领域，为 Unity 开发提供全方位专家指导。同时内置 **40+ 篇** 调试经验文档、试玩广告模板和常用代码模板。

## 技能分类

### 关卡抽取 (Level Extract)

从 Unity 项目中抽取指定关卡，移除网络/广告/统计/存档/远程配置等依赖，生成纯单机试玩广告版本。

| Skill                     | 名称               | 说明                                                                                   |
| ------------------------- | ------------------ | -------------------------------------------------------------------------------------- |
| `level-extract-planner`   | 关卡抽取规划器     | 根据需求文档分析项目结构，生成模块化执行计划（ExecutionPlan.md），将改造拆解为独立模块 |
| `level-extract-developer` | 关卡抽取开发执行器 | 消费 planner 产出的 ExecutionPlan.md，逐模块执行改造，每模块通过编译门禁并 git commit  |

**子代理（agents/）**：

| Agent                 | 说明                                        |
| --------------------- | ------------------------------------------- |
| `editor-plan-agent`   | 改造规划子代理 - 生成/更新 ExecutionPlan.md |
| `editor-module-agent` | 改造执行子代理 - 执行具体模块并通过编译门禁 |

适用场景：试玩、抽关、关卡抽取、playable ads、微信试玩、抖音试玩、单关卡版本、离线版本

**工作流**：

```text
用户提供需求文档 → level-extract-planner → ExecutionPlan.md → level-extract-developer → 改造完成
```

### 试玩广告制作 (Playable Ad)

| Skill                   | 名称               | 说明                                                                                                         |
| ----------------------- | ------------------ | ------------------------------------------------------------------------------------------------------------ |
| `playable-ad-planner`   | 试玩广告规划器     | 根据游戏类型和现有素材，规划 30 秒试玩广告的完整流程（开始页→游戏玩法→结算页），生成资产需求清单和开发教程   |
| `playable-ad-developer` | 试玩广告开发执行器 | 消费 planner 产出的交接产物，优先使用现有资产，无可用资产时调用 TJGenerator 生成，快速实现 30 秒试玩广告体验 |

### 游戏开发工作流 (Game Dev Workflow)

| Skill                | 名称           | 说明                                                                 |
| -------------------- | -------------- | -------------------------------------------------------------------- |
| `game-dev-planner`   | 游戏教程规划器 | 根据游戏类型生成 AI 可执行的开发教程和资产需求清单，交接给 developer |
| `game-dev-developer` | 游戏开发执行器 | 消费 planner 产出的交接产物，先生成资产再直接开发游戏                |
| `game-dev-debugger`  | 游戏调试执行器 | 检索开发经验知识库，为游戏开发/生成过程中的调试问题提供支持          |

### 开发进化分析 (Game Evolution)

| Skill              | 名称           | 说明                                                                                              |
| ------------------ | -------------- | ------------------------------------------------------------------------------------------------- |
| `log-analyzer`     | 日志分析器     | 从开发会话日志中提取 bug、失败模式和可复用经验，去重后自动生成经验文档写入知识库                  |
| `session-profiler` | 会话性能分析器 | 解析聊天日志，统计工具调用次数与失败率、Skill 激活、Sub-Agent 调度、耗时分布，生成 Profiling 报告 |

### AI 资产生成 (TJGenerators)

集成团结 AI 平台的多模态生成能力，无缝嵌入 Unity 编辑器工作流。

| Skill                            | 名称               | 说明                                                                 |
| -------------------------------- | ------------------ | -------------------------------------------------------------------- |
| `generate_3d_model`              | 3D 模型生成        | 生成静态 3D 模型；默认优先 Tripo P1，高精度 / PBR / FBX 场景可切换腾讯混元 3D |
| `generate_animated_character`    | 动画角色生成       | 生成带骨骼动画的 Humanoid 角色                                       |
| `generate_rigged_animated_model` | 绑骨动画模型生成   | 为现有模型绑骨和/或生成动作动画                                      |
| `generate_sprite`                | 精灵生成           | 生成 2D 精灵资产（图标、物品图、角色肖像等）                         |
| `generate_sprite_sequence`       | 精灵序列生成       | 从角色参考图生成 2D 逐帧动画                                         |
| `generate_image`                 | 图片生成           | 生成 Texture2D / PNG 图片资产（概念图、UI 背景等）                   |
| `generate_material`              | 材质生成           | 生成 PBR 表面材质（无缝纹理 PNG + `.mat` 文件）                      |
| `generate_skybox`                | 天空盒生成         | 生成天空盒 Cubemap / Skybox 材质                                     |
| `generate_audio_clip`            | 背景音乐生成       | 生成 BGM 和环境音频片段                                              |
| `generate_sound_effect`          | 音效生成           | 生成一次性音效（枪声、脚步声、爆炸等）                               |
| `generate_terrain`               | 地形生成           | 生成大尺度 Unity Terrain 地形并应用到当前场景                        |
| `generate_video`                 | 视频生成           | 生成视频资产（宣传视频、转场视频、场景视频等）                       |
| `search_assets`                  | 资产搜索下载       | 从云端资产库搜索并下载现成资产（搜索→评估→下载）                     |

**子代理（agents/）**：

| Agent                                  | 说明                                         |
| -------------------------------------- | -------------------------------------------- |
| `ai-generation-planner`                | AI 生成规划子代理 - 多任务生成请求拆解与执行规划 |
| `3d-model-generator`                   | 3D 模型生成子代理                            |
| `animated-character-generator`         | 动画角色生成子代理                           |
| `audio-generator`                      | 音频生成子代理                               |
| `image-generator`                      | 图片生成子代理                               |
| `material-generator`                   | 材质生成子代理                               |
| `skybox-generator`                     | 天空盒生成子代理                             |
| `sprite-and-sprite-sequence-generator` | 精灵及序列生成子代理                         |
| `search-assets`                        | 资产搜索与下载子代理                         |
| `video-generator`                      | 视频生成与 VideoPlayer 配置子代理            |

### AI 资产生产 (AI Asset Production)

面向已有资产的接线、放置、地形/骨骼后处理与场景整合。

| Skill                    | 名称               | 说明                                                                 |
| ------------------------ | ------------------ | -------------------------------------------------------------------- |
| `fbx-humanoid-auto-rig`  | FBX Humanoid 自动绑骨 | 使用 UniRig 为任意人形 FBX 自动生成骨架、Humanoid 命名、Skin 与 UV merge |
| `place_assets_in_scene`  | 场景放置专家       | 将已有资产按自然语言要求放入当前场景，支持 Prefab / Sprite / Audio / Material / TerrainData 等 |

### UI 系统 (UI Systems)

| Skill              | 名称            | 说明                                                  |
| ------------------ | --------------- | ----------------------------------------------------- |
| `ui-system-router` | UI 系统路由器   | 判断合适的 UI 系统并路由到正确的专业技能              |
| `imgui`            | IMGUI 专家      | 为编辑器扩展和调试工具生成即时模式 GUI 代码           |
| `ugui`             | uGUI 专家       | 理解现有 UI、进行针对性编辑、生成新的 Canvas 层级结构 |
| `ui-toolkit`       | UI Toolkit 专家 | 生成 UXML/USS 文件、Manipulators、处理 UI 运行时绑定  |

### 环境渲染 (Environment & Rendering)

| Skill           | 名称                 | 说明                                            |
| --------------- | -------------------- | ----------------------------------------------- |
| `cinemachine`   | Cinemachine 3.1 专家 | 使用模块化组件系统创建正确的摄像机设置          |
| `lighting`      | Lighting 专家        | 为 Built-in 和 URP 管线创建、配置和排查场景光照 |
| `scene-creator` | Scene Creator 专家   | 以编程方式构建、验证和修复 2D/3D 场景           |

### 游戏机制 (Gameplay Mechanics)

| Skill          | 名称             | 说明                               |
| -------------- | ---------------- | ---------------------------------- |
| `2d-character` | 2D 角色专家      | 端到端处理角色创建工作流           |
| `auto-wire`    | 资产自动接线专家 | 自动将生成的资产接入用户项目       |
| `input-system` | 输入系统专家     | 确定合适的系统并实现健壮的输入设置 |

## 经验知识库

### 关卡抽取需求文档 (experience/templates-level-extract/)

| 模板           | 说明                                                                                             |
| -------------- | ------------------------------------------------------------------------------------------------ |
| `LevelExtract` | 关卡抽取试玩广告的标准需求文档模板，包含业务需求约束：纯单机、无存档、无广告、无统计、无远程配置 |

### 试玩广告模板 (experience/templates-playable-ad/)

| 模板                    | 说明                     |
| ----------------------- | ------------------------ |
| `playable_start_page`   | 标题、按钮动画、场景切换 |
| `playable_guide_system` | 目标点、箭头、完成检测   |
| `playable_end_page`     | 分数展示、重玩按钮、微信通知 |

### 调试经验 (experience/development/)

当前内置 **30+ 篇** Unity 开发调试经验文档，涵盖常见问题：

- **UI 问题**：UGUI 遮挡 Camera、EventSystem 缺失导致点击失效、中文/字体显示异常
- **物理问题**：CapsuleCollider 中心漂移、Collider 未设 `isTrigger`、Player 插入地面
- **编译错误**：DLL 访问冲突、Library 缓存错误、匿名类型数组 `CS0826`、私有方法访问
- **渲染问题**：Sprite 半透明边缘、粒子材质显示方块、Standard Shader 光泽度无效
- **运行时问题**：场景对象被禁用、自动射击未实现、相机跟随抖动、游戏状态管理缺失

### 代码模板 (experience/templates/)

| 模板                 | 说明       |
| -------------------- | ---------- |
| `asset_loader`       | 资产加载器 |
| `camera_follow`      | 相机跟随   |
| `collectible_system` | 收集系统   |
| `procedural_meshes`  | 程序化网格 |
| `ui_panels`          | UI 面板    |

## 使用方式

```bash
# 激活特定技能
activate_skill("<skill-name>")

# 关卡抽取
activate_skill("level-extract-planner")
activate_skill("level-extract-developer")

# 试玩广告
activate_skill("playable-ad-planner")
activate_skill("playable-ad-developer")

# 游戏开发工作流
activate_skill("game-dev-planner")
activate_skill("game-dev-developer")
activate_skill("game-dev-debugger")

# AI 资产生成
activate_skill("generate_3d_model")
activate_skill("generate_animated_character")
activate_skill("generate_rigged_animated_model")
activate_skill("generate_sprite")
activate_skill("generate_sprite_sequence")
activate_skill("generate_image")
activate_skill("generate_material")
activate_skill("generate_skybox")
activate_skill("generate_audio_clip")
activate_skill("generate_sound_effect")
activate_skill("generate_terrain")
activate_skill("generate_video")
activate_skill("search_assets")

# AI 资产生产
activate_skill("fbx-humanoid-auto-rig")
activate_skill("place_assets_in_scene")

# 开发进化
activate_skill("log-analyzer")
activate_skill("session-profiler")

# Unity 专家
activate_skill("lighting")
activate_skill("cinemachine")
activate_skill("scene-creator")
activate_skill("ui-system-router")
```

## 技能依赖关系

```text
level-extract-planner → level-extract-developer
editor-plan-agent → editor-module-agent (关卡抽取子代理)

playable-ad-planner → playable-ad-developer

game-dev-planner → game-dev-developer → game-dev-debugger → log-analyzer → session-profiler

ai-generation-planner → 3d-model-generator | animated-character-generator | audio-generator | image-generator | material-generator | skybox-generator | sprite-and-sprite-sequence-generator | search-assets | video-generator

generate_3d_model → place_assets_in_scene
generate_animated_character → place_assets_in_scene
generate_sprite → place_assets_in_scene
generate_terrain → place_assets_in_scene
search_assets → search-assets → place_assets_in_scene
generate_video → video-generator

2d-character → auto-wire
ui-system-router → imgui | ugui | ui-toolkit
```

## 文件结构

```text
unity-master-skills/
├── .gitignore
├── CODELY.md
├── gemini-extension.json
├── manifest.json
├── agents/
│   ├── 3d-model-generator.toml
│   ├── ai-generation-planner.toml
│   ├── animated-character-generator.toml
│   ├── audio-generator.toml
│   ├── editor-module-agent.toml
│   ├── editor-plan-agent.toml
│   ├── image-generator.toml
│   ├── material-generator.toml
│   ├── search-assets.toml
│   ├── skybox-generator.toml
│   ├── sprite-and-sprite-sequence-generator.toml
│   └── video-generator.toml
├── experience/
│   ├── development/
│   ├── templates/
│   ├── templates-level-extract/
│   └── templates-playable-ad/
├── hooks/
│   └── hooks.json
├── Packages/
│   └── cn.tuanjie.ai.generators/
└── skills/
    ├── 2d-character/
    ├── auto-wire/
    ├── cinemachine/
    ├── fbx-humanoid-auto-rig/
    ├── game-dev-debugger/
    ├── game-dev-developer/
    ├── game-dev-planner/
    ├── generate_3d_model/
    ├── generate_animated_character/
    ├── generate_audio_clip/
    ├── generate_image/
    ├── generate_material/
    ├── generate_rigged_animated_model/
    ├── generate_skybox/
    ├── generate_sound_effect/
    ├── generate_sprite/
    ├── generate_sprite_sequence/
    ├── generate_terrain/
    ├── generate_video/
    ├── imgui/
    ├── input-system/
    ├── level-extract-developer/
    ├── level-extract-planner/
    ├── lighting/
    ├── log-analyzer/
    ├── place_assets_in_scene/
    ├── playable-ad-developer/
    ├── playable-ad-planner/
    ├── scene-creator/
    ├── search_assets/
    ├── session-profiler/
    ├── ugui/
    ├── ui-system-router/
    └── ui-toolkit/
```
