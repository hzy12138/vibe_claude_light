# Claude Code 红绿灯 🚦

> Windows 桌面红绿灯指示器 — 瞄一眼就知道 Claude Code 是在工作、等你确认、还是已完成。

A desktop traffic light indicator for Claude Code on Windows. [English](#english)

![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue) ![license](https://img.shields.io/badge/license-MIT-green) ![dotnet](https://img.shields.io/badge/.NET-8.0-purple)

---

## 痛点

用 Claude Code 写代码，切屏干别的，总得 alt-tab 回去看它跑完没、是不是卡权限了、还是崩了。反复切来切去很烦。

## 解决方案

屏幕角落放一个小红绿灯。🔴 红灯 = 工作中。🟡 黄闪 = 需要你确认。🟢 绿灯 = 完成了。不用切屏，瞟一眼就行。

## 灯色说明

| 灯色 | 含义 |
|------|------|
| 🔴 红灯常亮 | Claude Code 正在执行任务（工具调用或思考中） |
| 🔴🟡 红底+黄灯闪烁 | Claude Code 弹出权限确认框，需要你去确认 |
| 🟢 绿灯常亮 | 任务完成，Claude Code 空闲等待下一条消息 |
| ⚫ 全灭 | Claude Code 未运行 |

## 功能

- **实时状态** — 通过 Claude Code 原生 hooks 获取状态，非轮询
- **多项目支持** — 每个项目独立窗口，窗口显示项目名和路径，自动偏移排列不重叠
- **权限提醒** — 需要确认时红底+黄闪，醒目但不碍眼
- **系统托盘** — 常驻托盘，左键查看运行项目，右键切换布局/显示隐藏/开机自启/退出
- **自动安装 hooks** — 双击启动，自动写入 `~/.claude/settings.json`，无需手动配置
- **双击切换布局** — 横排/竖排随意切换
- **拖拽移动** — 窗口可拖到任意位置

## 快速开始

**环境要求:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
# 1. 编译
cd ClaudeLight.CSharp
dotnet publish -c Release --self-contained -r win-x64 -o publish/

# 2. 运行
./publish/ClaudeLight.exe

# 3. 正常使用 Claude Code，红绿灯自动出现
```

或者下载预编译 ZIP：[Releases](https://github.com/hzy12138/vibe_claude_light/releases)，解压双击 `ClaudeLight.exe` 即可，无需安装 .NET。

## 工作原理

```
┌──────────────────────────────────────────────────────────┐
│ Claude Code                                               │
│   PreToolUse ────→ CLI.exe hook running ──┐               │
│   PostToolUse ───→ CLI.exe hook done  ────┤               │
│   PermissionReq ─→ CLI.exe hook confirm ──┤               │
│   Stop ─────────→ CLI.exe hook idle ──────┤               │
└────────────────────────────────────────────┼──────────────┘
                                             │
              ┌──────────────────────────────┘
              ▼
    ~/.claude-lights/{项目路径}.json
              │
              ▼
    FileSystemWatcher 实时监控 → 红绿灯 GUI
              🔴 🟡 🟢
```

四个 hook，每个项目一个 JSON 文件，FileSystemWatcher 实时驱动。不轮询、不连服务器、不依赖云。

### Hook 状态流

```
用户发消息
  → PreToolUse  → running → 🔴 红灯
  → PostToolUse → done→running → 🔴 红灯（保持！）
  → PermissionRequest → confirm → 🔴🟡 红底+黄闪
  → 用户确认 → PostToolUse → done→running → 🔴 红灯
  → Stop → idle → 3s 无新事件 → 🟢 绿灯
```

> 设计关键：PostToolUse 每次工具完成都触发，但一次回复会连续调多个工具。用 Stop 而非 PostToolUse 判断"真正完成"，避免思考间歇误亮绿灯。

## 托盘操作

| 操作 | 效果 |
|------|------|
| **左键点击** | 气泡显示运行中的项目数量与列表 |
| **右键菜单** | 切换布局、显示/隐藏全部、开机自启动、退出 |
| **双击** | 显示/隐藏全部窗口 |

## 卸载

```powershell
./publish/ClaudeLight.Clean.exe --force
```

清除 hooks 配置和状态文件，干干净净。

## 同类对比

| | Claude Light | [claude-status-bar](https://github.com/m1ckc3s/claude-status-bar) | [Claude Status](https://github.com/gmr/claude-status) |
|---|---|---|---|
| 平台 | **Windows** | macOS | macOS |
| 形式 | 桌面红绿灯窗口 | 菜单栏图标 | 菜单栏 + 桌面组件 |
| 多项目 | ✅ 独立窗口 | ✅ 聚合 | ✅ 下拉列表 |
| 权限提醒 | 🔴🟡 红底+黄闪 | 🟡 黄点 | 🟠 橙点 |
| Hook 自动安装 | ✅ | ✅ | ✅ |

---

## English

### The Problem

You're coding with Claude Code in VSCode, tab away to something else, and constantly alt-tab back to check: did it finish? Is it stuck on a permission dialog? Did it crash?

### The Solution

A tiny traffic light overlay on your screen. 🔴 Red = working. 🔴🟡 Red+Yellow = needs you. 🟢 Green = done. Zero keystrokes.

### Light States

| Light | Meaning |
|-------|---------|
| 🔴 Red (solid) | Claude is actively working (tools or thinking) |
| 🔴🟡 Red + Yellow (blink) | Claude needs your permission — open Claude Code |
| 🟢 Green (solid) | Task complete, Claude is idle |
| ⚫ Off | No Claude Code session active |

### Features

- **Real-time status** — hooks into Claude Code's lifecycle via native hooks, not polling
- **Multi-project** — one window per project, auto-arranged with offset
- **Permission alert** — distinct red+yellow blink when Claude needs approval
- **System tray** — left-click for project list, right-click for config
- **Auto-install hooks** — writes to `~/.claude/settings.json` on startup, zero manual config
- **Double-click** — toggle horizontal/vertical layout
- **Drag** — move windows anywhere on screen

### Quick Start

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
cd ClaudeLight.CSharp
dotnet publish -c Release --self-contained -r win-x64 -o publish/
./publish/ClaudeLight.exe
```

Or download the pre-built ZIP from [Releases](https://github.com/hzy12138/vibe_claude_light/releases) — unzip and run, no .NET install needed.

### How It Works

```
Claude Code fires hooks → CLI.exe writes JSON → ~/.claude-lights/
→ FileSystemWatcher monitors in real-time → GUI updates lights
```

Four hooks drive the state machine:

| Hook | When | Light |
|------|------|-------|
| `PreToolUse` | Tool starts | 🔴 Red |
| `PostToolUse` | Tool finishes (session continues) | 🔴 Red (stays) |
| `PermissionRequest` | Permission dialog appears | 🔴🟡 Red+Yellow |
| `Stop` | Claude finishes responding | ⏱ 3s → 🟢 Green |

> Key design: PostToolUse fires after every tool, but one response may call many tools. We use Stop, not PostToolUse, to detect "truly done", avoiding false green between tool calls.

### Tray

| Click | Action |
|-------|--------|
| **Left-click** | Balloon tip with running project list |
| **Right-click** | Menu: layout, visibility, auto-start, exit |
| **Double-click** | Show/hide all windows |

### Uninstall

```powershell
./publish/ClaudeLight.Clean.exe --force
```

### Comparison

| | Claude Light | [claude-status-bar](https://github.com/m1ckc3s/claude-status-bar) | [Claude Status](https://github.com/gmr/claude-status) |
|---|---|---|---|
| Platform | **Windows** | macOS | macOS |
| Visual | Traffic light window | Menu bar icon | Menu bar + widget |
| Multi-project | ✅ Per-project window | ✅ Aggregated | ✅ Dropdown |
| Permission alert | 🔴🟡 Red+yellow | 🟡 Yellow dot | 🟠 Orange dot |
| Hook auto-install | ✅ | ✅ | ✅ |

---

## License

MIT © hzy12138
