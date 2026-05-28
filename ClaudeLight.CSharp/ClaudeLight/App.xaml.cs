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
        _mutex = new Mutex(true, "ClaudeLight_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        // Install hooks
        var cliPath = HookInstaller.EnsureCliInstalled();
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "settings.json");

        if (File.Exists(settingsPath))
            HookInstaller.InstallHooks(settingsPath, cliPath);

        // Start system tray
        _systemTray = new SystemTray();

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
        _stateWatcher?.Dispose();
        _systemTray?.Dispose();
        _mutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
