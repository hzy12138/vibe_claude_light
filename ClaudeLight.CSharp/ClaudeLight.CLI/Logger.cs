using System;
using System.IO;
using System.Threading;

namespace ClaudeLight;

public static class Logger
{
    private static readonly object _lock = new();
    private static string? _logPath;

    public static void Init(string baseDir)
    {
        _logPath = Path.Combine(baseDir, "claude-light.log");
        // 保留最近 1MB 日志
        if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 1_000_000)
            File.Delete(_logPath);
        Info("Logger", "========== Claude Light 启动 ==========");
    }

    public static void Info(string tag, string message)
    {
        Write("INFO", tag, message);
    }

    public static void Warn(string tag, string message)
    {
        Write("WARN", tag, message);
    }

    public static void Error(string tag, string message)
    {
        Write("ERROR", tag, message);
    }

    private static void Write(string level, string tag, string message)
    {
        if (_logPath == null) return;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] [{tag}] {message}";
        lock (_lock)
        {
            try { File.AppendAllText(_logPath, line + Environment.NewLine); }
            catch { /* 日志写入失败不影响主流程 */ }
        }
    }
}
