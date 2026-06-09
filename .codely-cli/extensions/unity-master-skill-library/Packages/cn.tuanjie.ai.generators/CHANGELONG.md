# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.6] - 2026-05-15

### Added

- 各类生成工具由轮询改为基于 `bg_task_done` 的推送通知
- 资源搜索下载流程改为异步通知模式
- 表面材质模板选择器支持紧凑列表视图

### Fixed

- URP 下 3D 预览材质显示为粉色的问题
- 腾讯系接口面数字段与平台约束对齐
- 图片生成尺寸文案更新；移除质量下拉与 WebP 输出选项
- 图片生成在未填写用户文本提示时拦截并优化错误提示
- 天空盒预览应用到场景时使用材质副本，避免意外改动共享材质
- 生成历史与占位资源绑定及音频工具抽取相关行为
- Frontier 游戏设计图生成器 `numImages` 与 Frontier 2D 序列输出目录
- 条件编译下遗漏的清除全部生成历史逻辑
- 额度刷新与部分输入框配色
- 下载日志补充 `unitypackage_url`

### Changed

- 生成管线与自定义工具整体改进；MP4 音频统一规范为 M4A 并整理音频资产处理
- JSON 序列化迁移至 `Codely.Newtonsoft.Json` 命名空间
- 品牌与 Unity 菜单：团结 AI / Tuanjie AI 展示与路径重组（含菜单自 `Window/Tuanjie AI` 调整为 `AI`）
- UI 缩放与共用样式拆分、整合（含天空盒 `SetImagePath` 等调用简化）

## [1.0.5] - 2026-05-11

### Added

- 新增带骨骼动作的 3D 模型生成：`generate_rigged_animated_model` 自定义工具及配套 Codely skill
- 背景音乐生成结果可在编辑器 Play 模式下自动开始播放（并支持 `play_on_awake` 开关）
- 资源库搜索结果改为卡片式布局与新版界面
- 若工程里已导入过同一下载包（按网址识别），不再重复下载

### Fixed

- 会话里保存的搜索结果条数在合并缓存时不再被截断
- 带动画的 FBX：先等资源库刷新完毕再写导入选项，Animator 默认状态机更稳妥
- 勾选动作相关生成时，不再强行套用模型的缩放与旋转，避免姿态异常
- 各窗口文本框统一用编辑器专用输入，减少焦点与 IME 等问题
- 图片参考窗口偶现文件占用与历史记录错乱

### Changed

- 生成历史存盘方式调整，管线里的请求数据结构单独整理
- 原先手写拼 JSON 的逻辑改为 Newtonsoft 解析与生成
- 左侧栏等共用间距与样式常量集中管理，脚本目录按职责重新归类
- 资源搜索里已在本工程中的条目可直接「置入场景」，不必再点下载
- 上述「已导入」判断会读落地后的元数据，并与下载任务状态一致

## [1.0.4] - 2026-04-30

### Added

- 资源库搜索窗口：场景置入、GIF 动图预览
- Domain 重载与 Play 模式下持久化搜索结果
- 3D 生成、贴图与模型选择等窗口新版 UI（输入框、图片上传、下拉等）

### Fixed

- TaskRecovery：加载时清理失败/异常任务
- 资源搜索预览占位文案；移除演示窗口并修正相关警告
- 历史面板等区域用 IMGUI 跟踪鼠标，替代 `Input.GetMouseButton`，避免编辑器下交互异常
- 图片生成器、按钮九宫格切片、额度检查等 UI 问题

### Changed

- 资源搜索与多类生成窗口界面整体改版
- 窗口矩形与布局逻辑集中到 UIComponents；历史面板布局计算集中维护
- 进一步将弹窗提示改为控制台输出；搜索参数与窗口初始化简化
- 内联整合 TaskRecovery 辅助逻辑

## [1.0.3] - 2026-04-24

### Added

- 视频 / Seedance 与序列相关生成与窗口
- 资源搜索迁 Codely 并整体优化下载与筛选
- 地形高度图自定义工具
- UniRig + 混元动作后处理
- 多视图图生及无水印、游戏设计图等生成选项
- 模型项目内选资源上传与 Tripo 变体支持

### Fixed

- 导入、下载、预览与 API 配置（分辨率、字段名、时序与错误处理等）
- 图片与序列、Rodin/材质与渲染管线相关若干问题

### Changed

- 主菜单「AI生成」→「Codely AI」
- 部分错误由弹窗改为控制台
- 去除 Burst 与部分弃用选项
- 3D 工具合并/重命名、上传与动捕选项整理
- 子模块与扩展更新
- npm 包剔除无关内容

## [1.0.2] - 2026-04-10

### Added

- Tripo P1：会话 `session_id` 支持，以及对应 UI 与自定义工具流程。
- Tripo 生成器：`base_model` 字段支持。
- 自高度图一键生成地形。

### Fixed

- 音频保存路径问题；按生成器驱动的音频格式处理。
- 序列精灵资源在完成时正确打上 `TJGeneratorsAIGenerated` 标签。
- IME 输入时占位符重叠问题。
- 为请求补充 `DefaultRequestHeaders` 的 `Accept`。
- 自定义程序集启用 `overrideReferences` 相关修正。

### Changed

- `session_id` 已接入各自定义生成与下载工具。

## [1.0.1] - 先前版本

- 基础 AI 资产生成功能与依赖（Codely Bridge、GLTFast、Newtonsoft.Json 等）。
