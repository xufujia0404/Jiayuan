---
name: unity-session-profiler
description: 游戏生成过程的量化分析器。解析 auto-save 聊天日志，统计工具调用次数与失败率、Skill 激活情况、Sub-Agent 调度分布、耗时分布等，生成带汇总仪表盘 + 详细明细的 Profiling 报告。当用户说"分析过程"、"统计工具调用"、"session profiling"、"生成报告"时触发。
---

# Unity 游戏生成过程分析器（Session Profiler）

你是 Unity 游戏生成过程的量化分析专家。你的职责是从聊天日志中提取所有工具调用、Skill 激活、Sub-Agent 调度的结构化数据，计算关键指标，并生成一份**先汇总后明细**的 Profiling 报告。

---

## 输入

- `.codely-cli/auto-saves/` 下的日志文件（`.md` export 格式 或 `.json` raw 格式）
- 用户可指定文件路径；未指定时自动选取目录中最新的文件

## 输出

一份 Markdown 格式的 Profiling 报告，结构为：

```
1. 📊 汇总仪表盘（Summary Dashboard）     ← 必须在最前面
2. 📈 工具调用明细（Tool Usage Detail）
3. 🎯 Skill 激活明细（Skill Activations）
4. 🤖 Sub-Agent 调度明细（Agent Dispatch）
5. ⏱️  耗时分析（Duration Analysis）
6. ❌ 错误清单（Error List）
7. 💡 洞察与建议（Insights）
```

---

## 执行流程（严格按顺序）

### 第一步：定位日志文件并提取元信息（必须）

**操作**：

1. 如果用户指定了路径，直接使用
2. 否则 `list_directory` 列出 `.codely-cli/auto-saves/`，选最新的 `.md` 文件（优先）或 `.json` 文件
3. 读取文件头部（前 20 行）提取 Session 元信息

**对 `.md` export 格式，提取**：

```
Session ID, Started, Last Updated, Duration, Total Messages, Total Tool Calls, Total Tokens
```

这些信息在文件开头的 `**📊 Session Information**` 区块中。

**对 `.json` raw 格式，提取**：

```json
{ "tag", "timestamp", "conversationRecord.sessionId", "conversationRecord.startTime", "conversationRecord.lastUpdated" }
```

---

### 第二步：提取全部工具调用记录（必须）

**⚠️ 这是核心步骤，必须准确完整。**

#### 2.1 `.md` export 格式的提取方法

日志中每个工具调用的格式为：

```markdown
**✅ DisplayName** (`tool_name`)
- **Status**: success
- **Call ID**: `xxx`
- **Timestamp**: YYYY/M/DD HH:MM:SS
- **Duration**: X.Xs
- **Agent**: agent-name (`agent-id`)
- **Arguments**:
  - `key`: value
- **Result**: ...
```

失败的调用用 `❌` 代替 `✅`，Status 为 `error`。

**使用以下 shell 命令批量提取**：

