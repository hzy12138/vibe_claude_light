# Claude Code Traffic Light — Design Spec

## Context

Claude Code CLI 和 VS Code 插件在运行时没有直观的状态指示。用户需要一个屏幕上的视觉指示器，显示当前 Claude Code 实例的工作状态（运行中 / 等待确认 / 完成），支持多实例同时监控。

## Goals

- 屏幕上显示半透明红绿灯，指示 Claude Code 状态
- 支持多个 Claude Code 实例同时监控，每个灯旁显示项目名和路径
- 始终置顶，不被其他窗口覆盖
- 双击切换竖排/横排布局
- 系统托盘管理（切换布局、开机自启、退出）
- 提供 C# 版（推荐）和 Python 版（备选）
- 提供清理工具，一键移除 hooks 配置

## Non-Goals

- 不做远程监控
- 不做状态历史记录
- 不做复杂的通知系统

## Architecture

```
Claude Code (运行中)
    ↓ PreToolUse hook              → "running" (红灯)
    ↓ Notification hook            → "confirm" (黄灯闪烁)
    ↓ PostToolUse hook             → "done" (绿灯)
Hook 脚本 (claude-light hook <action> <project_dir>)
    ↓ 写入状态文件
~/.claude-lights/<project_id>.json
    ↓ FileSystemWatcher 监听
WPF / tkinter 红绿灯窗口
    ↓ 更新灯色
屏幕显示
```

### Components

#### 1. Hook CLI (`claude-light`)

由 Claude Code hooks 调用的命令行工具。

Claude Code 支持的 hook 类型：
- `PreToolUse` — 工具调用前触发
- `PostToolUse` — 工具调用后触发
- `Notification` — Claude 需要用户关注时触发（如等待确认）

命令格式：
- `claude-light hook running <project_dir>` — 写入状态 "running"（由 PreToolUse 触发）
- `claude-light hook confirm <project_dir>` — 写入状态 "confirm"（由 Notification 触发，黄灯闪烁）
- `claude-light hook done <project_dir>` — 写入状态 "done"（由 PostToolUse 触发，绿灯）
- `claude-light hook exit <project_dir>` — 删除状态文件（灯消失）

状态文件位置：`~/.claude-lights/<project_id>.json`

状态文件格式：
```json
{
  "project_name": "my-project",
  "project_dir": "E:/projects/my-project",
  "status": "running",
  "pid": 12345,
  "updated_at": "2026-05-28T22:00:00"
}
```

#### 2. Hook 自动安装 (`HookInstaller`)

程序启动时自动执行：

1. 将 `claude-light.exe` 复制到 `~/.claude-light/` 目录（固定位置）
2. 读取 `~/.claude/settings.json`
3. 检查是否已有 `claude-light` 相关 hook 配置
4. 没有则注入 hook 配置，命令使用完整路径：
   - `PreToolUse` → `"~/.claude-light/claude-light.exe" hook running $CLAUDE_PROJECT_DIR`
   - `Notification` → `"~/.claude-light/claude-light.exe" hook confirm $CLAUDE_PROJECT_DIR`
   - `PostToolUse` → `"~/.claude-light/claude-light.exe" hook done $CLAUDE_PROJECT_DIR`

不需要修改 PATH 或注册表，hook 命令里直接用完整路径即可。

#### 3. 进程自动发现 (`ProcessWatcher`)

定期扫描运行中的 Claude Code 进程，提取工作目录。

检测方式：
- 枚举所有 `claude.exe` / `claude` 进程
- 从进程命令行参数或工作目录提取项目路径
- 发现新进程时创建红绿灯窗口
- 进程退出时移除对应红绿灯

#### 4. 红绿灯窗口 (`MainWindow`)

WPF 属性：
- `WindowStyle = SingleBorderWindow`（有边框）
- `Topmost = True`（始终置顶）
- `AllowsTransparency = True`
- `Background = Transparent`
- `Opacity = 0.85`（半透明）

窗口内容：
- 3 个圆形灯（红/黄/绿），未激活时为暗灰色
- 分隔线
- 项目名称 + 路径

交互：
- 拖拽移动位置
- 双击切换竖排/横排布局

布局：
- 竖排：灯从上到下，项目名在底部
- 横排：灯从左到右，项目名在底部

#### 5. 系统托盘 (`SystemTray`)

- 托盘图标：灯泡图标（小灯泡 .ico 文件，嵌入为资源）
- 右键菜单：
  - 切换布局（竖排/横排）
  - 开机自启（开关）
  - 显示/隐藏所有窗口
  - 退出
