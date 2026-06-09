## 1. 背景说明
当前 Unity 游戏首次运行进入“序章关卡”，序章结束后进入主菜单。
现在要把项目修改为“微信试玩广告”版本，满足以下约束，并以模块化方式执行，确保每个模块结束时 Unity 编译 0 error。

## 2. 业务需求（必须全部满足）
- 纯单机：不得包含任何网络请求与云服务相关接口/逻辑/SDK/Package
- 无存档：不需要保存任何玩家进度/游戏状态/本地配置/存档数据/云存档数据，即：本地存档与云存档（相关系统、SDK、读写逻辑全部移除或替换为无状态实现）。删除任何保存，读取，修改游戏进度的逻辑。
- 无广告：不得包含任何广告相关接口/逻辑/SDK/Package，包括但不限于：激励视频广告、插屏广告、banner广告、开屏广告、信息流广告等全部移除或禁用。不需要有任何关于广告的UI，移除任何相关按钮，弹窗，提示语，界面元素等。
- 无统计：不得包含任何数据打点/统计相关接口/逻辑/SDK/Package，包括但不限于：用户行为统计、崩溃统计、性能统计、热力图等全部移除或禁用
- 无远程配置：不得包含任何远程配置相关接口/逻辑/SDK/Package，包括但不限于：远程配置、热更新、热更、动态资源加载。**原因**：试玩版必须完全离线运行，运行时不得依赖云端下发的配置/参数。若需要以“本地 hardcode / 本地配置表”替代远程配置，**必须基于项目内可追溯的可信依据**（例如 ScriptableObject、本地 JSON/CSV、默认常量、注释/策划表）；若找不到足够依据，**宁可禁用相关功能或将模块标记为 blocked 并记录原因**，禁止编造配置值。
- 只包含序章：每次启动都从序章开始；序章结束则试玩结束（不进入主菜单、不进入后续关卡）。

## 3. 技术约束
- **优先保证游戏编译通过**，所有模块执行后，Unity 编译 0 error
- **禁止删除资源**，包括但不限于：Shader、Texture、Audio、Animation、Prefab、Script、Scene、Config、Data、AssetBundle、Manifest、Resource 等资源文件，可以注释或者删除引用，但是禁止删除资源本身。
- 项目已有备份，不要做任何备份工作
- **Plan 和 MODULE 都不得违反以上硬约束**：ExecutionPlan 中的 steps/rollback，以及 MODULE 实际执行时，禁止出现物理删除 `Assets/` 下资源（包括脚本/场景）或复制整个工程做备份（如 robocopy 整个 Assets/ProjectSettings/Packages）的操作；如果需要“移除某系统/演示场景/商店/社交”等，必须通过“逻辑下线”（移除引用、从 Build Settings 中移除场景、脚本 stub/禁用入口 UI 等）来实现，资源文件本身必须保留。
- 每个 MODULE 执行完必须生成一个checkpoint，以git commit形式方便回溯。

## 4. 执行流程（主控 Orchestrator 的职责）
### 4.1 生成计划（PLAN 子代理，必须先做）
- 主控调用 `editor-plan-agent`，传入：`global_requirements_md`（即本文件全文）和 `execution_plan_path="ExecutionPlan.md"`，要求它分析项目目录并生成/更新 `ExecutionPlan.md`
- PLAN 子代理**只允许写入 `ExecutionPlan.md`**，不得改动任何其他项目文件/资源/包
- PLAN 必须严格遵守第 2、3 节的约束：不得在计划中设计“物理删除资源/Scene/Script”或“创建工程级备份（如 robocopy 整个 Assets/ProjectSettings/Packages）”等步骤，移除系统一律通过“逻辑下线”（移除引用/从 Build Settings 移除/脚本 stub）
- 模块应尽量做到“自包含、无跨模块依赖”。如某些步骤存在天然先后关系（例如先移除 package 再改代码），应把前置步骤写入同一模块 steps 中，或在计划中标注推荐执行顺序，但不要把“必须先执行模块 A 才能执行模块 B”作为硬依赖。

### 4.2 逐模块执行（MODULE 子代理，严格一模块一调用）
- 主控读取 `ExecutionPlan.md`，按顺序取出模块列表
- 对每个模块：
  - 主控调用 `editor-module-agent`，传入：`global_requirements_md`、`execution_plan_path`、`module_id`、`module_title`、`module_instructions`（来自 `ExecutionPlan.md`）
  - MODULE 子代理必须在本次调用内完成该模块并更新 `ExecutionPlan.md` 的模块状态
  - 如果 `module_instructions` 中存在与第 2、3 节约束冲突的指令（例如“物理删除 Assets 下资源”“用 robocopy 备份整个工程”等），MODULE 子代理必须**以全局约束为准进行调整**：把这些内容视为“高层意图”，只做“逻辑下线”或其他安全替代实现，并在 `record` 中写明实际执行内容
  - MODULE 子代理必须保证结束时 Unity 编译 0 error（否则必须在调用内修复到 0 error），且 0 error 的判据必须满足第 3 节的“focus→clear→read”真实依据链路
  - MODULE 子代理在调用 `complete_task` 结束前，必须在参数里回传一个可机读的 `handoff_json`（用于主控串联后续模块），至少包含：`module_id/module_title/status/changes/compile_gate(passed+since_token+error_entries_count=0)/notes`

