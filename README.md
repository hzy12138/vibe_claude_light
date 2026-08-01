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
- 系统托盘管理（红绿灯图标）
- 启动时自动配置 hooks，无需手动操作
- 多个项目窗口自动偏移显示，避免重叠

## 安装和使用

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

4. 在 VSCode 中正常使用 Claude Code，红绿灯会在屏幕右侧自动出现

### 使用说明

- **窗口位置**：红绿灯默认出现在屏幕右侧，可拖拽到任意位置
- **多项目**：同时监控多个 Claude Code 实例时，窗口会自动偏移显示
- **切换布局**：双击窗口可切换竖排/横排布局
- **最小化**：窗口关闭后仍在系统托盘运行，双击托盘图标可重新显示

### 卸载

```
ClaudeLight.CSharp\publish\ClaudeLight.Clean.exe
```

## 工作原理

1. 程序启动时自动将 hook 配置写入 `~/.claude/settings.json`
2. Claude Code 运行时会触发这些 hook
3. Hook 将状态信息写入 `~/.claude-lights/` 目录下的 JSON 文件
4. 红绿灯程序轮询监控这些文件，实时更新灯色

## License

MIT
