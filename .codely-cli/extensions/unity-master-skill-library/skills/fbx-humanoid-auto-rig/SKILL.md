---
name: fbx-humanoid-auto-rig
description: 使用 UniRig 大模型对任意人形 FBX 自动生成 skeleton，并在后处理中按“语义+拓扑”规则重命名为 Unity Humanoid 兼容骨名（不再依赖固定 bone_0~bone_27 索引映射）；随后生成 skin 权重（本技能默认推荐 Blender Auto Weights，UniRig skin 作为可选尝试），最后将 skin 结果 merge 回原始有 UV 的模型（UV merge），输出可直接用于引擎渲染与动画的 rigged 模型。当用户请求“自动绑定人形骨骼 / 生成 humanoid / 修复骨骼命名不兼容 / 让模型动起来”时使用。默认关联全局 Python 工程路径 /Users/yixianliu/Project/project/agent_list/UniRig，可通过参数覆盖。
---

# FBX 人形自动骨骼绑定（UniRig + 语义Humanoid命名 + Skin）

## 固定 Python 环境（必须）
统一使用：

- `unirig_root`: `/Users/yixianliu/Project/project/agent_list/UniRig`
- `python_bin`: `/Users/yixianliu/Project/project/agent_list/UniRig/.venv311/bin/python`
- 执行前将 PATH 前置到该环境：

```bash
export UNIRIG_ROOT=/Users/yixianliu/Project/project/agent_list/UniRig
export PYTHON_BIN=$UNIRIG_ROOT/.venv311/bin/python
export PATH=$UNIRIG_ROOT/.venv311/bin:$PATH
```

## 预下载模型文件（建议先执行）
运行：

```bash
$PYTHON_BIN {baseDir}/scripts/prepare_unirig_model_assets.py \
  --unirig-root $UNIRIG_ROOT
```

下载后文件会放在 UniRig 下（固定路径）：

- `$UNIRIG_ROOT/pretrained/facebook_opt_350m/config.json`
- `$UNIRIG_ROOT/pretrained/VAST-AI_UniRig/skeleton/articulation-xl_quantization_256/model.ckpt`
- `$UNIRIG_ROOT/pretrained/VAST-AI_UniRig/skin/articulation-xl/model.ckpt`（可选）
- `$UNIRIG_ROOT/pretrained/VAST-AI_UniRig/skin/skeleton/model.ckpt`（可选）

## 一键执行（骨骼+命名）
运行：

```bash
$PYTHON_BIN {baseDir}/scripts/run_unirig_humanoid_pipeline.py \
  --input-fbx /绝对路径/xxx.fbx \
  --output-dir /绝对路径/输出目录 \
  --unirig-root $UNIRIG_ROOT \
  --require-local-assets
```

## 一键执行（在 Humanoid 基础上生成可动画 skin，默认推荐 Blender Auto Weights）
运行：

```bash
$PYTHON_BIN {baseDir}/scripts/run_unirig_humanoid_skin_pipeline.py \
  --input-humanoid-fbx /绝对路径/model_unirig_generate_skeleton_humanoid_named(.fbx|_vN.fbx) \
  --output-dir /绝对路径/输出目录 \
  --unirig-root $UNIRIG_ROOT \
  --force-autoweight
```

推荐完整链路（skin + UV merge）：

```bash
$PYTHON_BIN {baseDir}/scripts/run_unirig_humanoid_skin_pipeline.py \
  --input-humanoid-fbx /绝对路径/model_unirig_generate_skeleton_humanoid_named(.fbx|_vN.fbx) \
  --output-dir /绝对路径/输出目录 \
  --unirig-root $UNIRIG_ROOT \
  --force-autoweight \
  --merge-target /绝对路径/原模型.glb|fbx \
  --merge-output /绝对路径/model_rigged.glb|fbx
```

## 默认全局路径
- `unirig_root` 默认：`/Users/yixianliu/Project/project/agent_list/UniRig`
- `python_bin` 默认：`{unirig_root}/.venv311/bin/python`
- 说明：脚本已避免将该路径解析为 pyenv base，确保按 venv 语义执行。

## 输入约束
1. 使用绝对路径输入 FBX。
2. 输入必须是人形模型（非人形会导致 Humanoid 重命名语义不成立）。
3. 禁止直接改 Unity 的 `.meta/.prefab/.unity` YAML。

