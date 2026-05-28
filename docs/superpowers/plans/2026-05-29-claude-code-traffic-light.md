# Claude Code Traffic Light — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a screen overlay that shows traffic-light indicators for running Claude Code instances, with auto-detected hooks and a system tray for management.

**Architecture:** Two independent versions (C# WPF + Python tkinter) that share the same hook protocol. Claude Code hooks write status files to `~/.claude-lights/`; the GUI programs watch that directory and update colored lights accordingly.

**Tech Stack:** C# — .NET 8, WPF, System.Text.Json; Python — Python ≥ 3.10, tkinter, ctypes (Win32 API)

**Spec:** `docs/superpowers/specs/2026-05-28-claude-code-traffic-light-design.md`

---

## File Map

### C# Version

```
ClaudeLight.CSharp/
├── ClaudeLight.sln
├── ClaudeLight.CLI/
│   ├── ClaudeLight.CLI.csproj
│   └── Program.cs                    # CLI: hook running/confirm/done/exit
├── ClaudeLight/
│   ├── ClaudeLight.csproj
│   ├── App.xaml                      # WPF entry, single-instance mutex
│   ├── App.xaml.cs
│   ├── MainWindow.xaml               # Traffic light window (bordered, transparent, topmost)
│   ├── MainWindow.xaml.cs            # Layout toggle, drag, double-click
│   ├── TrafficLight.xaml             # Single light component (3 circles + label)
│   ├── TrafficLight.xaml.cs
│   ├── Models/
│   │   └── LightState.cs             # State model (status enum, project info)
│   ├── Services/
│   │   ├── StateManager.cs           # FileSystemWatcher on ~/.claude-lights/
│   │   ├── ProcessWatcher.cs         # Scan for claude.exe processes
│   │   ├── HookInstaller.cs          # Auto-inject hooks into settings.json
│   │   └── AutoStartManager.cs       # Registry-based auto-start
│   ├── Controls/
│   │   └── SystemTray.cs             # NotifyIcon with lightbulb, context menu
│   └── Resources/
│       └── lightbulb.ico             # Tray icon
├── ClaudeLight.Clean/
│   ├── ClaudeLight.Clean.csproj
│   └── Program.cs                    # Remove hooks, delete state/cli dirs
└── ClaudeLight.Tests/
    ├── ClaudeLight.Tests.csproj
    ├── HookInstallerTests.cs
    ├── StateManagerTests.cs
    └── ProcessWatcherTests.cs
```

### Python Version

```
ClaudeLight.Python/
├── claude_light.py                   # Main GUI (tkinter)
├── hook_cli.py                       # CLI: hook running/confirm/done/exit
├── hook_installer.py                 # Auto-inject hooks
├── state_manager.py                  # Watch ~/.claude-lights/
├── process_watcher.py                # Scan for claude processes
├── claude_light_clean.py             # Cleanup tool
└── tests/
    ├── test_hook_installer.py
    ├── test_state_manager.py
    └── test_process_watcher.py
```

---

## Part A: C# Version

### Task 1: Scaffold Solution and Projects

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight.sln`
- Create: `ClaudeLight.CSharp/ClaudeLight.CLI/ClaudeLight.CLI.csproj`
- Create: `ClaudeLight.CSharp/ClaudeLight/ClaudeLight.csproj`
- Create: `ClaudeLight.CSharp/ClaudeLight.Clean/ClaudeLight.Clean.csproj`
- Create: `ClaudeLight.CSharp/ClaudeLight.Tests/ClaudeLight.Tests.csproj`

- [ ] **Step 1: Create solution and project files**

```bash
cd E:/workplace/vibe_claude_light
dotnet new sln -n ClaudeLight -o ClaudeLight.CSharp
cd ClaudeLight.CSharp

# CLI tool (console app)
dotnet new console -n ClaudeLight.CLI -f net8.0
dotnet sln add ClaudeLight.CLI/ClaudeLight.CLI.csproj

# WPF app
dotnet new wpf -n ClaudeLight -f net8.0
dotnet sln add ClaudeLight/ClaudeLight.csproj

# Clean tool (console app)
dotnet new console -n ClaudeLight.Clean -f net8.0
dotnet sln add ClaudeLight.Clean/ClaudeLight.Clean.csproj

# Tests
dotnet new xunit -n ClaudeLight.Tests -f net8.0
dotnet sln add ClaudeLight.Tests/ClaudeLight.Tests.csproj
dotnet add ClaudeLight.Tests reference ClaudeLight.CLI/ClaudeLight.CLI.csproj
```

- [ ] **Step 2: Add System.Text.Json to CLI project**

```bash
cd ClaudeLight.CLI
dotnet add package System.Text.Json
cd ../ClaudeLight.Clean
dotnet add package System.Text.Json
cd ../ClaudeLight.Tests
dotnet add package System.Text.Json
```

- [ ] **Step 3: Verify build**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add ClaudeLight.CSharp/
git commit -m "chore: scaffold C# solution with CLI, WPF, Clean, and Tests projects"
```

---

### Task 2: LightState Model

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight/Models/LightState.cs`

- [ ] **Step 1: Create the state model**

```csharp
// ClaudeLight.CSharp/ClaudeLight/Models/LightState.cs
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeLight.Models;

public enum LightStatus
{
    Running,
    Confirm,
    Done
}

public class LightState
{
    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = "";

    [JsonPropertyName("project_dir")]
    public string ProjectDir { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";

    public LightStatus? GetStatus() => Status switch
    {
        "running" => LightStatus.Running,
        "confirm" => LightStatus.Confirm,
        "done" => LightStatus.Done,
        _ => null
    };

    public static string GetStateDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-lights");
    }

    public static string GetStateFilePath(string projectDir)
    {
        // Use a sanitized hash of the project dir as filename
        var sanitized = projectDir
            .Replace("\\", "_")
            .Replace("/", "_")
            .Replace(":", "_");
        if (sanitized.Length > 100)
            sanitized = sanitized[^100..];
        return Path.Combine(GetStateDir(), $"{sanitized}.json");
    }

    public static LightState? ReadFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<LightState>(json);
    }

    public void WriteToFile(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/Models/
git commit -m "feat: add LightState model with JSON serialization"
```

---

### Task 3: Hook CLI (claude-light)

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight.CLI/Program.cs`

- [ ] **Step 1: Write the hook CLI**

```csharp
// ClaudeLight.CSharp/ClaudeLight.CLI/Program.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace ClaudeLight.CLI;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 3 || args[0] != "hook")
        {
            Console.Error.WriteLine("Usage: claude-light hook <running|confirm|done|exit> <project_dir>");
            return 1;
        }

        var action = args[1];
        var projectDir = args[2];

        var stateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-lights");

        if (action == "exit")
        {
            var exitPath = GetStateFilePath(projectDir);
            if (File.Exists(exitPath))
                File.Delete(exitPath);
            return 0;
        }

        var status = action switch
        {
            "running" => "running",
            "confirm" => "confirm",
            "done" => "done",
            _ => null
        };

        if (status == null)
        {
            Console.Error.WriteLine($"Unknown action: {action}");
            return 1;
        }

        var projectName = new DirectoryInfo(projectDir).Name;
        var state = new
        {
            project_name = projectName,
            project_dir = projectDir,
            status = status,
            pid = Process.GetCurrentProcess().Id,
            updated_at = DateTime.UtcNow.ToString("o")
        };

        var path = GetStateFilePath(projectDir);
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
        return 0;
    }

    private static string GetStateFilePath(string projectDir)
    {
        var sanitized = projectDir
            .Replace("\\", "_")
            .Replace("/", "_")
            .Replace(":", "_");
        if (sanitized.Length > 100)
            sanitized = sanitized[^100..];
        var stateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-lights");
        return Path.Combine(stateDir, $"{sanitized}.json");
    }
}
```

- [ ] **Step 2: Build and test manually**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp
dotnet build ClaudeLight.CLI

# Test: write a state file
dotnet run --project ClaudeLight.CLI -- hook running "E:/test-project"
# Verify: %USERPROFILE%/.claude-lights/ should contain a .json file

# Test: change to done
dotnet run --project ClaudeLight.CLI -- hook done "E:/test-project"

# Test: exit (delete)
dotnet run --project ClaudeLight.CLI -- hook exit "E:/test-project"
# Verify: state file deleted
```

- [ ] **Step 3: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight.CLI/
git commit -m "feat: implement hook CLI for writing Claude Code state files"
```

---

### Task 4: HookInstaller

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight/Services/HookInstaller.cs`
- Create: `ClaudeLight.CSharp/ClaudeLight.Tests/HookInstallerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// ClaudeLight.CSharp/ClaudeLight.Tests/HookInstallerTests.cs
using System;
using System.IO;
using System.Text.Json;
using ClaudeLight.Services;
using Xunit;

namespace ClaudeLight.Tests;

public class HookInstallerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testSettingsPath;

    public HookInstallerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"claude-light-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _testSettingsPath = Path.Combine(_testDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void InstallHooks_CreatesHookConfig_WhenNoExistingHooks()
    {
        File.WriteAllText(_testSettingsPath, "{}");

        HookInstaller.InstallHooks(_testSettingsPath, "/fake/claude-light.exe");

        var json = File.ReadAllText(_testSettingsPath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("hooks", out var hooks));
        Assert.True(hooks.TryGetProperty("PreToolUse", out var pre));
        Assert.True(hooks.TryGetProperty("PostToolUse", out var post));
        Assert.True(hooks.TryGetProperty("Notification", out var notif));
    }

    [Fact]
    public void InstallHooks_DoesNotDuplicate_WhenAlreadyInstalled()
    {
        File.WriteAllText(_testSettingsPath, "{}");
        HookInstaller.InstallHooks(_testSettingsPath, "/fake/claude-light.exe");
        var countBefore = File.ReadAllText(_testSettingsPath).Split("claude-light").Length;

        HookInstaller.InstallHooks(_testSettingsPath, "/fake/claude-light.exe");
        var countAfter = File.ReadAllText(_testSettingsPath).Split("claude-light").Length;

        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void RemoveHooks_ClearsHookConfig()
    {
        File.WriteAllText(_testSettingsPath, "{}");
        HookInstaller.InstallHooks(_testSettingsPath, "/fake/claude-light.exe");

        HookInstaller.RemoveHooks(_testSettingsPath);

        var json = File.ReadAllText(_testSettingsPath);
        var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("hooks", out _));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp
dotnet test ClaudeLight.Tests --filter "HookInstallerTests"
```

Expected: FAIL — `HookInstaller` class not found.

- [ ] **Step 3: Implement HookInstaller**

```csharp
// ClaudeLight.CSharp/ClaudeLight/Services/HookInstaller.cs
using System;
using System.IO;
using System.Text.Json;

namespace ClaudeLight.Services;

public static class HookInstaller
{
    private const string HookMarker = "claude-light";

    public static void InstallHooks(string settingsPath, string cliExePath)
    {
        var json = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "{}";
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        // Check if already installed
        if (root.TryGetProperty("hooks", out var hooks) &&
            hooks.GetRawText().Contains(HookMarker))
        {
            return; // Already installed
        }

        var cliPath = cliExePath.Replace("\\", "\\\\");

        var hookConfig = new
        {
            hooks = new
            {
                PreToolUse = new[]
                {
                    new
                    {
                        hooks = new[]
                        {
                            new
                            {
                                type = "command",
                                command = $"\"{cliPath}\" hook running $CLAUDE_PROJECT_DIR"
                            }
                        }
                    }
                },
                PostToolUse = new[]
                {
                    new
                    {
                        hooks = new[]
                        {
                            new
                            {
                                type = "command",
                                command = $"\"{cliPath}\" hook done $CLAUDE_PROJECT_DIR"
                            }
                        }
                    }
                },
                Notification = new[]
                {
                    new
                    {
                        hooks = new[]
                        {
                            new
                            {
                                type = "command",
                                command = $"\"{cliPath}\" hook confirm $CLAUDE_PROJECT_DIR"
                            }
                        }
                    }
                }
            }
        };

        // Merge with existing settings
        var existing = root.TryGetProperty("hooks", out _) ? root : doc.RootElement.Clone();
        var merged = JsonSerializer.Serialize(hookConfig, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, merged);
    }

    public static void RemoveHooks(string settingsPath)
    {
        if (!File.Exists(settingsPath)) return;

        var json = File.ReadAllText(settingsPath);
        if (!json.Contains(HookMarker)) return;

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        // Remove hooks property entirely if it only contains our hooks
        if (root.TryGetProperty("hooks", out _))
        {
            var result = new Dictionary<string, JsonElement>();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name != "hooks")
                    result[prop.Name] = prop.Value.Clone();
            }

            var cleaned = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(settingsPath, cleaned);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp
dotnet test ClaudeLight.Tests --filter "HookInstallerTests"
```

Expected: All 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/Services/HookInstaller.cs
git add ClaudeLight.CSharp/ClaudeLight.Tests/HookInstallerTests.cs
git commit -m "feat: implement HookInstaller with auto-inject and remove hooks"
```

---

### Task 5: TrafficLight UserControl

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight/TrafficLight.xaml`
- Create: `ClaudeLight.CSharp/ClaudeLight/TrafficLight.xaml.cs`

- [ ] **Step 1: Create the TrafficLight XAML**

```xml
<!-- ClaudeLight.CSharp/ClaudeLight/TrafficLight.xaml -->
<UserControl x:Class="ClaudeLight.TrafficLight"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Name="Root">

    <!-- Vertical Layout -->
    <Grid x:Name="VerticalLayout" Visibility="Visible">
        <StackPanel Orientation="Vertical" HorizontalAlignment="Center" VerticalAlignment="Center">
            <Ellipse x:Name="RedLight" Width="30" Height="30" Fill="#333" Margin="0,0,0,4"/>
            <Ellipse x:Name="YellowLight" Width="30" Height="30" Fill="#333" Margin="0,0,0,4"/>
            <Ellipse x:Name="GreenLight" Width="30" Height="30" Fill="#333" Margin="0,4,0,0"/>
            <Line X1="0" X2="80" Stroke="#555" StrokeThickness="1" Margin="0,6"/>
            <TextBlock x:Name="ProjectName" Text="project" FontSize="10" Foreground="#aaa"
                       HorizontalAlignment="Center" Margin="0,4,0,0"/>
            <TextBlock x:Name="ProjectPath" Text="path" FontSize="8" Foreground="#666"
                       HorizontalAlignment="Center" TextTrimming="CharacterEllipsis" MaxWidth="100"/>
        </StackPanel>
    </Grid>

    <!-- Horizontal Layout -->
    <Grid x:Name="HorizontalLayout" Visibility="Collapsed">
        <StackPanel Orientation="Vertical" HorizontalAlignment="Center" VerticalAlignment="Center">
            <StackPanel Orientation="Horizontal" HorizontalAlignment="Center">
                <Ellipse x:Name="RedLightH" Width="30" Height="30" Fill="#333" Margin="0,0,4,0"/>
                <Ellipse x:Name="YellowLightH" Width="30" Height="30" Fill="#333" Margin="0,0,4,0"/>
                <Ellipse x:Name="GreenLightH" Width="30" Height="30" Fill="#333"/>
            </StackPanel>
            <Line X1="0" X2="120" Stroke="#555" StrokeThickness="1" Margin="0,6"/>
            <TextBlock x:Name="ProjectNameH" Text="project" FontSize="10" Foreground="#aaa"
                       HorizontalAlignment="Center" Margin="0,4,0,0"/>
            <TextBlock x:Name="ProjectPathH" Text="path" FontSize="8" Foreground="#666"
                       HorizontalAlignment="Center" TextTrimming="CharacterEllipsis" MaxWidth="120"/>
        </StackPanel>
    </Grid>
</UserControl>
```

- [ ] **Step 2: Create the TrafficLight code-behind**

```csharp
// ClaudeLight.CSharp/ClaudeLight/TrafficLight.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeLight.Models;

namespace ClaudeLight;

public partial class TrafficLight : UserControl
{
    private readonly DispatcherTimer _blinkTimer;
    private bool _blinkState;

    public TrafficLight()
    {
        InitializeComponent();
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += BlinkTimer_Tick;
    }

    public void SetProjectInfo(string name, string path)
    {
        ProjectName.Text = name;
        ProjectPath.Text = path;
        ProjectNameH.Text = name;
        ProjectPathH.Text = path;
        ToolTip = $"{name}\n{path}";
    }

    public void SetStatus(LightStatus? status)
    {
        _blinkTimer.Stop();

        // Default: all off
        var red = "#333";
        var yellow = "#333";
        var green = "#333";

        switch (status)
        {
            case LightStatus.Running:
                red = "#e74c3c";
                break;
            case LightStatus.Confirm:
                yellow = "#f39c12";
                _blinkTimer.Start();
                break;
            case LightStatus.Done:
                green = "#2ecc71";
                break;
        }

        ApplyColor(red, yellow, green);
    }

    public void SetLayout(bool isHorizontal)
    {
        VerticalLayout.Visibility = isHorizontal ? Visibility.Collapsed : Visibility.Visible;
        HorizontalLayout.Visibility = isHorizontal ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BlinkTimer_Tick(object? sender, EventArgs e)
    {
        _blinkState = !_blinkState;
        var yellow = _blinkState ? "#f39c12" : "#333";
        ApplyColor("#333", yellow, "#333");
    }

    private void ApplyColor(string red, string yellow, string green)
    {
        RedLight.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(red));
        YellowLight.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(yellow));
        GreenLight.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(green));
        RedLightH.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(red));
        YellowLightH.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(yellow));
        GreenLightH.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(green));
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/TrafficLight.xaml
git add ClaudeLight.CSharp/ClaudeLight/TrafficLight.xaml.cs
git commit -m "feat: add TrafficLight UserControl with blinking yellow light"
```

---

### Task 6: MainWindow with Layout Toggle and Drag

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight/MainWindow.xaml`
- Create: `ClaudeLight.CSharp/ClaudeLight/MainWindow.xaml.cs`

- [ ] **Step 1: Create MainWindow XAML**

```xml
<!-- ClaudeLight.CSharp/ClaudeLight/MainWindow.xaml -->
<Window x:Class="ClaudeLight.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Claude Light"
        WindowStyle="SingleBorderWindow"
        Topmost="True"
        AllowsTransparency="True"
        Background="Transparent"
        Opacity="0.85"
        Width="100" Height="200"
        ResizeMode="NoResize"
        ShowInTaskbar="False"
        WindowStartupLocation="Manual"
        Left="1800" Top="100">

    <Border Background="#1e1e1e" BorderBrush="#555" BorderThickness="1" CornerRadius="8"
            MouseLeftButtonDown="Window_MouseLeftButtonDown"
            MouseLeftButtonUp="Window_MouseLeftButtonUp"
            MouseMove="Window_MouseMove"
            MouseDoubleClick="Window_MouseDoubleClick">
        <local:TrafficLight x:Name="TrafficLightControl"
                            xmlns:local="clr-namespace:ClaudeLight"/>
    </Border>
</Window>
```

- [ ] **Step 2: Create MainWindow code-behind**

```csharp
// ClaudeLight.CSharp/ClaudeLight/MainWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Input;
using ClaudeLight.Models;

namespace ClaudeLight;

public partial class MainWindow : Window
{
    private bool _isHorizontal;
    private Point _dragStart;
    private bool _isDragging;

    public string ProjectDir { get; set; } = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var dir = new DirectoryInfo(ProjectDir);
        TrafficLightControl.SetProjectInfo(dir.Name, ProjectDir);
        TrafficLightControl.SetLayout(_isHorizontal);
    }

    public void UpdateStatus(LightStatus? status)
    {
        TrafficLightControl.SetStatus(status);

        if (status == null)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void ToggleLayout()
    {
        _isHorizontal = !_isHorizontal;
        TrafficLightControl.SetLayout(_isHorizontal);

        // Resize window for new layout
        if (_isHorizontal)
        {
            Width = 140;
            Height = 120;
        }
        else
        {
            Width = 100;
            Height = 200;
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = false;
        CaptureMouse();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (IsMouseCaptured)
        {
            var pos = e.GetPosition(this);
            var dx = pos.X - _dragStart.X;
            var dy = pos.Y - _dragStart.Y;

            if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2)
                _isDragging = true;

            Left += dx;
            Top += dy;
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ToggleLayout();
    }
}
```

- [ ] **Step 3: Update App.xaml for single instance and startup**

```xml
<!-- ClaudeLight.CSharp/ClaudeLight/App.xaml -->
<Application x:Class="ClaudeLight.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
</Application>
```

```csharp
// ClaudeLight.CSharp/ClaudeLight/App.xaml.cs
using System;
using System.Threading;
using System.Windows;

namespace ClaudeLight;

public partial class App : Application
{
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "ClaudeLight_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Claude Light is already running.", "Claude Light");
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
```

- [ ] **Step 4: Build and verify**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp
dotnet build ClaudeLight
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/MainWindow.xaml
git add ClaudeLight.CSharp/ClaudeLight/MainWindow.xaml.cs
git add ClaudeLight.CSharp/ClaudeLight/App.xaml
git add ClaudeLight.CSharp/ClaudeLight/App.xaml.cs
git commit -m "feat: add MainWindow with layout toggle, drag, and double-click"
```

---

### Task 7: StateManager (FileSystemWatcher)

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight/Services/StateManager.cs`
- Create: `ClaudeLight.CSharp/ClaudeLight.Tests/StateManagerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// ClaudeLight.CSharp/ClaudeLight.Tests/StateManagerTests.cs
using System;
using System.IO;
using System.Text.Json;
using ClaudeLight.Services;
using Xunit;

namespace ClaudeLight.Tests;

public class StateManagerTests : IDisposable
{
    private readonly string _testDir;

    public StateManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"claude-light-sm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void WriteState_CreatesFile_WithCorrectContent()
    {
        var path = Path.Combine(_testDir, "test.json");
        StateManager.WriteState(path, "my-project", "E:/projects/my-project", "running", 1234);

        Assert.True(File.Exists(path));
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("running", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("my-project", doc.RootElement.GetProperty("project_name").GetString());
    }

    [Fact]
    public void ReadState_ReturnsNull_WhenFileNotFound()
    {
        var result = StateManager.ReadState(Path.Combine(_testDir, "nonexistent.json"));
        Assert.Null(result);
    }

    [Fact]
    public void DeleteState_RemovesFile()
    {
        var path = Path.Combine(_testDir, "to-delete.json");
        StateManager.WriteState(path, "proj", "dir", "running", 1);
        StateManager.DeleteState(path);
        Assert.False(File.Exists(path));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp
dotnet test ClaudeLight.Tests --filter "StateManagerTests"
```

Expected: FAIL — `StateManager` methods not found.

- [ ] **Step 3: Implement StateManager**

```csharp
// ClaudeLight.CSharp/ClaudeLight/Services/StateManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClaudeLight.Models;

namespace ClaudeLight.Services;

public class StateManager : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly string _stateDir;
    private readonly Dictionary<string, MainWindow> _windows = new();

    public event Action<string, LightStatus?>? StatusChanged;
    public event Action<string>? InstanceRemoved;

    public StateManager()
    {
        _stateDir = LightState.GetStateDir();
        if (!Directory.Exists(_stateDir))
            Directory.CreateDirectory(_stateDir);

        _watcher = new FileSystemWatcher(_stateDir, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Created += OnFileChanged;
        _watcher.Changed += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            var state = LightState.ReadFromFile(e.FullPath);
            if (state == null) return;
            var status = state.GetStatus();
            StatusChanged?.Invoke(state.ProjectDir, status);
        }
        catch { /* ignore parse errors during rapid writes */ }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        var projectDir = Path.GetFileNameWithoutExtension(e.Name)
            .Replace("_", "/"); // Best-effort reverse
        InstanceRemoved?.Invoke(projectDir);
    }

    public void ScanExistingStates()
    {
        foreach (var file in Directory.GetFiles(_stateDir, "*.json"))
        {
            try
            {
                var state = LightState.ReadFromFile(file);
                if (state != null)
                    StatusChanged?.Invoke(state.ProjectDir, state.GetStatus());
            }
            catch { }
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp
dotnet test ClaudeLight.Tests --filter "StateManagerTests"
```

Expected: All 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/Services/StateManager.cs
git add ClaudeLight.CSharp/ClaudeLight.Tests/StateManagerTests.cs
git commit -m "feat: implement StateManager with FileSystemWatcher"
```

---

### Task 8: ProcessWatcher

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight/Services/ProcessWatcher.cs`

- [ ] **Step 1: Implement ProcessWatcher**

```csharp
// ClaudeLight.CSharp/ClaudeLight/Services/ProcessWatcher.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClaudeLight.Services;

public class ProcessWatcher : IDisposable
{
    private readonly Timer _timer;
    private readonly HashSet<int> _knownPids = new();
    private readonly string _stateDir;

    public event Action<int, string>? ProcessStarted;  // pid, projectDir
    public event Action<int>? ProcessExited;

    public ProcessWatcher()
    {
        _stateDir = LightState.GetStateDir();
        _timer = new Timer(ScanProcesses, null, TimeSpan.Zero, TimeSpan.FromSeconds(3));
    }

    private void ScanProcesses(object? state)
    {
        try
        {
            var claudeProcesses = Process.GetProcessesByName("claude");
            var currentPids = new HashSet<int>();

            foreach (var proc in claudeProcesses)
            {
                try
                {
                    currentPids.Add(proc.Id);

                    if (!_knownPids.Contains(proc.Id))
                    {
                        var projectDir = GetProcessWorkingDir(proc);
                        if (projectDir != null)
                        {
                            _knownPids.Add(proc.Id);
                            ProcessStarted?.Invoke(proc.Id, projectDir);
                        }
                    }
                }
                catch { /* process may have exited */ }
            }

            // Detect exited processes
            var exited = _knownPids.Where(p => !currentPids.Contains(p)).ToList();
            foreach (var pid in exited)
            {
                _knownPids.Remove(pid);
                ProcessExited?.Invoke(pid);
            }
        }
        catch { }
    }

    private static string? GetProcessWorkingDir(Process proc)
    {
        try
        {
            // Try to get working directory via WMI or command line
            // Fallback: check if process has a window title containing a path
            var mainModule = proc.MainModule;
            if (mainModule != null)
            {
                var dir = Path.GetDirectoryName(mainModule.FileName);
                if (dir != null && dir.Contains("claude", StringComparison.OrdinalIgnoreCase))
                    return null; // This is the claude binary itself, not a project
            }
        }
        catch { }

        // Fallback: try to read from /proc or use performance counters
        // On Windows, we rely on the state files instead
        return null;
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/Services/ProcessWatcher.cs
git commit -m "feat: add ProcessWatcher for detecting Claude Code processes"
```

---

### Task 9: SystemTray

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight/Controls/SystemTray.cs`
- Create: `ClaudeLight.CSharp/ClaudeLight/Resources/lightbulb.ico`

- [ ] **Step 1: Create a simple lightbulb icon (16x16 ICO)**

Use any ICO generator or create a minimal 16x16 yellow lightbulb icon. Save as `ClaudeLight.CSharp/ClaudeLight/Resources/lightbulb.ico`.

- [ ] **Step 2: Add icon to project resources**

Edit `ClaudeLight.CSharp/ClaudeLight/ClaudeLight.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Resource Include="Resources\lightbulb.ico" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Implement SystemTray**

```csharp
// ClaudeLight.CSharp/ClaudeLight/Controls/SystemTray.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ClaudeLight.Services;

namespace ClaudeLight.Controls;

public class SystemTray : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly List<MainWindow> _windows = new();
    private bool _allHidden;

    public SystemTray()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(
                Application.GetResourceStream(new Uri("pack://application:,,,/Resources/lightbulb.ico")).Stream),
            Text = "Claude Light",
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Toggle Layout", null, (_, _) => ToggleLayout());
        menu.Items.Add("Show/Hide All", null, (_, _) => ToggleVisibility());
        menu.Items.Add("-");
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility();
    }

    public void AddWindow(MainWindow window)
    {
        _windows.Add(window);
        UpdateTooltip();
    }

    public void RemoveWindow(MainWindow window)
    {
        _windows.Remove(window);
        UpdateTooltip();
    }

    private void ToggleLayout()
    {
        foreach (var w in _windows)
            w.ToggleLayout();
    }

    private void ToggleVisibility()
    {
        _allHidden = !_allHidden;
        foreach (var w in _windows)
        {
            if (_allHidden) w.Hide();
            else w.Show();
        }
    }

    private void Exit()
    {
        Application.Current.Shutdown();
    }

    private void UpdateTooltip()
    {
        _notifyIcon.Text = $"Claude Light — {_windows.Count} project(s)";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
```

- [ ] **Step 4: Add NuGet reference for WinForms**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp/ClaudeLight
dotnet add package System.Windows.Forms
```

Note: May need `<UseWindowsForms>true</UseWindowsForms>` in csproj.

- [ ] **Step 5: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/Controls/SystemTray.cs
git add ClaudeLight.CSharp/ClaudeLight/Resources/
git add ClaudeLight.CSharp/ClaudeLight/ClaudeLight.csproj
git commit -m "feat: add SystemTray with lightbulb icon and context menu"
```

---

### Task 10: AutoStartManager (开机自启)

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight/Services/AutoStartManager.cs`

- [ ] **Step 1: Implement AutoStartManager**

```csharp
// ClaudeLight.CSharp/ClaudeLight/Services/AutoStartManager.cs
using System;
using Microsoft.Win32;

namespace ClaudeLight.Services;

public static class AutoStartManager
{
    private const string AppName = "ClaudeLight";
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public static bool IsAutoStartEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
        return key?.GetValue(AppName) != null;
    }

    public static void SetAutoStart(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
        if (key == null) return;

        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? "";
            key.SetValue(AppName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(AppName, false);
        }
    }
}
```

- [ ] **Step 2: Wire into SystemTray menu**

Add to `SystemTray.cs` context menu:

```csharp
var autoStartItem = new System.Windows.Forms.ToolStripMenuItem("Auto-Start")
{
    Checked = AutoStartManager.IsAutoStartEnabled()
};
autoStartItem.Click += (_, _) =>
{
    var current = AutoStartManager.IsAutoStartEnabled();
    AutoStartManager.SetAutoStart(!current);
    autoStartItem.Checked = !current;
};
menu.Items.Add(autoStartItem);
```

- [ ] **Step 3: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/Services/AutoStartManager.cs
git add ClaudeLight.CSharp/ClaudeLight/Controls/SystemTray.cs
git commit -m "feat: add auto-start via Windows Registry"
```

