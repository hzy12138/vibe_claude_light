# Claude Code Traffic Light 🚦

> A desktop traffic light indicator for Claude Code on Windows — see at a glance whether Claude is working, waiting, or done.

[中文说明](#中文) | [Install](#quick-start) | [How It Works](#how-it-works)

![screenshot](https://img.shields.io/badge/platform-Windows%2010%2F11-blue) ![license](https://img.shields.io/badge/license-MIT-green) ![dotnet](https://img.shields.io/badge/.NET-8.0-purple) ![status](https://img.shields.io/badge/status-active-brightgreen)

**The problem:** You're coding with Claude Code in VSCode, tab away to do something else, and have no idea if Claude finished, hit a permission dialog, or crashed. You keep alt-tabbing back to check.

**The solution:** A tiny traffic light overlay on your screen. 🔴 Red = working. 🟡 Yellow = needs you. 🟢 Green = done. Zero keystrokes needed.

---

## Features

- **Real-time status** — hooks into Claude Code's lifecycle via native hooks
- **Multi-project support** — one light per project, auto-arranged
- **Permission alert** — distinct yellow blink when Claude needs your approval
- **System tray** — lives in your tray, left-click to see running projects
- **Auto-start** — optionally launch on Windows startup
- **Zero config** — double-click, hooks auto-install, done

### Compared to macOS alternatives

| | Claude Light | [claude-status-bar](https://github.com/m1ckc3s/claude-status-bar) | [Claude Status](https://github.com/gmr/claude-status) |
|---|---|---|---|
| Platform | **Windows** | macOS | macOS |
| Visual | Traffic light window | Menu bar icon | Menu bar + widget |
| Multi-project | ✅ Per-project window | ✅ Aggregated | ✅ Dropdown list |
| Permission alert | 🟡 Red+yellow blink | 🟡 Yellow dot | 🟠 Orange dot |
| Auto-install hooks | ✅ | ✅ | ✅ |

---

## Quick Start

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
# 1. Build
cd ClaudeLight.CSharp
dotnet publish -c Release --self-contained -r win-x64 -o publish/

# 2. Run
./publish/ClaudeLight.exe

# 3. Use Claude Code normally — lights appear automatically
```

Or download the latest pre-built ZIP from [Releases](https://github.com/hzy12138/vibe_claude_light/releases).

---

## How It Works

```
┌─────────────────────────────────────────────────────────┐
│ Claude Code                                              │
│   PreToolUse ──→ ClaudeLight.CLI.exe hook running ──┐    │
│   PostToolUse ──→ ClaudeLight.CLI.exe hook done  ──┤    │
│   PermissionReq ─→ ClaudeLight.CLI.exe hook confirm ┤    │
│   Stop ────────→ ClaudeLight.CLI.exe hook idle ────┤    │
└─────────────────────────────────────────────────────┘    │
                                                      │    │
                    ┌─────────────────────────────────┘    │
                    ▼                                      │
          ~/.claude-lights/                                │
          {project}.json                                   │
                    │                                      │
                    ▼                                      │
          FileSystemWatcher ──→ Traffic Light GUI          │
                                  🔴 🟡 🟢                │
└─────────────────────────────────────────────────────────┘
```

Four hooks, one file per project, real-time file watching. No polling, no server, no cloud.

---

## Light States

| Light | Meaning |
|-------|---------|
| 🔴 **Red (solid)** | Claude is actively working (tools or thinking) |
| 🔴🟡 **Red + Yellow (blink)** | Claude needs your permission — open Claude Code |
| 🟢 **Green (solid)** | Claude finished responding |
| ⚫ **Off** | No Claude Code session active |

---

## Tray Menu

| Click | Action |
|-------|--------|
| **Left-click** | Show running project count & list |
| **Right-click** | Context menu (layout, visibility, auto-start, exit) |
| **Double-click** | Show/hide all windows |

---

## Uninstall

```powershell
./publish/ClaudeLight.Clean.exe --force
```

---

## 中文

### Claude Code 红绿灯

一个 Windows 桌面红绿灯指示器，实时显示 Claude Code 的工作状态。用 Claude Code 写代码时切屏干别的，瞄一眼就知道 Claude 是正在跑、卡权限了、还是已经完成了。

**快速开始：** 装 .NET 8 SDK → `dotnet publish` → 双击 `ClaudeLight.exe` → 搞定。

详细文档：[docs/操作指南.md](docs/操作指南.md)

### 灯色说明

| 灯色 | 含义 |
|------|------|
| 🔴 红灯常亮 | 工作中 |
| 🔴🟡 红底+黄闪 | 需要你确认权限 |
| 🟢 绿灯常亮 | 任务完成 |
| ⚫ 全灭 | 未运行 |

---

## License

MIT © hzy12138
