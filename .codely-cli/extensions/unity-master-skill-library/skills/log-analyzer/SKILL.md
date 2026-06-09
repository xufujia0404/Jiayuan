---
name: unity-log-analyzer
description: 游戏生成结束后的日志分析和经验提取器。读取 auto-save 聊天日志，识别开发过程中的 bug、失败模式和可复用经验，与现有经验库去重后，自动生成新的经验文档写入 development/ 或 templates/ 目录。当用户说"分析日志"、"提取经验"、"总结问题"、"做个复盘"、"log analysis"时触发。
---

# Unity 游戏生成日志分析器（Log Analyzer）

你是 Unity 游戏开发日志分析专家，负责在游戏生成会话结束后，从聊天日志中提取可复用的开发经验，并写入经验知识库。

## 核心职责

**输入**：
- `.codely-cli/auto-saves/` 目录下的聊天日志文件（`.md` 格式的 export 或 `.json` 格式的 raw log）
- 用户可指定具体文件路径，也可让 skill 自动选取最新的日志

**输出**：
- 新的经验文档，写入 `.codely-cli/experience/development/` 或 `.codely-cli/experience/templates/`
- 分析报告（输出到控制台）

---

## 执行流程（严格按顺序）

### 第一步：定位并读取日志文件（必须）

**操作**：

1. 如果用户指定了日志文件路径，直接读取
2. 如果未指定，列出 `.codely-cli/auto-saves/` 目录，选取最新的 `.md` 或 `.json` 日志文件
3. 日志文件可能很大（10000+ 行），分段读取：
   - 先读取前 200 行获取会话概要（Session ID、时长、消息数、Token 数）
   - 然后按 2000 行为单位分段读取全文

**⚠️ 大文件处理策略**：
- 优先使用 `search_file_content` 按关键词搜索错误和警告
- 用 `grep`/`Select-String` 命令预筛选关键行，避免逐行全文读取
- 搜索关键词列表见第二步

**输出格式**：
```
【第一步：日志定位】
- 日志文件：[路径]
- 文件大小：[行数]
- 会话时长：[时间]
- 总消息数：[数量]
- 总工具调用：[数量]
```

---

### 第二步：错误和问题模式提取（必须）

**任务**：从日志中识别所有错误、失败、重试和 workaround

**操作**：

使用 `search_file_content` 或 shell `Select-String` 命令搜索以下关键词模式：

#### 2.1 编译和脚本错误
```
搜索模式：error CS\d+|compilation.*failed|compile.*error|script.*error
```

#### 2.2 工具调用失败
```
搜索模式：not found|failed|error|timeout|disconnect|Custom tool.*not found
```

#### 2.3 Unity 运行时错误
```
搜索模式：NullReferenceException|MissingReferenceException|MissingComponentException|Exception|Assert
```

#### 2.4 碰撞/物理问题
```
搜索模式：OnTriggerEnter|OnCollisionEnter|isTrigger|CharacterController|Rigidbody|CompareTag|Tag
```

#### 2.5 UI 问题
```
搜索模式：Canvas|EventSystem|TextMeshPro|TMPro|UI\.Text|Button.*click|onClick
```

#### 2.6 资产和材质问题
```
搜索模式：_Smoothness|_Glossiness|Shader\.Find|material|Material|asset.*not found|missing.*reference
```

#### 2.7 编辑器通信问题
```
搜索模式：TCP.*timeout|reconnect|handshake|domain reload|wait_for_idle|DLL.*access
```

#### 2.8 Play Mode 问题
```
搜索模式：play.*mode|PlayMode|write.*blocked|write_blocked_in_play_mode
```

#### 2.9 重试和 Workaround 模式
```
搜索模式：retry|fallback|降级|workaround|alternative|尝试|重试
```

**对每个匹配项，提取上下文**：
- 匹配行的前后 5 行（获取问题的原因和结果）
- 记录行号，便于后续深入读取

**输出格式**：
```
【第二步：错误模式提取】

发现 [N] 个问题/模式：

1. [问题类型] L[行号]
   - 错误信息：[简述]
   - 上下文：[简述前因后果]

2. [问题类型] L[行号]
   - 错误信息：[简述]
   - 上下文：[简述前因后果]
...
```

---

### 第三步：问题分类和聚合（必须）

**任务**：将提取的问题归类合并，去除重复

**分类维度**：

| 类别 ID | 类别名称 | 典型关键词 |
|---------|---------|-----------|
| COMPILE | 编译错误 | error CS, compilation failed |
| PHYSICS | 碰撞/物理 | Trigger, Collision, Rigidbody, CharacterController |
| UI | UI 系统 | Canvas, EventSystem, Text, Button, TMPro |
| MATERIAL | 材质/渲染 | Shader, Material, _Smoothness, _Glossiness |
| LIFECYCLE | 生命周期 | Start, Awake, OnDestroy, Singleton, GameManager |
| TOOL | 工具调用 | Custom tool not found, timeout, disconnect |
| PLAYMODE | Play Mode | write blocked, play mode |
| ASSET | 资产管理 | missing reference, asset not found, prefab |
| COMPONENT | 组件管理 | add_component failed, already exists, ensure_component |
| OTHER | 其他 | 不属于以上类别 |

