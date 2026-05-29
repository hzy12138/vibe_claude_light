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

        // PostToolUse writes "idle" instead of "done"
        // StateFileWatcher will handle timeout to transition to "done"
        var status = action switch
        {
            "running" => "running",
            "confirm" => "confirm",
            "done" => "idle",
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
