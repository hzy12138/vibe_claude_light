# Claude Code 红绿灯 / Claude Code Traffic Light 🚦

[English](#english) | [中文](#中文)

屏幕上显示红绿灯，实时指示 Claude Code 的工作状态。

A desktop traffic light indicator that shows Claude Code's real-time status on your screen.

---

## English

### Light States

| Color | Meaning |
|-------|---------|
| 🔴 Red (solid) | Claude Code is working |
| 🟡 Yellow (blinking on red) | Claude Code needs your permission |
| 🟢 Green (solid) | Task completed |
| ⚫ Off | Claude Code is not running |

### Features

- Monitor multiple Claude Code sessions simultaneously, each with project name and path
- Double-click window to toggle horizontal/vertical layout
- Drag to reposition
- System tray with context menu (toggle layout, show/hide, auto-start, exit)
- **Left-click tray icon** to see running project list
- Auto-installs hooks on startup — no manual config needed
- Multiple windows auto-offset to avoid overlap

### How It Works

```
Claude Code hooks → CLI.exe → ~/.claude-lights/*.json → FileSystemWatcher → GUI
```

The app registers four Claude Code hooks on startup:
- `PreToolUse` → 🔴 Red (Claude starts working)
- `PostToolUse` → 🔴 Red (tool finished, session continues)
- `PermissionRequest` → 🟡 Yellow blink on red (needs your attention)
- `Stop` → ⏱️ 3s → 🟢 Green (Claude finished responding)

### Quick Start

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
# Build
cd ClaudeLight.CSharp
dotnet publish -c Release --self-contained -r win-x64 -o publish/

# Run
./publish/ClaudeLight.exe

# Uninstall
./publish/ClaudeLight.Clean.exe --force
```

### Tray Menu

| Item | Action |
|------|--------|
| Toggle Layout | Switch all windows between horizontal/vertical |
| Show/Hide All | Show or hide all traffic light windows |
| Auto-Start | Enable/disable launch on Windows startup |
| Exit | Quit the application |

### Debugging

Check the log file: `publish/claude-light.log`

---

## 中文

### 灯色说明

| 颜色 | 状态 |
|------|------|
| 🔴 红灯常亮 | Claude Code 正在执行 |
| 🟡 黄灯闪烁（红底） | Claude Code 需要你确认 |
| 🟢 绿灯常亮 | 任务完成 |
| ⚫ 无灯 | Claude Code 未运行 |

### 功能

- 支持多个 Claude Code 实例同时监控，每个灯旁显示项目名和路径
- 双击窗口切换竖排/横排布局
- 拖拽移动位置
- 系统托盘管理（右键菜单：切换布局、显示/隐藏、开机自启动、退出）
- **左键点击托盘图标**查看运行中的项目列表
- 启动时自动配置 hooks，无需手动操作
- 多个项目窗口自动偏移显示，避免重叠

### 工作原理

```
Claude Code hooks → CLI.exe → ~/.claude-lights/*.json → FileSystemWatcher → GUI
```

启动时自动注册四个 Claude Code hook：
- `PreToolUse` → 🔴 红灯（Claude 开始工作）
- `PostToolUse` → 🔴 红灯（工具完成，会话继续）
- `PermissionRequest` → 🟡 红底+黄闪（需要确认）
- `Stop` → ⏱️ 3 秒 → 🟢 绿灯（会话完成）

### 快速开始

**环境要求:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
# 编译
cd ClaudeLight.CSharp
dotnet publish -c Release --self-contained -r win-x64 -o publish/

# 运行
./publish/ClaudeLight.exe

# 卸载
./publish/ClaudeLight.Clean.exe --force
```

详细操作指南见 [docs/操作指南.md](docs/操作指南.md)。

### 托盘菜单

| 菜单项 | 功能 |
|--------|------|
| 切换布局 | 切换所有窗口的横排/竖排 |
| 显示/隐藏全部 | 一键显示或隐藏所有红绿灯窗口 |
| 开机自启动 | 勾选后开机自动启动 |
| 退出 | 完全退出程序 |

### 调试

查看日志文件：`publish/claude-light.log`

---

## License

MIT
