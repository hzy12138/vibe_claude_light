using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using ClaudeLight.Controls;
using ClaudeLight.Models;
using ClaudeLight.Services;
using Application = System.Windows.Application;

namespace ClaudeLight;

public partial class App : Application
{
    private Mutex? _mutex;
    private StateFileWatcher? _stateWatcher;
    private ProcessWatcher? _processWatcher;
    private SystemTray? _systemTray;
    private readonly Dictionary<string, MainWindow> _windows = new();

    /// <summary>
    /// 用户"关闭并从列表移除"的项目集合（规范化路径）。
    /// 关闭后灯消失、从托盘列表移除，直到该项目再次 running 才恢复显示。
    /// </summary>
    private readonly HashSet<string> _dismissed = new();

    /// <summary>
    /// 用户"隐藏灯"的项目集合（规范化路径）。
    /// 隐藏后灯不显示，但项目仍保留在托盘列表，可随时手动重新显示。
    /// </summary>
    private readonly HashSet<string> _hidden = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        // 初始化日志
        Logger.Init(AppContext.BaseDirectory);

        // Global exception handler
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Logger.Error("App", $"Unhandled exception: {ex}");
        };
        DispatcherUnhandledException += (s, args) =>
        {
            Logger.Error("App", $"Dispatcher exception: {args.Exception}");
            args.Handled = true;
        };

        _mutex = new Mutex(true, "ClaudeLight_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Logger.Warn("App", "Another instance is already running, shutting down");
            Shutdown();
            return;
        }

        // 清理过期的状态文件（超过 1 小时未更新的）
        CleanStaleStateFiles();

        try
        {
            // Install hooks
            var cliPath = HookInstaller.EnsureCliInstalled();
            Logger.Info("App", $"CLI path: {(string.IsNullOrEmpty(cliPath) ? "NOT FOUND" : cliPath)}");

            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", "settings.json");

            if (File.Exists(settingsPath))
            {
                HookInstaller.InstallHooks(settingsPath, cliPath);
                Logger.Info("App", $"Hooks installed to {settingsPath}");
            }
            else
            {
                Logger.Warn("App", $"settings.json not found at {settingsPath}, skipping hook install");
            }

            // Start system tray
            _systemTray = new SystemTray();
            _systemTray.CloseRequested += OnProjectClose;
            _systemTray.HideToggleRequested += OnProjectHideToggle;
            _systemTray.IsProjectHidden = dir => _hidden.Contains(LightState.NormalizeDir(dir));
            Logger.Info("App", "System tray started");
        }
        catch (Exception ex)
        {
            Logger.Error("App", $"Startup error: {ex}");
            System.Windows.MessageBox.Show($"Startup error: {ex.Message}\n\nCheck claude-light.log for details.", "ClaudeLight Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        // Start state file watcher
        _stateWatcher = new StateFileWatcher();
        _stateWatcher.StatusChanged += OnStatusChanged;
        _stateWatcher.InstanceRemoved += OnInstanceRemoved;
        Logger.Info("App", "StateFileWatcher started, scanning existing states...");
        _stateWatcher.ScanExistingStates();
        Logger.Info("App", $"Scanned existing states: {_windows.Count} window(s) created");

        // Start process watcher
        _processWatcher = new ProcessWatcher();
        _processWatcher.ProcessStarted += OnProcessStarted;
        _processWatcher.ProcessExited += OnProcessExited;
        Logger.Info("App", "ProcessWatcher started");

        // 启动完成提示
        _systemTray?.ShowStartupBalloon();

        base.OnStartup(e);
    }

    /// <summary>
    /// 清理启动时发现的过期状态文件。
    /// 超过 1 小时未更新的文件视为过期（Claude Code 进程已不存在）。
    /// </summary>
    private static void CleanStaleStateFiles()
    {
        try
        {
            var stateDir = LightState.GetStateDir();
            if (!Directory.Exists(stateDir)) return;

            var staleThreshold = DateTime.UtcNow.AddHours(-1);
            var cleaned = 0;

            foreach (var file in Directory.GetFiles(stateDir, "*.json"))
            {
                try
                {
                    var lastWrite = File.GetLastWriteTimeUtc(file);
                    if (lastWrite < staleThreshold)
                    {
                        File.Delete(file);
                        cleaned++;
                        Logger.Info("App", $"Cleaned stale state file: {Path.GetFileName(file)} (last write: {lastWrite:yyyy-MM-dd HH:mm})");
                    }
                }
                catch { }
            }

            if (cleaned > 0)
                Logger.Info("App", $"Cleaned {cleaned} stale state file(s)");
        }
        catch (Exception ex)
        {
            Logger.Error("App", $"Error cleaning stale state files: {ex.Message}");
        }
    }

    private void OnProcessStarted(int pid, string projectDir)
    {
        Logger.Info("App", $"Claude process started: pid={pid}");
    }

    private void OnProcessExited(int pid)
    {
        Logger.Info("App", $"Claude process exited: pid={pid}");
    }

    private void OnStatusChanged(string projectDir, LightStatus? status)
    {
        // 标准化路径：统一使用小写和正斜杠，避免同一项目创建多个窗口
        var normalizedDir = LightState.NormalizeDir(projectDir);

        Logger.Info("App", $"Status changed: dir={projectDir}, status={status?.ToString() ?? "null"}, windows={_windows.Count}");

        Dispatcher.Invoke(() =>
        {
            try
            {
                if (status == null)
                {
                    // 状态文件失效 → 关窗，并清除隐藏/关闭标记（该项目的下一次 running 将重新出现）
                    _dismissed.Remove(normalizedDir);
                    _hidden.Remove(normalizedDir);
                    if (_windows.TryGetValue(normalizedDir, out var window))
                    {
                        Logger.Info("App", $"Removing window for: {normalizedDir}");
                        window.UpdateStatus(null);
                        _systemTray?.RemoveWindow(window);
                        _windows.Remove(normalizedDir);
                    }
                }
                else
                {
                    // 用户已手动隐藏该项目（托盘点击关闭）→ 隐藏；一旦 Claude 再次开始工作即恢复显示
                    if (_dismissed.Contains(normalizedDir))
                    {
                        // 再次 running（PreToolUse/PostToolUse）或等待确认（PermissionRequest）→ 恢复显示
                        if (status == LightStatus.Running || status == LightStatus.Confirm)
                        {
                            _dismissed.Remove(normalizedDir);
                            Logger.Info("App", $"Project running again, restore: {normalizedDir}");
                        }
                        else
                        {
                            Logger.Info("App", $"Project dismissed, skip showing: {normalizedDir}");
                            return;
                        }
                    }

                    if (!_windows.TryGetValue(normalizedDir, out var window))
                    {
                        // check：根据已有窗口数量偏移，避免重叠
                        var offset = _windows.Count * 30;
                        window = new MainWindow
                        {
                            ProjectDir = projectDir,
                            Left = 1700 + offset,
                            Top = 50 + offset
                        };
                        _windows[normalizedDir] = window;
                        _systemTray?.AddWindow(window);
                        Logger.Info("App", $"Created new window: dir={projectDir}, offset={offset}");
                    }
                    window.UpdateStatus(status);

                    // 已"隐藏灯"的项目 → 更新状态但保持隐藏（项目仍留在托盘列表，可手动重新显示）
                    if (_hidden.Contains(normalizedDir))
                    {
                        window.Hide();
                        return;
                    }

                    window.Show();
                    window.Activate();
                    window.Topmost = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"OnStatusChanged error: {ex}");
            }
        });
    }

    private void OnInstanceRemoved(string projectDir)
    {
        Logger.Info("App", $"Instance removed: {projectDir}");
        var normalizedDir = LightState.NormalizeDir(projectDir);
        Dispatcher.Invoke(() =>
        {
            // 会话结束（SessionEnd/exit 删除状态文件）→ 清除隐藏/关闭标记，下次 running 恢复显示
            _dismissed.Remove(normalizedDir);
            _hidden.Remove(normalizedDir);
            if (_windows.TryGetValue(normalizedDir, out var window))
            {
                window.UpdateStatus(null);
                _systemTray?.RemoveWindow(window);
                _windows.Remove(normalizedDir);
            }
        });
    }

    /// <summary>
    /// 托盘菜单"关闭并从列表移除"：关闭灯 + 从列表移除，再次 running 才恢复。
    /// </summary>
    private void OnProjectClose(string projectDir)
    {
        var normalizedDir = LightState.NormalizeDir(projectDir);
        Logger.Info("App", $"Close project: {normalizedDir}");
        Dispatcher.Invoke(() =>
        {
            _hidden.Remove(normalizedDir);
            _dismissed.Add(normalizedDir);
            if (_windows.TryGetValue(normalizedDir, out var window))
            {
                window.UpdateStatus(null);
                window.Hide();
                _systemTray?.RemoveWindow(window);
                _windows.Remove(normalizedDir);
            }
        });
    }

    /// <summary>
    /// 托盘菜单"隐藏/显示灯"：隐藏时灯不显示但项目保留在列表，可随时重新显示。
    /// </summary>
    private void OnProjectHideToggle(string projectDir)
    {
        var normalizedDir = LightState.NormalizeDir(projectDir);
        Logger.Info("App", $"Toggle hide project: {normalizedDir}");
        Dispatcher.Invoke(() =>
        {
            if (!_hidden.Remove(normalizedDir))
            {
                // 尚未隐藏 → 隐藏灯
                _hidden.Add(normalizedDir);
                if (_windows.TryGetValue(normalizedDir, out var window))
                    window.Hide();
            }
            else if (_windows.TryGetValue(normalizedDir, out var window))
            {
                // 已隐藏 → 重新显示灯
                window.Show();
                window.Activate();
                window.Topmost = true;
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Info("App", "Shutting down...");
        _processWatcher?.Dispose();
        _stateWatcher?.Dispose();
        _systemTray?.Dispose();
        _mutex?.ReleaseMutex();
        Logger.Info("App", "Shutdown complete");
        base.OnExit(e);
    }
}