## 输出约定
1. 大模型骨架结果：`<模型名>_unirig_generate_skeleton.fbx`
2. Humanoid 命名结果（自动命名）：`<模型名>_unirig_generate_skeleton_humanoid_named.fbx`  
   说明：若同名文件已存在，会自动追加版本号 `..._vN.fbx`。
3. Skin 中间结果（可动画，但不保证保留原始 UV）：`<模型名>_unirig_generate_skin_humanoid_named(.fbx|_vN.fbx)`
4. UV merge 后最终结果（用于引擎渲染+动画）：`<模型名>_rigged.glb|fbx`（或 `--merge-output` 指定路径）

## 执行流程
1. 检查 UniRig 根目录、固定 Python 环境与关键依赖。
2. 预下载并落盘到 `UniRig/pretrained`（避免运行时临时拉取）。
3. 自动生成本地 CPU 配置（`*_cpu_local.yaml`），优先引用 `UniRig/pretrained` 下的本地模型路径。
4. 执行 UniRig `generate_skeleton`。
5. 运行语义重命名脚本：按骨架拓扑和空间关系识别 Hips/Spine/四肢/手指并重命名；补 `LeftEye/RightEye/Jaw`；同时清理剩余 `bone_xx` 命名。
6. Skin 阶段默认推荐 `Blender Armature Auto Weights`（`--force-autoweight`），保证稳定产出可动模型。
7. 将 skin 结果 merge 到原始模型（UV merge），得到保留原始 UV/贴图采样的最终 rigged 模型。
8. 对 merge 结果执行验证（UV、骨骼、材质显示）后，再进入后续动画/场景验证。

## 常用参数
- `--unirig-root`：覆盖默认 UniRig 全局路径。
- `--python-bin`：指定执行 Python。
- `--seed`：控制 skeleton 生成随机性。
- `--auto-install`：自动安装缺失依赖（耗时较长）。
- `--require-local-assets`：要求必须使用 `UniRig/pretrained` 本地资源，否则直接失败。
- `--disable-autoweight-fallback`：禁用 skin 阶段 Blender 自动权重回退。
- `--force-autoweight`：跳过 UniRig skin，直接执行 Blender `Armature Auto Weights`。
- `--merge-target`：skin 结果 merge 的目标模型（通常是原始有 UV 的 `glb/fbx`）。
- `--merge-output`：merge 输出路径（建议作为最终可用模型路径）。

## 直接调用重命名脚本
若你已拿到 `generate_skeleton` 结果，仅做命名改写时运行：

```bash
python {baseDir}/scripts/rename_unirig_skeleton_to_humanoid.py \
  --input /绝对路径/model_unirig_generate_skeleton.fbx \
  --output /绝对路径/model_unirig_generate_skeleton_humanoid_named.fbx
```

## 策略说明（重要）
1. 已废弃旧的“固定 `bone_0~bone_27` 映射”作为默认方案。
2. 当前默认方案为语义+拓扑命名：优先保证 Humanoid 主链正确，并尽量标准化手指链。
3. 若模型拓扑异常导致局部骨骼无法语义识别，脚本会使用 `ExtraBone_*` 命名兜底，避免残留 `bone_xx`。
4. 想要“模型能动起来”，仅有 skeleton+命名不够，必须继续做 skin 权重映射。
5. 本技能默认推荐 Blender `Armature Auto Weights` 作为 skin 执行策略（通过 `--force-autoweight` 明确触发）。
6. 想要“模型动起来且贴图正常”，不能停在 skin 中间产物；必须执行 UV merge，把权重/骨架合并回原始有 UV 模型。

## UV Merge 规则（关键）
1. `source` 使用 skin 结果（包含骨架与权重）。
2. `target` 使用原始可渲染模型（包含正确 UV 与贴图拓扑）。
3. `output` 作为最终引擎资产；后续动画、材质、场景挂载都应使用该 `output`。
4. 若出现“rig 后全黑/贴图异常”，优先检查是否遗漏 UV merge，或 `merge-target` 误传为无 UV 资产。

## 完成后验证（必须）
1. 结构验证：模型包含 Armature + Mesh，且 Mesh 仍有 UV（`uv0 > 0`）。
2. 动画验证：在 Unity Humanoid 下可播放动作，角色跟随控制信号移动。
3. 渲染验证：在 Lit/URP Lit 材质下贴图正常，不出现全黑模型。
4. 场景验证：将 merge 后模型应用到目标场景后截图确认（如 `TifaLitPreviewScene` 或战斗场景）。