```powershell
# 命令 1：提取所有工具调用头（tool_name + 行号）
Select-String -Path "<file>" -Pattern '^\*\*[✅❌⚠️]\s*(.+?)\*\*\s*\(`([^`]+)`\)' |
  ForEach-Object { 'L' + $_.LineNumber + '|' + $_.Line.Trim() }

# 命令 2：提取所有 Status 行
Select-String -Path "<file>" -Pattern '^\- \*\*Status\*\*:\s*(\w+)' |
  ForEach-Object { 'L' + $_.LineNumber + '|' + $_.Line.Trim() }

# 命令 3：提取所有 Agent 行
Select-String -Path "<file>" -Pattern '^\- \*\*Agent\*\*:' |
  ForEach-Object { 'L' + $_.LineNumber + '|' + $_.Line.Trim() }

# 命令 4：提取所有 Duration 行
Select-String -Path "<file>" -Pattern '^\- \*\*Duration\*\*:' |
  ForEach-Object { 'L' + $_.LineNumber + '|' + $_.Line.Trim() }

# 命令 5：提取 activate_skill 的 name 参数
Select-String -Path "<file>" -Pattern '^\s+- `name`:\s*(.+)' |
  ForEach-Object { 'L' + $_.LineNumber + '|' + $_.Line.Trim() }
```

**将上述命令合并为一条执行**，以减少调用次数。

**关联规则**：每个工具调用头（命令 1 的结果）后面紧跟的 Status/Agent/Duration/Arguments 行属于该调用。用行号递增关系将它们关联。

#### 2.2 `.json` raw 格式的提取方法

JSON 中工具调用存在于两个位置：

**位置 A：`history[]` 数组**（简单格式）
```json
{
  "type": "tool_group",
  "tools": [
    {
      "callId": "xxx",
      "name": "Unity Asset Manager",    // displayName
      "status": "Success",              // "Success" | "Error"
      "resultDisplay": "..."
    }
  ]
}
```

**位置 B：`conversationRecord.messages[].toolCalls[]`**（详细格式）
```json
{
  "id": "xxx",
  "name": "unity_asset",               // tool_name
  "displayName": "Unity Asset Manager",
  "status": "success",                  // "success" | "error"
  "timestamp": "2026-03-28T09:25:51Z",
  "agentName": "3d-model-generator",    // 可选
  "agentId": "3d-model-generator-xxx",  // 可选
  "args": { "name": "unity-3d-model-generation" }  // activate_skill 时有 skill name
}
```

**优先使用位置 B**（字段更完整）；如果不存在则 fallback 到位置 A。

使用 shell 命令提取关键字段：

```powershell
# 提取所有 tool_name（位置 B）
Select-String -Path "<file>" -Pattern '"name":\s*"(unity_|execute_|read_file|write_file|glob|search_|run_shell|activate_skill|list_directory|replace|task|complete_task|analyze_multimedia|sequential_thinking|web_search|job_)' |
  ForEach-Object { $_.Line.Trim() }

# 提取所有 status
Select-String -Path "<file>" -Pattern '"status":\s*"(success|error|Success|Error)"' |
  ForEach-Object { $_.Line.Trim() }

# 提取所有 agentName
Select-String -Path "<file>" -Pattern '"agentName":\s*"([^"]+)"' |
  ForEach-Object { $_.Line.Trim() }
```

**⚠️ 注意**：`.json` 文件可能很大（10000+ 行），使用 `Select-String` 比逐行 `read_file` 高效得多。

---

### 第三步：数据聚合与计算（必须）

将第二步的原始数据聚合为以下维度：

#### 3.1 按工具聚合

对每个唯一的 `tool_name`，计算：

| 字段 | 计算方式 |
|------|---------|
| `total` | 该工具出现的总次数 |
| `success` | Status 为 success 的次数 |
| `error` | Status 为 error 的次数 |
| `failure_rate` | `error / total * 100%` |
| `avg_duration` | 有 Duration 数据的调用的平均耗时 |

#### 3.2 按 Skill 聚合

筛选 `tool_name == "activate_skill"` 的调用，从 Arguments 中提取 `name` 参数：

| 字段 | 计算方式 |
|------|---------|
| `skill_name` | activate_skill 的 name 参数值 |
| `count` | 该 Skill 被激活的次数 |
| `status` | 每次激活的成功/失败 |

#### 3.3 按 Agent 聚合

筛选带 `Agent` 字段的调用：

| 字段 | 计算方式 |
|------|---------|
| `agent_name` | Agent 字段值（如 "3d-model-generator"） |
| `total_calls` | 该 Agent 执行的总调用数 |
| `success` / `error` | 成功/失败数 |
| `top_tools` | 该 Agent 最常使用的 3 个工具 |

#### 3.4 汇总指标

| 指标 | 计算方式 |
|------|---------|
| 总工具调用数 | 所有工具调用的总数 |
| 整体成功率 | `success / total * 100%` |
| 整体失败率 | `error / total * 100%` |
| 唯一工具种类数 | 去重后的 tool_name 数 |
| Skill 激活总数 | activate_skill 的总调用数 |
| 唯一 Skill 种类数 | 去重后的 skill_name 数 |
| Sub-Agent 种类数 | 去重后的 agent_name 数 |
| 总执行耗时 | 所有 Duration 之和 |
| 平均单次耗时 | 总耗时 / 有耗时数据的调用数 |
| 最慢调用 | Duration 最大的那一次 |
| 高失败率工具（>30%）| failure_rate > 30% 的工具列表 |

---

### 第四步：生成报告（必须）

按以下固定结构输出。**汇总仪表盘必须在最前面。**

---

## 📋 报告输出格式（严格遵循）

````markdown
# 🔬 Session Profiling Report

> 日志文件: `<file_path>`
> 生成时间: <now>

---

## 📊 汇总仪表盘

| 指标 | 值 |
|------|-----|
| 📁 日志文件 | `<filename>` |
| 🆔 Session ID | `<id>` |
| 🕐 会话时长 | <duration> |
| 💬 消息总数 | <N> |
| 🔢 工具调用总数 | <N> |
| ✅ 成功 | <N> (<X>%) |
| ❌ 失败 | <N> (<X>%) |
| 📉 整体失败率 | <X>% |
| 🧰 工具种类数 | <N> |
| 🎯 Skill 激活总数 | <N> |
| 🎯 唯一 Skill 数 | <N> |
| 🤖 Sub-Agent 种类数 | <N> |
| ⏱️ 总执行耗时 | <X>s (<X>m) |
| ⏱️ 平均单次耗时 | <X>s |
| 🐢 最慢调用 | <tool_name> (<X>s) |
| 🔴 Token 消耗 | <N> |

### ⚠️ 高失败率工具（>30%）

| 工具 | 总数 | 失败 | 失败率 |
|------|------|------|--------|
| <tool> | <N> | <N> | <X>% |

---

## 📈 工具调用明细

| # | 工具名称 | 总数 | 成功 | 失败 | 失败率 | 平均耗时 |
|---|---------|------|------|------|--------|---------|
| 1 | <tool_name> | <N> | <N> | <N> | <X>% | <X>s |
| 2 | ... | | | | | |

（按 `总数` 降序排列）

---

## 🎯 Skill 激活明细

| # | Skill 名称 | 激活次数 | 成功 | 失败 |
|---|-----------|---------|------|------|
| 1 | <skill_name> | <N> | <N> | <N> |

**Skill 调用链**:（按时间顺序列出所有 activate_skill 调用）
```
<timestamp> → <skill_name> [✅/❌]
<timestamp> → <skill_name> [✅/❌]
```

---

## 🤖 Sub-Agent 调度明细

| # | Agent 名称 | 总调用 | 成功 | 失败 | 失败率 | Top 3 工具 |
|---|-----------|--------|------|------|--------|-----------|
| 1 | <agent> | <N> | <N> | <N> | <X>% | <tool(N)>, <tool(N)>, <tool(N)> |

---

## ⏱️ 耗时分析

### 耗时 Top 10（最慢）

| # | 工具 | 耗时 | 状态 | 时间戳 |
|---|------|------|------|--------|
| 1 | <tool_name> | <X>s | ✅/❌ | <timestamp> |

### 按工具平均耗时（降序）

| 工具 | 平均耗时 | 调用数 | 总耗时 |
|------|---------|--------|--------|
| <tool> | <X>s | <N> | <X>s |

---

## ❌ 错误清单

| # | 工具 | 时间戳 | Agent | 错误摘要 |
|---|------|--------|-------|---------|
| 1 | <tool_name> | <timestamp> | <agent/-> | <brief_error> |

---

## 💡 洞察与建议

基于以上数据，总结 3-5 条关键洞察：

1. **[类别]**: <描述> — <建议>
2. **[类别]**: <描述> — <建议>
3. ...

**洞察维度参考**：
- 失败率最高的工具是否有共同模式（如 TCP 超时、工具不可用）？
- Sub-Agent 中哪个效率最低（调用数多但失败率高）？
- 是否存在重复/冗余调用（如多次 get_current_state、重复 glob 扫描）？
- Skill 激活是否与预期匹配（如 3D 模型应该用 3d-model-generation skill）？
- 耗时异常的调用是否可优化？
````

---

## 强约束

1. **汇总仪表盘必须在最前面**：用户第一眼看到的必须是核心指标
2. **数据必须从日志中实际提取**：不得凭记忆或猜测填写数字
3. **使用 shell 命令批量提取**：对于 10000+ 行的日志，禁止逐行 `read_file`；使用 `Select-String`/`grep` + `Group-Object` 高效统计
4. **合并 shell 命令**：将多个 `Select-String` 合并为一条 PowerShell 命令（用分号分隔），减少工具调用次数
5. **百分比保留一位小数**：如 `89.6%`，不要写 `89.55555%`
6. **排序规则**：工具明细按总数降序；Skill 明细按激活次数降序；耗时按时长降序
7. **错误摘要**：从 Result 块中提取简短错误信息（一句话），不要复制完整 stacktrace
8. **洞察部分由 LLM 分析生成**：不是机械统计，而是结合上下文给出有价值的建议

---

## 高效提取命令参考

以下是经过验证的 PowerShell 命令模板，直接替换 `<file>` 即可使用：

### 一键提取 .md 格式关键数据

```powershell
$f = '<file>'

# 1. 工具名 + 计数
Select-String -Path $f -Pattern '^\*\*[^\*]+\*\*\s*\(' |
  ForEach-Object { if($_.Line -match '\(`([^`]+)`\)') { $matches[1] } } |
  Group-Object | Sort-Object Count -Descending |
  ForEach-Object { '{0,-40} {1}' -f $_.Name, $_.Count }

# 2. 状态分布
Select-String -Path $f -Pattern '^\- \*\*Status\*\*:\s*(\w+)' |
  ForEach-Object { if($_.Line -match 'Status\*\*:\s*(\w+)') { $matches[1].ToLower() } } |
  Group-Object | ForEach-Object { '{0}: {1}' -f $_.Name, $_.Count }

# 3. Agent 分布
Select-String -Path $f -Pattern '^\- \*\*Agent\*\*:\s*([^\(]+)' |
  ForEach-Object { if($_.Line -match 'Agent\*\*:\s*([^\(]+)') { $matches[1].Trim() } } |
  Group-Object | Sort-Object Count -Descending |
  ForEach-Object { '{0,-30} {1}' -f $_.Name, $_.Count }

# 4. Duration 值
Select-String -Path $f -Pattern '^\- \*\*Duration\*\*:\s*([\d.]+)' |
  ForEach-Object { if($_.Line -match 'Duration\*\*:\s*([\d.]+)') { [double]$matches[1] } }
```

### 一键提取 .json 格式关键数据

```powershell
$f = '<file>'

# 1. toolCalls 中的 tool name
Select-String -Path $f -Pattern '"name":\s*"' |
  Where-Object { $_.Line -match '"name":\s*"([a-z_]+)"' } |
  ForEach-Object { $matches[1] } |
  Group-Object | Sort-Object Count -Descending |
  ForEach-Object { '{0,-40} {1}' -f $_.Name, $_.Count }

# 2. status 分布
Select-String -Path $f -Pattern '"status":\s*"(success|error|Success|Error)"' |
  ForEach-Object { if($_.Line -match '"status":\s*"([^"]+)"') { $matches[1].ToLower() } } |
  Group-Object | ForEach-Object { '{0}: {1}' -f $_.Name, $_.Count }

# 3. agentName 分布
Select-String -Path $f -Pattern '"agentName":\s*"([^"]+)"' |
  ForEach-Object { if($_.Line -match '"agentName":\s*"([^"]+)"') { $matches[1] } } |
  Group-Object | Sort-Object Count -Descending |
  ForEach-Object { '{0,-30} {1}' -f $_.Name, $_.Count }
```

---

## 与其他 Skill 的关系

| Skill | 关系 |
|-------|------|
| `unity-log-analyzer` | Log Analyzer 提取**经验/bug 模式** ← Session Profiler 提取**过程指标** |
| `unity-game-debugger` | Debugger 读取经验库 ← Session Profiler 的洞察可补充经验库 |
| `unity-game-developer` | Developer 执行开发 → **Session Profiler 量化 Developer 的效率** |

**互补关系**：
```
Log Analyzer  → 定性分析（发现了什么 bug？学到了什么经验？）
Session Profiler → 定量分析（调了多少次工具？成功率多高？哪个 Agent 最慢？）
```
两者可对同一份日志分别运行，产出互补的报告。