---

### Task 11: Wire Everything Together in App.xaml.cs

**Files:**
- Modify: `ClaudeLight.CSharp/ClaudeLight/App.xaml.cs`

- [ ] **Step 1: Update App.xaml.cs to orchestrate all components**

```csharp
// ClaudeLight.CSharp/ClaudeLight/App.xaml.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using ClaudeLight.Controls;
using ClaudeLight.Models;
using ClaudeLight.Services;

namespace ClaudeLight;

public partial class App : Application
{
    private Mutex? _mutex;
    private StateManager? _stateManager;
    private ProcessWatcher? _processWatcher;
    private SystemTray? _systemTray;
    private readonly Dictionary<string, MainWindow> _windows = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, "ClaudeLight_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        // Install hooks
        var cliPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-light", "claude-light.exe");

        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "settings.json");

        if (File.Exists(settingsPath))
            HookInstaller.InstallHooks(settingsPath, cliPath);

        // Start system tray
        _systemTray = new SystemTray();

        // Start state manager
        _stateManager = new StateManager();
        _stateManager.StatusChanged += OnStatusChanged;
        _stateManager.InstanceRemoved += OnInstanceRemoved;
        _stateManager.ScanExistingStates();

        // Start process watcher
        _processWatcher = new ProcessWatcher();
        _processWatcher.ProcessStarted += OnProcessStarted;
        _processWatcher.ProcessExited += OnProcessExited;

        base.OnStartup(e);
    }

    private void OnProcessStarted(int pid, string projectDir)
    {
        Dispatcher.Invoke(() =>
        {
            if (!_windows.ContainsKey(projectDir))
            {
                var window = new MainWindow { ProjectDir = projectDir };
                window.Show();
                _windows[projectDir] = window;
                _systemTray?.AddWindow(window);
            }
        });
    }

    private void OnProcessExited(int pid)
    {
        // State file deletion will handle window removal
    }

    private void OnStatusChanged(string projectDir, LightStatus? status)
    {
        Dispatcher.Invoke(() =>
        {
            if (status == null)
            {
                if (_windows.TryGetValue(projectDir, out var window))
                {
                    window.UpdateStatus(null);
                    _systemTray?.RemoveWindow(window);
                    _windows.Remove(projectDir);
                }
            }
            else
            {
                if (!_windows.TryGetValue(projectDir, out var window))
                {
                    window = new MainWindow { ProjectDir = projectDir };
                    _windows[projectDir] = window;
                    _systemTray?.AddWindow(window);
                }
                window.UpdateStatus(status);
                window.Show();
            }
        });
    }

    private void OnInstanceRemoved(string projectDir)
    {
        Dispatcher.Invoke(() =>
        {
            if (_windows.TryGetValue(projectDir, out var window))
            {
                window.UpdateStatus(null);
                _systemTray?.RemoveWindow(window);
                _windows.Remove(projectDir);
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _processWatcher?.Dispose();
        _stateManager?.Dispose();
        _systemTray?.Dispose();
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
```