### 4.3 全流程最终verification（额外最后一道防护，必须执行）
- 当 `ExecutionPlan.md` 中所有业务模块都完成后，主控必须再执行一次“最终编译verification”，确保整体收尾时仍然 **compile error=0**。
- 为了节省 main context，推荐把它作为 `ExecutionPlan.md` 的最后一个模块（例如 `M99_FinalCompileGate`）。
- 最终verification推荐使用专用 subagent：`UnityCompileGateAgent`，它会强制执行 **Console focus → clear → compile/wait → focus → read** 循环，直到 error/exception 为空才允许 `complete_task`，并回传 `handoff_json` 证据。
- 最终verification的验证链路必须与第 3 节一致：**Console focus → clear → compile/wait → focus → read（error/exception 为空）**；如有 error 必须修复并重复，直到通过后才算整个流程完成。

## 5. ExecutionPlan.md 的要求（便于主控分发 MODULE）
ExecutionPlan.md 必须把工作拆成互相独立、无依赖的模块；每个模块必须包含：
- module_id（如 M01）
- module_title
- module_scope（范围，推荐描述“模块目标+影响范围+背后原因”，而不是只写具体文件列表）
- steps（建议性的执行步骤，重点说明“要达成的效果/原因/策略方向”，并要求 **Discovery→Decision→Implementation→Verification** 的结构；**禁止把 steps 写成“必须修改某个具体文件/目录”的清单**，因为 PLAN 阶段分析可能不完整）
- verification（至少包含“编译 0 error”的验证步骤；必须使用 **Console focus→clear→read** 的真实依据链路，并用 since_token 做 console 新旧隔离）
- rollback（最小改动优先）
- status（pending / in_progress / completed）
- record（完成后写入：改动的文件/资源/包、关键日志摘要、验证结果、以及 subagent `handoff_json` 的关键字段/证据）

补充要求：
- ExecutionPlan.md 必须包含一个最终模块（建议 `M99_FinalCompileGate`）：只做最终verification（不再做业务改动）。推荐由 `UnityCompileGateAgent` 执行，并在通过后记录“最终编译验证通过”的证据（token、读取到的 error 条目数等）。

> 说明：PLAN 阶段的分析能力和上下文有限，ExecutionPlan 中的 steps/instructions 只能视为“高层任务目标与建议方向”，不能保证已经穷尽所有受影响代码；MODULE 子代理在执行时必须先对齐模块目标与原因，再基于真实代码结构重新设计/修正自己的行动方案。如因缺乏可信依据（例如找不到本地配置来源、无法确定 hardcode 数值含义）而无法安全执行某些变更，MODULE 子代理应当宁可标记本模块为 blocked 并记录原因，也不要“编造配置”或破坏运行逻辑。

补充格式约定（用于防御“文件级误导”）：
- 如果确实需要提到可能相关的文件/类/路径，只能放在模块中的一个独立小节：**`possible_touchpoints`（非绑定线索）**，并明确标注“仅供搜索定位参考，以 discovery 结果为准”；不得将其写成 steps 里的硬指令。

## 6. 约束摘要（便于子代理抽取，不新增额外约束）
本节只是对前文第 2、3 节约束的**机读化摘要**，不引入新的约束；如有歧义，以第 2、3 节的自然语言描述为准。

```json
{
  "constraints": {
    "network": "must_not_use",               // 纯单机：不得包含任何网络请求与云服务接口/SDK/Package
    "cloud_services": "must_not_use",        // 包括云存档、远程配置、云诊断等所有云服务
    "save_system": "must_not_persist",       // 无存档：不保存本地/云存档，可使用无状态或临时内存
    "ads": "must_not_use",                   // 无广告：移除或禁用所有广告相关系统/SDK/逻辑
    "analytics": "must_not_use",             // 无统计：移除或禁用所有打点/统计/崩溃统计等
    "remote_config": "must_not_use",         // 无远程配置/热更新/动态远程资源加载
    "hardcode_values": "must_be_evidence_based_or_block", // 如需本地 hardcode 替代外部来源配置/参数：必须有可信依据，否则禁用或 blocked，禁止编造数值
    "only_prologue": "must_start_and_end_in_prologue", // 每次启动从序章开始，序章结束即试玩结束
    "delete_assets": "forbidden",            // 禁止物理删除 Assets 下的资源/脚本/场景等，只能移除引用或逻辑下线
    "project_backup": "forbidden",           // 项目已有备份，不要再做任何工程级备份操作
    "compile_errors": "must_be_zero"         // 所有模块执行后 Unity 编译 error/exception 必须为 0
  }
}
```
