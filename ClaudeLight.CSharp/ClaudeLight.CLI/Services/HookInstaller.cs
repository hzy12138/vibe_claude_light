using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace ClaudeLight.Services;

public static class HookInstaller
{
    private const string HookMarker = "claude-light";

    public static string EnsureCliInstalled()
    {
        var targetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-light");
        var targetExe = Path.Combine(targetDir, "claude-light.exe");

        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        var currentExe = Environment.ProcessPath;
        if (currentExe != null && File.Exists(currentExe))
        {
            File.Copy(currentExe, targetExe, overwrite: true);
        }

        return targetExe;
    }

    public static void InstallHooks(string settingsPath, string cliExePath)
    {
        var json = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "{}";
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        if (root.TryGetProperty("hooks", out var hooks) &&
            hooks.GetRawText().Contains(HookMarker))
        {
            return;
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