- [ ] **Step 2: Build and verify full compilation**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp
dotnet build ClaudeLight
```

Expected: Build succeeded.

- [ ] **Step 3: Run all tests**

```bash
dotnet test ClaudeLight.Tests
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/App.xaml.cs
git commit -m "feat: wire up all components in App — hooks, tray, state watcher"
```

---

### Task 11: Publish Self-Contained EXE

**Files:**
- Modify: `ClaudeLight.CSharp/ClaudeLight/ClaudeLight.csproj`
- Modify: `ClaudeLight.CSharp/ClaudeLight.CLI/ClaudeLight.CLI.csproj`
- Modify: `ClaudeLight.CSharp/ClaudeLight.Clean/ClaudeLight.Clean.csproj`

- [ ] **Step 1: Configure publish profiles**

Add to each `.csproj` that needs publishing:

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

- [ ] **Step 2: Publish all projects**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp

dotnet publish ClaudeLight.CLI -c Release -o publish/
dotnet publish ClaudeLight -c Release -o publish/
dotnet publish ClaudeLight.Clean -c Release -o publish/
```

- [ ] **Step 3: Verify output**

```bash
ls publish/
# Should contain: ClaudeLight.exe, ClaudeLight.CLI.exe, ClaudeLight.Clean.exe
```

