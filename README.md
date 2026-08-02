# Claude Code 红绿灯 🚦

> Windows 桌面红绿灯指示器 — 瞄一眼就知道 Claude Code 是在工作、等你确认、还是已完成。

A desktop traffic light for Claude Code on Windows. [English](#english)

![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue) ![license](https://img.shields.io/badge/license-MIT-green) ![dotnet](https://img.shields.io/badge/.NET-8.0-purple)

---

## 这是什么

用 Claude Code 写代码时切屏干别的，总得 alt-tab 回去看它跑完没、是不是卡权限了。这个小工具在屏幕角落放一个红绿灯，瞄一眼就知道状态。

## 灯色说明

| 灯色 | 含义 |
|------|------|
| 🔴 红灯常亮 | Claude Code 正在执行 |
| 🔴🟡 红底+黄灯闪烁 | Claude Code 需要你确认权限 |
| 🟢 绿灯常亮 | 任务完成 |
| ⚫ 全灭 | Claude Code 未运行 |

## 功能

- **实时状态** — 通过 Claude Code 原生 hooks 获取状态
- **多项目支持** — 每个项目独立窗口，自动偏移排列
- **权限提醒** — 需要确认时黄灯闪烁（红底），醒目但不碍眼
- **系统托盘** — 常驻托盘，左键查看运行项目，右键配置
- **自动安装 hooks** — 双击启动，hooks 自动写入 `~/.claude/settings.json`
- **开机自启动** — 右键勾选即可
- **双击切换布局** — 横排/竖排随意切换

## 快速开始

**环境要求:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```powershell
# 编译
cd ClaudeLight.CSharp
dotnet publish -c Release --self-contained -r win-x64 -o publish/

# 运行
./publish/ClaudeLight.exe
```

或者下载预编译的 ZIP：[Releases](https://github.com/hzy12138/vibe_claude_light/releases)

## 工作原理

```
Claude Code 触发 hooks → CLI.exe 写 JSON → ~/.claude-lights/
→ FileSystemWatcher 实时监控 → 红绿灯 GUI 更新灯色
```

四个 hook 各司其职：

| Hook | 触发时机 | 灯色 |
|------|----------|------|
| `PreToolUse` | 工具开始执行 | 🔴 红灯 |
| `PostToolUse` | 工具执行完成 | 🔴 红灯（保持） |
| `PermissionRequest` | 弹出权限对话框 | 🔴🟡 红底+黄闪 |
| `Stop` | Claude 完成回复 | ⏱ 3s → 🟢 绿灯 |

## 托盘说明

| 操作 | 效果 |
|------|------|
| **左键点击** | 气泡显示运行中的项目列表 |
| **右键菜单** | 切换布局 / 显示隐藏 / 开机自启动 / 退出 |
| **双击** | 显示/隐藏全部窗口 |

## 卸载

```powershell
./publish/ClaudeLight.Clean.exe --force
```

## 同类对比

| | Claude Light | [claude-status-bar](https://github.com/m1ckc3s/claude-status-bar) | [Claude Status](https://github.com/gmr/claude-status) |
|---|---|---|---|
| 平台 | **Windows** | macOS | macOS |
| 形式 | 桌面红绿灯窗口 | 菜单栏图标 | 菜单栏 + 桌面组件 |
| 多项目 | ✅ 独立窗口 | ✅ 聚合显示 | ✅ 下拉列表 |
| 权限提醒 | 🟡 红底+黄闪 | 🟡 黄点 | 🟠 橙点 |
| 自动安装 hooks | ✅ | ✅ | ✅ |

---

## English

A Windows desktop traffic light indicator for Claude Code. When you're coding with Claude Code and tab away, glance at the light to see if Claude is working, needs your permission, or is done.

### Light States

| Light | Meaning |
|-------|---------|
| 🔴 Red (solid) | Claude is working |
| 🔴🟡 Red + Yellow (blink) | Claude needs your permission |
| 🟢 Green (solid) | Task complete |
| ⚫ Off | No Claude Code session |

### Quick Start

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Build, run `ClaudeLight.exe`, done. Or grab the ZIP from [Releases](https://github.com/hzy12138/vibe_claude_light/releases).

Full docs (Chinese): [docs/操作指南.md](docs/操作指南.md)

---

## License

MIT © hzy12138
