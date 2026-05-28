# Claude Code 红绿灯

屏幕上显示红绿灯，实时指示 Claude Code 的工作状态。

## 灯色说明

| 颜色 | 状态 |
|------|------|
| 红灯（常亮） | Claude Code 正在执行任务 |
| 黄灯（闪烁） | Claude Code 等待你确认 |
| 绿灯（常亮） | 任务完成 |
| 无灯 | Claude Code 未运行 |

## 功能

- 支持多个 Claude Code 实例同时监控，每个灯旁显示项目名和路径
- 双击窗口切换竖排/横排布局
- 拖拽移动位置
- 系统托盘管理（灯泡图标）
- 启动时自动配置 hooks，无需手动操作

---

## C# 版（推荐）

### 首次使用

1. 安装 .NET 8 SDK：
   ```
   winget install Microsoft.DotNet.SDK.8
   ```

2. 在项目目录下运行：
   ```
   cd ClaudeLight.CSharp
   dotnet publish -c Release --self-contained -r win-x64 -o publish/
   ```

3. 编译完成后，双击运行：
   ```
   ClaudeLight.CSharp\publish\ClaudeLight.exe
   ```

4. 正常使用 Claude Code，红绿灯会自动出现

### 卸载

```
ClaudeLight.CSharp\publish\ClaudeLight.Clean.exe
```

---

## Python 版（备选）

适用于没有安装 .NET 的电脑。

### 首次使用

需要 Python >= 3.10（已内置 tkinter，无需额外安装依赖）

直接运行：
```
python ClaudeLight.Python\claude_light.py
```

正常使用 Claude Code，红绿灯会自动出现。

### 卸载

```
python ClaudeLight.Python\claude_light_clean.py
```

---

## 工作原理

1. 程序启动时自动将 hook 配置写入 `~/.claude/settings.json`
2. Claude Code 运行时会触发这些 hook
3. Hook 将状态信息写入 `~/.claude-lights/` 目录下的 JSON 文件
4. 红绿灯程序监听这些文件，实时更新灯色

## License

MIT