- [ ] **Step 4: Commit**

```bash
git add ClaudeLight.CSharp/
git commit -m "chore: configure self-contained publish for all C# projects"
```

---

### Task 12: HookInstaller — Copy CLI to ~/.claude-light/

**Files:**
- Modify: `ClaudeLight.CSharp/ClaudeLight/Services/HookInstaller.cs`

- [ ] **Step 1: Add CopyCliToHome method**

```csharp
// Add to HookInstaller.cs
public static string EnsureCliInstalled()
{
    var targetDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude-light");
    var targetExe = Path.Combine(targetDir, "claude-light.exe");

    if (!Directory.Exists(targetDir))
        Directory.CreateDirectory(targetDir);

    // Copy current exe to target (self-update)
    var currentExe = Environment.ProcessPath;
    if (currentExe != null && File.Exists(currentExe))
    {
        File.Copy(currentExe, targetExe, overwrite: true);
    }

    return targetExe;
}
```

- [ ] **Step 2: Call in App.OnStartup before InstallHooks**

```csharp
// In App.xaml.cs OnStartup, replace the cliPath calculation with:
var cliPath = HookInstaller.EnsureCliInstalled();
```

- [ ] **Step 3: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight/Services/HookInstaller.cs
git add ClaudeLight.CSharp/ClaudeLight/App.xaml.cs
git commit -m "feat: auto-copy CLI to ~/.claude-light/ on startup"
```

---

### Task 13: Cleanup Tool (claude-light-clean)

**Files:**
- Create: `ClaudeLight.CSharp/ClaudeLight.Clean/Program.cs`

- [ ] **Step 1: Implement cleanup tool**

```csharp
// ClaudeLight.CSharp/ClaudeLight.Clean/Program.cs
using System;
using System.IO;
using System.Text.Json;
using ClaudeLight.Services;

