using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClaudeLight.Models;

namespace ClaudeLight.Services;

public class StateFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly string _stateDir;
    private readonly Dictionary<string, LightStatus?> _knownStates = new();

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
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            var state = LightState.ReadFromFile(e.FullPath);
            if (state == null) return;
            var status = state.GetStatus();

            if (_knownStates.TryGetValue(state.ProjectDir, out var oldStatus) && oldStatus == status)
                return;

            _knownStates[state.ProjectDir] = status;
            StatusChanged?.Invoke(state.ProjectDir, status);
        }
        catch { }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        try
        {
            var projectName = Path.GetFileNameWithoutExtension(e.Name);
            // Try to find matching project dir from known states
            foreach (var kvp in _knownStates)
            {
                if (kvp.Key.Replace("\\", "_").Replace("/", "_").Replace(":", "_").EndsWith(projectName, StringComparison.OrdinalIgnoreCase)
                    || projectName.Contains(kvp.Key.Replace("\\", "_").Replace("/", "_").Replace(":", "_")[^Math.Min(50, kvp.Key.Length)..]))
                {
                    _knownStates.Remove(kvp.Key);
                    InstanceRemoved?.Invoke(kvp.Key);
                    return;
                }
            }
        }
        catch { }
    }

    public void ScanExistingStates()
    {
        if (!Directory.Exists(_stateDir)) return;
        foreach (var file in Directory.GetFiles(_stateDir, "*.json"))
        {
            try
            {
                var state = LightState.ReadFromFile(file);
                if (state != null)
                {
                    _knownStates[state.ProjectDir] = state.GetStatus();
                    StatusChanged?.Invoke(state.ProjectDir, state.GetStatus());
                }
            }
            catch { }
        }
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