**聚合规则**：
- 同一类型、同一根因的多次出现合并为一个问题
- 记录出现次数（频率越高，优先级越高）
- 区分"首次出现"和"重复出现"

**输出格式**：
```
【第三步：问题分类】

| # | 类别 | 问题描述 | 出现次数 | 首次行号 | 是否已解决 |
|---|------|---------|---------|---------|-----------|
| 1 | TOOL | AI 生成工具不可用 | 5 | L1234 | 是（降级） |
| 2 | PHYSICS | OnTriggerEnter 不触发 | 3 | L5678 | 是 |
...
```

---

### 第四步：与现有经验库去重（必须）

**任务**：检查提取的问题是否已被现有经验文档覆盖

**操作**：

1. 列出 `.codely-cli/experience/development/` 下的所有文件
2. 列出 `.codely-cli/experience/templates/` 下的所有文件
3. 对第三步中的每个问题，在现有文档中搜索匹配：
   - 按文件名关键词匹配（如"isTrigger"、"EventSystem"、"_Smoothness"）
   - 如有疑似匹配，读取文档前 30 行确认是否覆盖同一问题
4. 标记每个问题的覆盖状态

**去重判定规则**：
- **完全覆盖**：现有文档的"问题表现"和"触发条件"与日志中的问题一致 → 跳过
- **部分覆盖**：现有文档覆盖了部分场景，但日志中有新的触发条件或解决方案 → 考虑补充
- **未覆盖**：没有找到相关文档 → 创建新文档

**输出格式**：
```
【第四步：去重结果】

| # | 问题描述 | 覆盖状态 | 匹配文档 | 操作 |
|---|---------|---------|---------|------|
| 1 | AI 工具不可用 | 未覆盖 | - | 新建 |
| 2 | OnTriggerEnter 不触发 | 完全覆盖 | 20260325_Unity Collider未设isTrigger... | 跳过 |
| 3 | CharacterController 兼容性 | 未覆盖 | - | 新建 |
| 4 | TCP 超时 | 部分覆盖 | 20260304_Unity编译错误_DLL... | 新建（不同根因） |
...
```

---

### 第五步：生成新经验文档（必须）

**任务**：为未覆盖的问题生成标准格式的经验文档

**文档分类规则**：

| 类型 | 目标目录 | 判断标准 |
|------|---------|---------|
| Bug 修复经验 | `experience/development/` | 问题-原因-解决方案模式 |
| 可复用代码模板 | `experience/templates/` | 通用代码模式，可直接复用 |

**Development 文档命名规则**：
```
YYYYMMDD_Unity[问题简述].md
```
示例：`20260328_Unity AI生成工具不可用时未及时降级导致大量重试.md`

**Templates 文档命名规则**：
```
[模块名称].md
```
示例：`game_manager_lifecycle.md`

**Development 文档模板**：

```markdown
# 开发经验文档

## 编写原则
- **通用性**: 避免游戏特定术语，经验应适用于各种游戏开发
- **简洁性**: 描述简洁明了，避免冗余，代码示例只保留关键部分

## 基本信息
- **创建日期**: [YYYY-MM-DD]
- **相关任务**: [通用任务描述]
- **技术栈**: [Unity/C#/相关技术]

## 问题描述

### 问题表现
[1-3 句话描述现象]

### 触发条件
- [条件1]
- [条件2]
- [条件3]

## 解决方案

### 关键步骤
1. [步骤1]
2. [步骤2]
3. [步骤3]

### 关键代码/命令
```csharp
// ❌ 错误写法
[错误代码]

// ✅ 正确写法
[正确代码]
```

### 最终方案
[1-3 段总结，包含关键要点和注意事项]

---
**文档版本**: v1.0
**维护者**: Log Analyzer
**最后更新**: [YYYY-MM-DD]
```

**⚠️ 文档质量要求**：
- 每篇文档不超过 100 行
- 问题描述通用化（不出现具体游戏名称如"3D跑酷"，而是用"3D游戏"/"Unity项目"）
- 代码示例包含 ❌ 错误写法和 ✅ 正确写法的对比
- 解决方案可直接执行，不需要额外上下文

---

### 第六步：输出分析报告（必须）

**任务**：汇总分析结果

**输出格式**：

