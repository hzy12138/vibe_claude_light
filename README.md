# Claude Code Traffic Light

A screen overlay that shows traffic-light indicators for running Claude Code instances.

## Quick Start

### C# Version (Recommended)

1. Install .NET 8 SDK: `winget install Microsoft.DotNet.SDK.8`
2. Build: `dotnet publish -c Release --self-contained -r win-x64 -o publish/`
3. Run: `publish/ClaudeLight.exe`
4. Use Claude Code normally — lights appear automatically

### Python Version (Alternative)

1. Requires Python >= 3.10
2. Run: `python ClaudeLight.Python/claude_light.py`
3. Use Claude Code normally — lights appear automatically

## Lights

| Color | Meaning |
|-------|---------|
| Red (solid) | Claude Code is working |
| Yellow (blinking) | Claude Code is waiting for your confirmation |
| Green (solid) | Task completed |
| No light | Claude Code is not running |

## Features

- Multiple instances supported — each project gets its own light
- Double-click to toggle vertical/horizontal layout
- Drag to reposition
- System tray icon (lightbulb) for management
- Auto-configures hooks on startup

## Uninstall

### C# Version
```bash
publish/ClaudeLight.Clean.exe
```

### Python Version
```bash
python ClaudeLight.Python/claude_light_clean.py
```

## License

MIT
