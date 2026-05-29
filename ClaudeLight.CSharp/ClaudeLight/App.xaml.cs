using System;
using System.Collections.Generic;
using System.IO;
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

    protected override void OnStartup(StartupEventArgs e)
    {
        // Global exception handler
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "error.log"),
                $"[{DateTime.Now}] Unhandled: {ex}\n");
        };
        DispatcherUnhandledException += (s, args) =>
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "error.log"),
                $"[{DateTime.Now}] Dispatcher: {args.Exception}\n");
            args.Handled = true;
        };

        _mutex = new Mutex(true, "ClaudeLight_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        try
        {
            // Install hooks
            var cliPath = HookInstaller.EnsureCliInstalled();
            var settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude", "settings.json");

            if (File.Exists(settingsPath))
                HookInstaller.InstallHooks(settingsPath, cliPath);

            // Start system tray
            _systemTray = new SystemTray();
        }
        catch (Exception ex)
        {
            File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "error.log"),
                $"[{DateTime.Now}] Startup: {ex}\n");
            System.Windows.MessageBox.Show($"Startup error: {ex.Message}\n\nCheck error.log for details.", "ClaudeLight Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }

        // Start state file watcher
        _stateWatcher = new StateFileWatcher();
        _stateWatcher.StatusChanged += OnStatusChanged;
        _stateWatcher.InstanceRemoved += OnInstanceRemoved;
        _stateWatcher.ScanExistingStates();

        // Start process watcher
        _processWatcher = new ProcessWatcher();
        _processWatcher.ProcessStarted += OnProcessStarted;
        _processWatcher.ProcessExited += OnProcessExited;

        base.OnStartup(e);
    }

    private void OnProcessStarted(int pid, string projectDir)
    {
        // State files handle window creation via StatusChanged
    }

    private void OnProcessExited(int pid)
    {
        // State file deletion handles window removal
    }

    private void OnStatusChanged(string projectDir, LightStatus? status)
    {
        // 标准化路径：统一使用小写和正斜杠，避免同一项目创建多个窗口
        var normalizedDir = projectDir.Replace("\\", "/").ToLowerInvariant();

        Dispatcher.Invoke(() =>
        {
            try
            {
                if (status == null)
                {
                    if (_windows.TryGetValue(normalizedDir, out var window))
                    {
                        window.UpdateStatus(null);
                        _systemTray?.RemoveWindow(window);
                        _windows.Remove(normalizedDir);
                    }
                }
                else
                {
                    if (!_windows.TryGetValue(normalizedDir, out var window))
                    {
                        // 计算新窗口位置：根据已有窗口数量偏移，避免重叠
                        var offset = _windows.Count * 30;
                        window = new MainWindow
                        {
                            ProjectDir = projectDir,
                            Left = 1700 + offset,
                            Top = 50 + offset
                        };
                        _windows[normalizedDir] = window;
                        _systemTray?.AddWindow(window);
                    }
                    window.UpdateStatus(status);
                    window.Show();
                    window.Activate();
                    window.Topmost = true;
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "error.log"),
                    $"[{DateTime.Now}] OnStatusChanged error: {ex}\n");
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
        _stateWatcher?.Dispose();
        _systemTray?.Dispose();
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