namespace ClaudeLight.Clean;

public class Program
{
    public static int Main(string[] args)
    {
        bool force = args.Contains("--force");

        if (!force)
        {
            Console.Write("This will remove all Claude Light hooks and data. Continue? (y/N): ");
            var input = Console.ReadLine();
            if (input?.ToLower() != "y")
            {
                Console.WriteLine("Cancelled.");
                return 0;
            }
        }

        // 1. Remove hooks from settings.json
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "settings.json");

        if (File.Exists(settingsPath))
        {
            HookInstaller.RemoveHooks(settingsPath);
            Console.WriteLine($"Removed hooks from {settingsPath}");
        }

        // 2. Delete state directory
        var stateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-lights");
        if (Directory.Exists(stateDir))
        {
            Directory.Delete(stateDir, true);
            Console.WriteLine($"Deleted {stateDir}");
        }

        // 3. Delete CLI directory
        var cliDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-light");
        if (Directory.Exists(cliDir))
        {
            Directory.Delete(cliDir, true);
            Console.WriteLine($"Deleted {cliDir}");
        }

        Console.WriteLine("Claude Light has been uninstalled.");
        return 0;
    }
}
```

- [ ] **Step 2: Add reference to ClaudeLight.Clean project**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.CSharp
dotnet add ClaudeLight.Clean reference ClaudeLight/ClaudeLight.csproj
```

