# Hunyuan 3.1 生成器文档

generator_id: `tencent-generation`  
适用场景：高精度 / hero 资产 / PBR 材质 / FBX 输出 / 高面数 3D 模型

---

## 何时选择 Hunyuan 3.1

- 高精度 hero 资产（武器主角、场景标志物）
- 需要 PBR 材质纹理（`enable_pbr: true`）
- 需要 FBX 格式输出
- 需要精细控制面数（`face_count` 范围 1000–2000000）

---

## 工具

### `generate_3d_model_by_tencent_generation`

启动 Hunyuan 3.1 生成任务。

**参数：**

| 参数 | 类型 | 必填 | 默认 | 说明 |
|------|------|------|------|------|
| `prompt` | string | 无图时必填 | — | 文本描述（≤1000字符） |
| `image_path` | string | 无提示词时必填 | — | Unity 资产路径（`Assets/...`）或绝对路径 |
| `prefab_output_path` | string | 否 | 自动生成 | 输出 prefab 路径（`.prefab` 自动添加） |
| `force_overwrite` | bool | 否 | false | 覆盖同路径已有 prefab |
| `face_count` | int | 否 | 500000 | 面数（1000–2000000）。越高越精细但越慢 |
| `enable_pbr` | bool | 否 | false | 启用 PBR 材质纹理 |
| `result_format` | string | 否 | `"GLB"` | 输出格式：`"GLB"` 或 `"FBX"` |
| `session_id` | string | 否 | — | 为占位符 prefab 添加 Session 标签 |

**返回（成功）：**

```json
{
  "success": true,
  "task_id": "static_model_1_...",
  "generator_id": "tencent-generation",
  "prompt": "wooden chair",
  "prefab_output_path": "Assets/TJGenerators/History/Model3D.prefab",
  "estimated_wait_seconds": 600,
  "notification_mode": "bg_task_done"
}
```

**返回（失败）：**

```json
{ "success": false, "error_code": "AUTH_REQUIRED", "message": "Not logged in..." }
```

调用前检查 `result["success"]`。若 `false`，立即上报错误，**不要**继续轮询。

---

### `query_3d_model_status_by_tencent_generation`

轮询任务状态。

**参数：**

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| `task_id` | string | 是 | `generate_3d_model_by_tencent_generation` 返回的 `task_id` |

**状态值：**

| Status | 含义 |
|--------|------|
| `initializing` | 任务已创建，等待后端任务 ID |
| `generating` | 后端生成中（进度 0–100%） |
| `recovering` | domain reload 后自动恢复中，继续轮询 |
| `completed` | 完成，模型已下载并绑定到 prefab |
| `failed` | 生成失败（查看 `error` 字段） |
| `interrupted` | domain reload 后丢失后端记录，需重新生成 |

> `interrupted` 处理：用 `generate_3d_model_by_tencent_generation` 加 `force_overwrite=true` 和相同 `prefab_output_path` 重新生成。

**返回（完成）：**

```json
{
  "success": true,
  "task_id": "static_model_1_...",
  "status": "completed",
  "progress": 100,
  "prefab_path": "Assets/TJGenerators/History/Model3D.prefab",
  "model_path": "Assets/TJGenerators/History/Model3D_model/Model3D_model.glb",
  "preview_url": "https://example.com/preview.png",
  "result_summary": "Generation completed. Model: Assets/...glb. Prefab: Assets/...prefab.",
  "end_time": "2026-03-13 10:35:22",
  "duration_seconds": 487
}
```

> **`preview_url`** — 渲染预览缩略图 URL，需直接展示给用户预览生成效果，可能为空。

**返回（进行中）：**

> ⚠️ Only call this as last-resort fallback — use `<bg_task_done>` notification instead.

```json
{
  "success": true,
  "status": "generating",
  "progress": 45
}
```

---

### `list_3d_model_tasks_by_tencent_generation`

列出当前 Unity Editor session 内的所有 Hunyuan 3.1 任务。

**参数：** 无

---

## 输入模式

| 模式 | 参数 | 适用场景 |
|------|------|---------|
| 文生3D | `prompt` | 文字描述生成 |
| 图生3D | `image_path` | 从参考图生成 |
| 文+图 | `prompt` + `image_path` | 带文字指导的参考图生成 |

---

## face_count 参考值

| 值 | 描述 |
|----|------|
| `50000` | 低面（快，适合背景道具） |
| `200000` | 中等（常规使用） |
| `500000` | 高细节（默认，平衡） |
| `1000000+` | 极高（慢，适合 hero 资产） |

---

## Domain Reload 恢复行为

domain reload（脚本编译）发生时：

- 任务状态持久化到 `Library/AI.TJGenerators/InterruptedTasks.json`
- 重新加载后，轮询在后台自动恢复
- 状态短暂显示 `"recovering"`，继续轮询即可
- 生成正常完成，模型下载并绑定到 prefab

> **注意：** 需要 C# 操作时，使用 `execute_csharp_script`，**不要**把 `.cs` 文件写入磁盘（会触发不必要的 domain reload）。

---

## 通知与 Fallback 查询

- **主要方式**：等待 `<bg_task_done>` 通知自动到达（不要轮询）
- **Fallback**：仅当通知未到达时，调用 `query_3d_model_status_by_tencent_generation` **一次**
- 绝不在循环中调用 query 工具
