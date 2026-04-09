#!/usr/bin/env bash
# 结束本机所有 godot-mcp-pro v1.10.1 Node 进程（与 Cursor 多会话争用 6505 时可用）。
set -euo pipefail
pkill -f "/godot-mcp-pro-v1.10.1/server/build/index.js" 2>/dev/null || true
echo "已结束 godot-mcp-pro (v1.10.1) Node 进程。请在 Cursor 中重载 MCP（godot-mcp-pro）。"
