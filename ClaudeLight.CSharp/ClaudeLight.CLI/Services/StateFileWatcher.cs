using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ClaudeLight.Models;

namespace ClaudeLight.Services;

public class StateFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly string _stateDir;
    private readonly Dictionary<string, LightStatus?> _knownStates = new();
    private readonly Dictionary<string, Timer> _timers = new();
    private readonly Dictionary<string, string> _fileToProjectDir = new();
    private readonly object _lock = new();

    private const int IdleTimeoutMs = 3000;     // Stop → idle → 3s → Done

    public event Action<string, LightStatus?>? StatusChanged;
    public event Action<string>? InstanceRemoved;

    public StateFileWatcher()
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

        Logger.Info("StateFileWatcher", $"Watching directory: {_stateDir}");
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            Logger.Info("StateFileWatcher", $"File {e.ChangeType}: {e.Name}");

            var state = LightState.ReadFromFile(e.FullPath);
            if (state == null)
            {
                Logger.Warn("StateFileWatcher", $"Failed to read state from: {e.Name}");
                return;
            }

            var status = state.GetStatus();
            Logger.Info("StateFileWatcher", $"State: project={state.ProjectName}, status={state.Status} → {status}");

            var fileName = Path.GetFileNameWithoutExtension(e.Name);
            if (fileName != null)
                _fileToProjectDir[fileName] = state.ProjectDir;

            lock (_lock)
            {
                // 任何新事件到来，取消该项目的所有待处理定时器
                CancelTimer(state.ProjectDir);

                // idle → 启动空闲定时器，超时后转绿灯
                if (status == LightStatus.Idle)
                {
                    _knownStates[state.ProjectDir] = LightStatus.Idle;

                    var projectDir = state.ProjectDir;
                    var timer = new Timer(_ =>
                    {
                        lock (_lock)
                        {
                            _timers.Remove(projectDir);
                            _knownStates[projectDir] = LightStatus.Done;
                        }
                        Logger.Info("StateFileWatcher", $"Idle timer expired → Done: {projectDir}");
                        StatusChanged?.Invoke(projectDir, LightStatus.Done);
                    }, null, IdleTimeoutMs, Timeout.Infinite);

                    _timers[state.ProjectDir] = timer;
                    Logger.Info("StateFileWatcher", $"Started idle timer ({IdleTimeoutMs}ms): {state.ProjectName}");
                    return;
                }

                // confirm → 红底+黄闪（不自动过期，等 PostToolUse/Stop 改变状态）
                if (status == LightStatus.Confirm)
                {
                    _knownStates[state.ProjectDir] = LightStatus.Confirm;
                    Logger.Info("StateFileWatcher", $"State: Confirm (no timer, waiting for next hook)");
                    StatusChanged?.Invoke(state.ProjectDir, LightStatus.Confirm);
                    return;
                }

                // 去重：相同状态不重复通知
                if (_knownStates.TryGetValue(state.ProjectDir, out var oldStatus) && oldStatus == status)
                {
                    Logger.Info("StateFileWatcher", $"Same status, skipping: {status}");
                    return;
                }

                _knownStates[state.ProjectDir] = status;
                Logger.Info("StateFileWatcher", $"State transition: {oldStatus} → {status} for {state.ProjectName}");
            }

            StatusChanged?.Invoke(state.ProjectDir, status);
        }
        catch (Exception ex)
        {
            Logger.Error("StateFileWatcher", $"Error in OnFileChanged: {ex.Message}");
        }
    }

    private void CancelTimer(string projectDir)
    {
        if (_timers.TryGetValue(projectDir, out var timer))
        {
            timer.Dispose();
            _timers.Remove(projectDir);
            Logger.Info("StateFileWatcher", $"Cancelled timer for: {projectDir}");
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            Logger.Info("StateFileWatcher", $"File deleted: {e.Name}");

            var fileName = Path.GetFileNameWithoutExtension(e.Name);
            if (fileName == null) return;

            lock (_lock)
            {
                if (!_fileToProjectDir.TryGetValue(fileName, out var projectDir))
                {
                    Logger.Warn("StateFileWatcher", $"No projectDir mapping for deleted file: {fileName}");
                    return;
                }

                CancelTimer(projectDir);

                if (_knownStates.Remove(projectDir))
                {
                    Logger.Info("StateFileWatcher", $"Removing instance: {projectDir}");
                    InstanceRemoved?.Invoke(projectDir);
                }

                _fileToProjectDir.Remove(fileName);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("StateFileWatcher", $"Error in OnFileDeleted: {ex.Message}");
        }
    }

    public void ScanExistingStates()
    {
        if (!Directory.Exists(_stateDir)) return;

        Logger.Info("StateFileWatcher", $"Scanning existing states in: {_stateDir}");
        var count = 0;

        foreach (var file in Directory.GetFiles(_stateDir, "*.json"))
        {
            try
            {
                var state = LightState.ReadFromFile(file);
                if (state != null)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (fileName != null)
                        _fileToProjectDir[fileName] = state.ProjectDir;
                    _knownStates[state.ProjectDir] = state.GetStatus();
                    StatusChanged?.Invoke(state.ProjectDir, state.GetStatus());
                    count++;
                    Logger.Info("StateFileWatcher", $"Loaded existing state: {state.ProjectName} → {state.GetStatus()}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("StateFileWatcher", $"Error reading {file}: {ex.Message}");
            }
        }

        Logger.Info("StateFileWatcher", $"Scan complete: {count} state(s) loaded");
    }

    public void Dispose()
    {
        _watcher.Dispose();
        lock (_lock)
        {
            foreach (var timer in _timers.Values)
                timer.Dispose();
            _timers.Clear();
        }
    }
}