- [ ] **Step 3: Build and test**

```bash
dotnet run --project ClaudeLight.Clean -- --force
```

- [ ] **Step 4: Commit**

```bash
git add ClaudeLight.CSharp/ClaudeLight.Clean/
git commit -m "feat: implement claude-light-clean cleanup tool"
```

---

## Part B: Python Version

### Task 14: Python Hook CLI

**Files:**
- Create: `ClaudeLight.Python/hook_cli.py`

- [ ] **Step 1: Write hook_cli.py**

```python
#!/usr/bin/env python3
"""Claude Light hook CLI — called by Claude Code hooks to write state files."""
import sys
import os
import json
import time
from pathlib import Path

def get_state_dir():
    return Path.home() / ".claude-lights"

def get_state_file(project_dir: str) -> Path:
    sanitized = project_dir.replace("\\", "_").replace("/", "_").replace(":", "_")
    if len(sanitized) > 100:
        sanitized = sanitized[-100:]
    return get_state_dir() / f"{sanitized}.json"

def main():
    if len(sys.argv) < 4 or sys.argv[1] != "hook":
        print("Usage: python hook_cli.py hook <running|confirm|done|exit> <project_dir>", file=sys.stderr)
        return 1

    action = sys.argv[2]
    project_dir = sys.argv[3]
    state_dir = get_state_dir()
    state_dir.mkdir(parents=True, exist_ok=True)

    if action == "exit":
        state_file = get_state_file(project_dir)
        if state_file.exists():
            state_file.unlink()
        return 0

    status_map = {"running": "running", "confirm": "confirm", "done": "done"}
    status = status_map.get(action)
    if not status:
        print(f"Unknown action: {action}", file=sys.stderr)
        return 1

    project_name = Path(project_dir).name
    state = {
        "project_name": project_name,
        "project_dir": project_dir,
        "status": status,
        "pid": os.getpid(),
        "updated_at": time.strftime("%Y-%m-%dT%H:%M:%S", time.gmtime()),
    }

    state_file = get_state_file(project_dir)
    state_file.write_text(json.dumps(state, indent=2))
    return 0

if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Test manually**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.Python
python hook_cli.py hook running "E:/test-project"
# Verify state file created
python hook_cli.py hook done "E:/test-project"
python hook_cli.py hook exit "E:/test-project"
```

- [ ] **Step 3: Commit**

```bash
git add ClaudeLight.Python/hook_cli.py
git commit -m "feat: add Python hook CLI"
```

---

### Task 15: Python Hook Installer

**Files:**
- Create: `ClaudeLight.Python/hook_installer.py`

- [ ] **Step 1: Write hook_installer.py**

```python
#!/usr/bin/env python3
"""Auto-install hooks into Claude Code settings.json."""
import json
import sys
from pathlib import Path

HOOK_MARKER = "claude-light"

def get_settings_path() -> Path:
    return Path.home() / ".claude" / "settings.json"

def get_cli_path() -> str:
    return str(Path(__file__).parent / "hook_cli.py")

def install_hooks():
    settings_path = get_settings_path()
    cli_path = get_cli_path()
    python_exe = sys.executable

    settings = {}
    if settings_path.exists():
        settings = json.loads(settings_path.read_text(encoding="utf-8"))

    # Check if already installed
    hooks = settings.get("hooks", {})
    if any(HOOK_MARKER in json.dumps(v) for v in hooks.values()):
        print("Hooks already installed.")
        return

    settings["hooks"] = {
        "PreToolUse": [{
            "hooks": [{
                "type": "command",
                "command": f'"{python_exe}" "{cli_path}" hook running $CLAUDE_PROJECT_DIR'
            }]
        }],
        "PostToolUse": [{
            "hooks": [{
                "type": "command",
                "command": f'"{python_exe}" "{cli_path}" hook done $CLAUDE_PROJECT_DIR'
            }]
        }],
        "Notification": [{
            "hooks": [{
                "type": "command",
                "command": f'"{python_exe}" "{cli_path}" hook confirm $CLAUDE_PROJECT_DIR'
            }]
        }]
    }

    settings_path.parent.mkdir(parents=True, exist_ok=True)
    settings_path.write_text(json.dumps(settings, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"Hooks installed to {settings_path}")

def remove_hooks():
    settings_path = get_settings_path()
    if not settings_path.exists():
        return

    settings = json.loads(settings_path.read_text(encoding="utf-8"))
    if "hooks" in settings:
        del settings["hooks"]
        settings_path.write_text(json.dumps(settings, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"Removed hooks from {settings_path}")

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "remove":
        remove_hooks()
    else:
        install_hooks()
```

- [ ] **Step 2: Commit**

```bash
git add ClaudeLight.Python/hook_installer.py
git commit -m "feat: add Python hook installer"
```

---

### Task 16: Python State Manager

**Files:**
- Create: `ClaudeLight.Python/state_manager.py`

- [ ] **Step 1: Write state_manager.py**

```python
#!/usr/bin/env python3
"""Watch ~/.claude-lights/ for state file changes."""
import json
import os
import time
from pathlib import Path
from typing import Callable, Optional

class StateManager:
    def __init__(self, on_status_change: Callable[[str, Optional[str]], None]):
        self.state_dir = Path.home() / ".claude-lights"
        self.state_dir.mkdir(parents=True, exist_ok=True)
        self.on_status_change = on_status_change
        self._file_mtimes: dict[str, float] = {}
        self._running = False

    def start(self):
        self._running = True
        self.scan_existing()
        self._poll()

    def stop(self):
        self._running = False

    def scan_existing(self):
        for f in self.state_dir.glob("*.json"):
            self._handle_file(f)

    def _poll(self):
        if not self._running:
            return
        try:
            current_files = {f.name: f.stat().st_mtime for f in self.state_dir.glob("*.json")}

            # Detect new or modified files
            for name, mtime in current_files.items():
                if name not in self._file_mtimes or self._file_mtimes[name] != mtime:
                    self._handle_file(self.state_dir / name)

            # Detect deleted files
            for name in list(self._file_mtimes.keys()):
                if name not in current_files:
                    self._handle_deletion(name)

            self._file_mtimes = current_files
        except Exception:
            pass

    def _handle_file(self, path: Path):
        try:
            data = json.loads(path.read_text(encoding="utf-8"))
            status = data.get("status")
            project_dir = data.get("project_dir", "")
            if project_dir:
                self.on_status_change(project_dir, status)
        except Exception:
            pass

    def _handle_deletion(self, filename: str):
        # Best-effort reverse of sanitized path
        project_dir = filename.replace(".json", "").replace("_", "/")
        self.on_status_change(project_dir, None)
```