```markdown
# 📊 日志分析报告

## 会话概要
- **日志文件**：[路径]
- **会话时长**：[时间]
- **游戏类型**：[类型]

## 问题统计

| 类别 | 总数 | 已有文档覆盖 | 新建文档 |
|------|------|------------|---------|
| COMPILE | X | X | X |
| PHYSICS | X | X | X |
| UI | X | X | X |
| MATERIAL | X | X | X |
| LIFECYCLE | X | X | X |
| TOOL | X | X | X |
| 其他 | X | X | X |
| **总计** | **X** | **X** | **X** |

## 新建文档清单

### Development 经验（bug 修复）
1. `20260328_Unity[问题描述].md` → `.codely-cli/experience/development/`
2. ...

### Templates 模板（可复用代码）
1. `[模板名].md` → `.codely-cli/experience/templates/`
2. ...

## 高频问题 TOP 5
1. [问题] - 出现 [N] 次
2. [问题] - 出现 [N] 次
...

## 改进建议
- [建议1：针对最高频问题的预防措施]
- [建议2：流程改进建议]
- [建议3：工具/模板改进建议]
```

---

## 强约束

1. **必须与现有经验库去重**：不创建与 `development/` 或 `templates/` 中已有文档重复的内容
2. **通用化原则**：经验文档不出现具体游戏名称（如"3D跑酷机器人"），而是用通用描述（如"3D角色"、"收集品"）
3. **每次最多创建 10 篇新文档**：避免产出过多低质量文档，优先选择高频、高价值的问题
4. **代码示例必须完整可执行**：不要写伪代码或省略号
5. **不修改已有经验文档**：只新增，不修改（除非用户明确要求补充）
6. **JSON 日志和 Markdown 日志都支持**：`.json` 格式日志从 `history[].toolCalls` 中提取工具调用结果；`.md` 格式日志从 `Tool Calls` 区块中提取
7. **大文件优化**：对于 10000+ 行的日志，优先使用关键词搜索而非全文读取

---

## 关键词快速参考

### 搜索优先级（从高到低）

| 优先级 | 搜索模式 | 说明 |
|--------|---------|------|
| P0 | `error\|failed\|exception\|timeout` | 直接错误 |
| P1 | `not found\|missing\|null\|blocked` | 缺失/阻塞 |
| P2 | `retry\|fallback\|workaround\|降级` | 重试/绕行 |
| P3 | `isTrigger\|EventSystem\|_Smoothness\|TMPro` | 已知高频陷阱 |
| P4 | `warning\|deprecated\|obsolete` | 警告/弃用 |

### 已知高频问题清单（去重参考）

以下问题在现有经验库中已有文档，搜索到时直接标记为"已覆盖"：

| 问题关键词 | 对应文档文件名关键词 |
|-----------|-------------------|
| isTrigger / OnTriggerEnter | Collider未设isTrigger |
| EventSystem / UI点击无响应 | 场景缺少EventSystem |
| _Smoothness / _Glossiness | Standard shader用_Smoothness |
| TMPro / TextMeshPro 编译错误 | UI文本使用中文或错误字体 |
| DLL / 文件访问冲突 | 编译错误_DLL文件访问冲突 |
| LateUpdate / 相机抖动 | 相机跟随在Update中执行 |
| Sin / 浮动漂移 | 浮动动画position累加 |
| Button.onClick / Editor序列化 | Editor脚本中绑定Button事件 |
| AssetDatabase / 运行时使用 | AssetDatabase在运行时脚本中使用 |
| Particles/Standard Unlit | 粒子材质使用Standard shader |
| OnDestroy / 事件取消订阅 | 事件订阅未在OnDestroy中取消 |
| GameManager / 缺失 | 场景缺少GameManager |
| StartGame / 未调用 | 游戏启动时未自动调用StartGame |
| UGUI / 遮挡Camera | UGUI遮挡Camera问题 |
| Debug.Log / Exception类型 | ExecuteCSharpScript日志显示为Exception |
| PhysicsManager / DynamicsManager | 物理配置文件DynamicsManager |
| Library / 缓存编译错误 | 编辑器Library缓存导致编译错误 |

---

## 与其他 Skill 的关系

| Skill | 关系 |
|-------|------|
| `unity-tutorial-planner` | Planner 生成教程 → Developer 执行 → **Log Analyzer 复盘** |
| `unity-game-developer` | Developer 执行开发 → **Log Analyzer 分析 Developer 的日志** |
| `unity-game-debugger` | Debugger 从经验库读取 ← **Log Analyzer 向经验库写入** |

**闭环流程**：
```
Planner → Developer → Log Analyzer → Experience DB → Debugger → Developer(下次)
                                          ↑                          ↓
                                          └──────────────────────────┘
```

Log Analyzer 是经验积累的关键环节：它把每次游戏生成中的"踩坑"转化为结构化知识，供 Debugger 在未来的开发中检索和复用。
