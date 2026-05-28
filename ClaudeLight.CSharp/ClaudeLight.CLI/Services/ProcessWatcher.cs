using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ClaudeLight.Services;

public class ProcessWatcher : IDisposable
{
    private readonly Timer _timer;
    private readonly HashSet<int> _knownPids = new();

    public event Action<int, string>? ProcessStarted;
    public event Action<int>? ProcessExited;

    public ProcessWatcher()
    {
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
                        _knownPids.Add(proc.Id);
                        ProcessStarted?.Invoke(proc.Id, "");
                    }
                }
                catch { }
            }

            var exited = _knownPids.Where(p => !currentPids.Contains(p)).ToList();
            foreach (var pid in exited)
            {
                _knownPids.Remove(pid);
                ProcessExited?.Invoke(pid);
            }
        }
        catch { }
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