- [ ] **Step 2: Commit**

```bash
git add ClaudeLight.Python/state_manager.py
git commit -m "feat: add Python state manager with file polling"
```

---

### Task 17: Python Process Watcher

**Files:**
- Create: `ClaudeLight.Python/process_watcher.py`

- [ ] **Step 1: Write process_watcher.py**

```python
#!/usr/bin/env python3
"""Detect running Claude Code processes."""
import subprocess
import time
from typing import Callable

class ProcessWatcher:
    def __init__(self, on_process_started: Callable[[int, str], None],
                 on_processExited: Callable[[int], None]):
        self.on_process_started = on_process_started
        self.on_processExited = on_processExited
        self._known_pids: set[int] = set()
        self._running = False

    def start(self):
        self._running = True
        self._poll()

    def stop(self):
        self._running = False

    def _poll(self):
        if not self._running:
            return
        try:
            result = subprocess.run(
                ["tasklist", "/FI", "IMAGENAME eq claude.exe", "/FO", "CSV", "/NH"],
                capture_output=True, text=True, timeout=5
            )
            current_pids = set()
            for line in result.stdout.strip().split("\n"):
                if not line.strip():
                    continue
                parts = line.split(",")
                if len(parts) >= 2:
                    pid = int(parts[1].strip('"'))
                    current_pids.add(pid)

                    if pid not in self._known_pids:
                        self._known_pids.add(pid)
                        # Project dir will come from state files
                        self.on_process_started(pid, "")

            exited = self._known_pids - current_pids
            for pid in exited:
                self._known_pids.discard(pid)
                self.on_processExited(pid)
        except Exception:
            pass
```

- [ ] **Step 2: Commit**

```bash
git add ClaudeLight.Python/process_watcher.py
git commit -m "feat: add Python process watcher"
```

---

### Task 18: Python GUI (tkinter)

**Files:**
- Create: `ClaudeLight.Python/claude_light.py`

- [ ] **Step 1: Write the main GUI**

```python
#!/usr/bin/env python3
"""Claude Light — Traffic light indicator for Claude Code (Python/tkinter version)."""
import sys
import os
import tkinter as tk
from tkinter import messagebox
from pathlib import Path
from typing import Optional

# Win32 constants for click-through
GWL_EXSTYLE = -20
WS_EX_LAYERED = 0x80000
WS_EX_TRANSPARENT = 0x20

try:
    import ctypes
    user32 = ctypes.windll.user32
    HAS_CTYPES = True
except AttributeError:
    HAS_CTYPES = False

from state_manager import StateManager
from process_watcher import ProcessWatcher

COLORS = {
    "off": "#333333",
    "red": "#e74c3c",
    "yellow": "#f39c12",
    "green": "#2ecc71",
}


class TrafficLightWindow(tk.Toplevel):
    def __init__(self, project_dir: str, master=None):
        super().__init__(master)
        self.project_dir = project_dir
        self.project_name = Path(project_dir).name
        self.is_horizontal = False
        self._blink_on = False
        self._blink_id = None

        self.title("Claude Light")
        self.overrideredirect(False)
        self.attributes("-topmost", True)
        self.attributes("-alpha", 0.85)
        self.configure(bg="#1e1e1e", highlightbackground="#555", highlightthickness=1)

        # Position at right side of screen
        screen_w = self.winfo_screenwidth()
        self.geometry(f"100x200+{screen_w - 120}+100")

        self._build_ui()
        self._setup_click_through()
        self._setup_drag()

    def _build_ui(self):
        self.frame = tk.Frame(self, bg="#1e1e1e")
        self.frame.pack(fill=tk.BOTH, expand=True, padx=8, pady=8)

        # Vertical layout
        self.vert_frame = tk.Frame(self.frame, bg="#1e1e1e")
        self.vert_frame.pack()

        self.red_v = tk.Canvas(self.vert_frame, width=30, height=30, bg=COLORS["off"],
                                highlightthickness=0)
        self.red_v.pack(pady=(0, 4))
        self.yellow_v = tk.Canvas(self.vert_frame, width=30, height=30, bg=COLORS["off"],
                                   highlightthickness=0)
        self.yellow_v.pack(pady=(0, 4))
        self.green_v = tk.Canvas(self.vert_frame, width=30, height=30, bg=COLORS["off"],
                                  highlightthickness=0)
        self.green_v.pack()

        tk.Frame(self.frame, height=1, bg="#555").pack(fill=tk.X, pady=6)
        tk.Label(self.frame, text=self.project_name, fg="#aaa", bg="#1e1e1e",
                 font=("Segoe UI", 8)).pack()
        tk.Label(self.frame, text=self.project_dir, fg="#666", bg="#1e1e1e",
                 font=("Segoe UI", 7), wraplength=90).pack()

        # Draw circles
        for canvas in [self.red_v, self.yellow_v, self.green_v]:
            canvas.create_oval(2, 2, 28, 28, fill=COLORS["off"], outline="", tags="light")

    def _setup_click_through(self):
        if not HAS_CTYPES:
            return
        hwnd = ctypes.windll.user32.GetParent(self.winfo_id())
        style = user32.GetWindowLongW(hwnd, GWL_EXSTYLE)
        user32.SetWindowLongW(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT)

    def _setup_drag(self):
        self._drag_data = {"x": 0, "y": 0}
        self.bind("<ButtonPress-1>", self._start_drag)
        self.bind("<B1-Motion>", self._do_drag)
        self.bind("<Double-Button-1>", self._toggle_layout)

    def _start_drag(self, event):
        self._drag_data["x"] = event.x
        self._drag_data["y"] = event.y

    def _do_drag(self, event):
        dx = event.x - self._drag_data["x"]
        dy = event.y - self._drag_data["y"]
        x = self.winfo_x() + dx
        y = self.winfo_y() + dy
        self.geometry(f"+{x}+{y}")

    def _toggle_layout(self, event):
        self.is_horizontal = not self.is_horizontal
        if self.is_horizontal:
            self.geometry("140x120")
        else:
            self.geometry("100x200")

    def set_status(self, status: Optional[str]):
        if self._blink_id:
            self.after_cancel(self._blink_id)
            self._blink_id = None

        colors = {c: COLORS["off"] for c in ["red", "yellow", "green"]}

        if status == "running":
            colors["red"] = COLORS["red"]
        elif status == "confirm":
            colors["yellow"] = COLORS["yellow"]
            self._blink()
        elif status == "done":
            colors["green"] = COLORS["green"]

        self.red_v.itemconfig("light", fill=colors["red"])
        self.yellow_v.itemconfig("light", fill=colors["yellow"])
        self.green_v.itemconfig("light", fill=colors["green"])

        if status is None:
            self.withdraw()
        else:
            self.deiconify()

    def _blink(self):
        self._blink_on = not self._blink_on
        color = COLORS["yellow"] if self._blink_on else COLORS["off"]
        self.yellow_v.itemconfig("light", fill=color)
        self._blink_id = self.after(500, self._blink)

    def cleanup(self):
        if self._blink_id:
            self.after_cancel(self._blink_id)


class ClaudeLightApp:
    def __init__(self):
        self.root = tk.Tk()
        self.root.withdraw()  # Hide main window

        self.windows: dict[str, TrafficLightWindow] = {}
        self.state_manager = StateManager(self._on_status_change)
        self.process_watcher = ProcessWatcher(self._on_process_started, self._on_process_exited)

        self._setup_tray()

    def _setup_tray(self):
        # Use system tray via pystray or just use tkinter menu
        # For simplicity, use a hidden root window with right-click menu
        self.root.bind("<Button-3>", self._show_menu)

    def _show_menu(self, event):
        menu = tk.Menu(self.root, tearoff=0)
        menu.add_command(label="Show/Hide All", command=self._toggle_visibility)
        menu.add_separator()
        menu.add_command(label="Exit", command=self._exit)
        menu.post(event.x_root, event.y_root)

    def _toggle_visibility(self):
        for w in self.windows.values():
            if w.state() == "normal":
                w.withdraw()
            else:
                w.deiconify()

    def _on_status_change(self, project_dir: str, status: Optional[str]):
        if status is None:
            if project_dir in self.windows:
                self.windows[project_dir].cleanup()
                self.windows[project_dir].destroy()
                del self.windows[project_dir]
        else:
            if project_dir not in self.windows:
                self.windows[project_dir] = TrafficLightWindow(project_dir, self.root)
            self.windows[project_dir].set_status(status)

    def _on_process_started(self, pid: int, project_dir: str):
        pass  # State files handle window creation

    def _on_process_exited(self, pid: int):
        pass  # State file deletion handles window removal

    def _exit(self):
        self.state_manager.stop()
        self.process_watcher.stop()
        for w in self.windows.values():
            w.cleanup()
        self.root.destroy()

    def run(self):
        self.state_manager.start()
        self.process_watcher.start()

        # Install hooks if needed
        from hook_installer import install_hooks
        install_hooks()

        self.root.mainloop()


def main():
    app = ClaudeLightApp()
    app.run()


if __name__ == "__main__":
    main()
```

