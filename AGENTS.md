# Repository Guidelines

## Project Structure & Module Organization
这是一个 Godot 4.6 + C# 的移动向项目，入口配置在 `project.godot`，主场景为 `res://scenes/main.tscn`。核心代码放在 `scripts/`：`autoload/` 为全局单例，`ui/` 为界面脚本，`systems/` 为玩法系统，`models/` 为数据模型。场景文件在 `scenes/`，手动测试场景在 `scenes/tests/`。运行时资源在 `resources/`，其中 `prompts/` 存 LLM 模板，`locations/` 存 JSON 数据；美术与音频素材在 `assets/`。`addons/` 为第三方或编辑器插件，非必要不要随意修改。

## Build, Test, and Development Commands
使用 Godot 编辑器开发时，运行 `godot --editor --path .` 打开项目。命令行启动主流程可用 `godot --path .`。C# 编译检查使用 `dotnet build RedHackathon.sln`；提交前至少跑一次，确保 `.csproj` 与场景绑定没有断裂。SVG 插件回归测试目前为手动方式：在编辑器中打开 `scenes/tests/svg_plugin_test.tscn` 后按 `F6`。

## Godot MCP Pro（Cursor）
每个 Cursor 会话会各启一套 `node …/godot-mcp-pro-v1.10.1/server/build/index.js`，但 **6505 只能被一个进程监听**；多会话会导致除第一个外的 MCP「假存活」、工具报 Godot 未连接。已在本仓库的 MCP 服务端加入 **端口占用即退出** 的防护；若出现连接异常，可先运行 `scripts/kill_godot_mcp_servers.sh`，再在 Cursor 里 **只保留一个** 使用 MCP 的聊天并重载 `godot-mcp-pro`，并确保 Godot 已打开本项目。

## Coding Style & Naming Conventions
遵循现有代码风格：文件统一 UTF-8；C# 与 GDScript 保持仓库内现有缩进风格（当前脚本以制表符为主）。C# 类型、方法、属性使用 `PascalCase`，私有字段使用 `_camelCase`，如 `ApiBridge`、`_httpRequest`。场景、资源和多数脚本文件名使用小写 snake_case，如 `death_registration.tscn`。新增场景时，脚本名应与主要节点职责一致。

## Testing Guidelines
当前仓库未配置独立单元测试框架，主要依赖 `dotnet build`、Godot 编辑器报错和手动场景走查。涉及 UI 或流程修改时，至少验证 `scenes/main.tscn` 和受影响场景；涉及 SVG 或资源加载时，追加验证 `scenes/tests/svg_plugin_test.tscn`。若新增可复现问题，优先补一个最小测试场景到 `scenes/tests/`。

## Commit & Pull Request Guidelines
当前分支还没有提交历史，请从现在开始使用简洁、祈使式提交信息，例如 `Add phone UI battery sync` 或 `Fix settings save fallback`。PR 应说明修改范围、手动验证步骤、是否影响 `user://settings.json`/`save_data.json`，UI 变更附截图，资源或提示词改动注明对应路径。

## Security & Configuration Tips
API Key 和 Base URL 通过 `user://settings.json` 管理，不要硬编码到脚本、场景或提交到仓库。不要手工编辑 `.godot/` 缓存内容；如遇导入异常，优先重开项目或清理本地缓存后重新导入。