- 双击：显示/隐藏所有红绿灯窗口
- Tooltip：显示监控的项目数量

#### 6. Hook 清理工具 (`claude-light-clean`)

用于卸载功能时清除 hooks 配置。

执行内容：
1. 移除 `~/.claude/settings.json` 中所有 claude-light 相关 hook 配置
2. 删除 `~/.claude-lights/` 目录（状态文件）
3. 删除 `~/.claude-light/` 目录（CLI 工具）

命令格式：
- `claude-light-clean` — 交互式确认后清除
- `claude-light-clean --force` — 跳过确认直接清除

同步提供 C# 版（编译为 exe）和 Python 版（直接运行 .py）。

#### 7. 状态管理 (`StateManager`)

使用 `FileSystemWatcher` 监听 `~/.claude-lights/` 目录：
- 文件创建 → 新建红绿灯窗口
- 文件修改 → 更新灯色
- 文件删除 → 移除红绿灯窗口

## State Machine

```
┌─────────┐  PreToolUse   ┌─────────┐  等待确认   ┌──────────┐  用户确认   ┌─────────┐
│  (无灯)  │ ────────────→ │  红灯   │ ──────────→ │ 黄灯闪烁  │ ──────────→ │  红灯   │
└─────────┘               └─────────┘             └──────────┘             └─────────┘
                              │                        │                       │
                              │ PostToolUse            │ PostToolUse           │ PostToolUse
                              │ (done)                 │ (done)                │ (done)
                              ↓                        ↓                       ↓
                         ┌─────────┐            ┌─────────┐              ┌─────────┐
                         │  绿灯   │            │  绿灯   │              │  绿灯   │
                         └─────────┘            └─────────┘              └─────────┘
                              │
                              │ 进程退出 / 超时
                              ↓
                         ┌─────────┐
                         │  (无灯)  │
                         └─────────┘
```

## Dual Version

### C# 版（推荐）

- 技术栈：.NET 8 + WPF
- 发布方式：`dotnet publish -c Release --self-contained -r win-x64`
- 体积：~60-80MB（含运行时）
- 依赖：无（自包含）

### Python 版（备选）

- 技术栈：Python ≥ 3.10 + tkinter
- 依赖：ctypes（内置，用于 Win32 API 调用实现点击穿透）
- 运行方式：直接 `python claude_light.py`
- 可选打包：PyInstaller 生成独立 exe

## Project Structure

```
vibe_claude_light/
├── ClaudeLight.CSharp/              # C# 版本
│   ├── ClaudeLight.sln
│   ├── ClaudeLight/                 # WPF 主程序
│   │   ├── App.xaml
│   │   ├── MainWindow.xaml          # 红绿灯窗口（有边框）
│   │   ├── TrafficLight.xaml        # 单灯组件
│   │   ├── Resources/
│   │   │   └── lightbulb.ico        # 灯泡托盘图标
│   │   ├── SystemTray.cs            # 托盘图标
│   │   ├── ProcessWatcher.cs        # 进程自动发现
│   │   ├── StateManager.cs          # 状态监听
│   │   └── HookInstaller.cs         # 启动时自动配置 hooks
│   └── ClaudeLight.CLI/             # hook 调用的 CLI
│       └── Program.cs
├── ClaudeLight.Clean.CSharp/        # C# 清理工具
│   └── Program.cs                   # claude-light-clean exe
├── ClaudeLight.Python/              # Python 版本
│   ├── claude_light.py              # 主程序（tkinter GUI）
│   ├── hook_cli.py                  # hook 调用的 CLI
│   ├── hook_installer.py            # 自动配置 hooks
│   └── claude_light_clean.py        # 清理工具
└── docs/
    └── superpowers/specs/
        └── 2026-05-28-claude-code-traffic-light-design.md
```

## Verification

1. 构建 C# 版：`dotnet publish -c Release --self-contained -r win-x64`
2. 运行 exe，验证自动注入 hooks 到 `~/.claude/settings.json`
3. 启动 Claude Code，验证红灯亮起
4. 触发确认场景，验证黄灯闪烁
5. 任务完成，验证绿灯亮起
6. 关闭 Claude Code，验证红绿灯消失
7. 双击窗口，验证布局切换
8. 测试 Python 版：`python claude_light.py`，验证相同功能
9. 测试多实例：同时运行多个 Claude Code，验证多个红绿灯独立显示
10. 运行清理工具：`claude-light-clean`，验证 hooks 配置被清除
11. 验证清理后 Claude Code 正常运行（无残留 hook 影响）