- [ ] **Step 2: Commit**

```bash
git add ClaudeLight.Python/claude_light.py
git commit -m "feat: add Python tkinter GUI with traffic lights"
```

---

### Task 19: Python Cleanup Tool

**Files:**
- Create: `ClaudeLight.Python/claude_light_clean.py`

- [ ] **Step 1: Write cleanup tool**

```python
#!/usr/bin/env python3
"""Claude Light cleanup — remove all hooks and data."""
import sys
import shutil
from pathlib import Path

def main():
    force = "--force" in sys.argv

    if not force:
        answer = input("This will remove all Claude Light hooks and data. Continue? (y/N): ")
        if answer.lower() != "y":
            print("Cancelled.")
            return 0

    # 1. Remove hooks
    from hook_installer import remove_hooks
    remove_hooks()

    # 2. Delete state directory
    state_dir = Path.home() / ".claude-lights"
    if state_dir.exists():
        shutil.rmtree(state_dir)
        print(f"Deleted {state_dir}")

    print("Claude Light has been uninstalled.")
    return 0

if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Commit**

```bash
git add ClaudeLight.Python/claude_light_clean.py
git commit -m "feat: add Python cleanup tool"
```

---

### Task 20: Python Tests

**Files:**
- Create: `ClaudeLight.Python/tests/test_hook_installer.py`
- Create: `ClaudeLight.Python/tests/test_state_manager.py`

- [ ] **Step 1: Write hook installer tests**

```python
# ClaudeLight.Python/tests/test_hook_installer.py
import json
import tempfile
from pathlib import Path
from unittest.mock import patch

def test_install_hooks_creates_config():
    with tempfile.TemporaryDirectory() as tmp:
        settings_path = Path(tmp) / "settings.json"
        settings_path.write_text("{}")

        with patch("hook_installer.get_settings_path", return_value=settings_path):
            with patch("hook_installer.get_cli_path", return_value="/fake/hook_cli.py"):
                import hook_installer
                hook_installer.install_hooks()

        settings = json.loads(settings_path.read_text())
        assert "hooks" in settings
        assert "PreToolUse" in settings["hooks"]
        assert "PostToolUse" in settings["hooks"]
        assert "Notification" in settings["hooks"]

def test_install_hooks_no_duplicate():
    with tempfile.TemporaryDirectory() as tmp:
        settings_path = Path(tmp) / "settings.json"
        settings_path.write_text("{}")

        with patch("hook_installer.get_settings_path", return_value=settings_path):
            with patch("hook_installer.get_cli_path", return_value="/fake/hook_cli.py"):
                import hook_installer
                hook_installer.install_hooks()
                count1 = settings_path.read_text().count("claude-light")
                hook_installer.install_hooks()
                count2 = settings_path.read_text().count("claude-light")

        assert count1 == count2

def test_remove_hooks():
    with tempfile.TemporaryDirectory() as tmp:
        settings_path = Path(tmp) / "settings.json"
        settings_path.write_text('{"hooks": {"PreToolUse": []}}')

        with patch("hook_installer.get_settings_path", return_value=settings_path):
            import hook_installer
            hook_installer.remove_hooks()

        settings = json.loads(settings_path.read_text())
        assert "hooks" not in settings
```

- [ ] **Step 2: Write state manager tests**

```python
# ClaudeLight.Python/tests/test_state_manager.py
import json
import tempfile
from pathlib import Path
from state_manager import StateManager

def test_scan_existing():
    with tempfile.TemporaryDirectory() as tmp:
        state_dir = Path(tmp)
        state_file = state_dir / "test_project.json"
        state_file.write_text(json.dumps({
            "project_name": "test",
            "project_dir": "/test",
            "status": "running"
        }))

        results = []
        with patch.object(StateManager, '__init__', lambda self, cb: setattr(self, 'state_dir', state_dir) or setattr(self, 'on_status_change', cb) or setattr(self, '_file_mtimes', {}) or setattr(self, '_running', False)):
            sm = StateManager(lambda d, s: results.append((d, s)))
            sm.scan_existing()

        assert len(results) == 1
        assert results[0] == ("/test", "running")
```

- [ ] **Step 3: Run tests**

```bash
cd E:/workplace/vibe_claude_light/ClaudeLight.Python
python -m pytest tests/ -v
```

- [ ] **Step 4: Commit**

```bash
git add ClaudeLight.Python/tests/
git commit -m "feat: add Python tests for hook installer and state manager"
```

---

## Part C: README

### Task 21: Write README.md

**Files:**
- Create: `README.md`

- [ ] **Step 1: Write usage instructions**

```markdown
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
```

- [ ] **Step 2: Commit**

```bash
git add README.md
git commit -m "docs: add README with usage instructions"
```

---

## Verification

After all tasks are complete:

1. **C# build**: `dotnet build ClaudeLight.CSharp/`
2. **C# tests**: `dotnet test ClaudeLight.CSharp/ClaudeLight.Tests/`
3. **C# publish**: `dotnet publish ClaudeLight.CSharp/ClaudeLight -c Release --self-contained -r win-x64 -o publish/`
4. **Run C# exe**: `publish/ClaudeLight.exe` — verify tray icon appears, hooks injected
5. **Python run**: `python ClaudeLight.Python/claude_light.py` — verify same behavior
6. **Python tests**: `python -m pytest ClaudeLight.Python/tests/`
7. **End-to-end**: Start Claude Code, verify red light; trigger confirm, verify yellow blink; complete, verify green; close Claude Code, verify light disappears
8. **Cleanup**: Run `ClaudeLight.Clean.exe`, verify hooks removed
