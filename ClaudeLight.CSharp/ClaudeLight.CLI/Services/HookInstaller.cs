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
        // 直接使用 ClaudeLight.exe 同目录下的 ClaudeLight.CLI.exe
        // 这样 CLI 可以找到 .NET 运行时和所有依赖
        var baseDir = AppContext.BaseDirectory;

        // 优先查找 ClaudeLight.CLI.exe（独立的 CLI 工具）
        var cliExe = Path.Combine(baseDir, "ClaudeLight.CLI.exe");

        if (File.Exists(cliExe))
            return cliExe;

        // 如果找不到 CLI，返回空路径（会在 InstallHooks 中处理）
        return string.Empty;
    }

    public static void InstallHooks(string settingsPath, string cliExePath)
    {
        var json = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : "{}";
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        // Check if claude-light hooks are already installed
        if (root.TryGetProperty("hooks", out var hooks) &&
            hooks.GetRawText().Contains(HookMarker))
        {
            return;
        }

        var cliPath = cliExePath.Replace("\\", "\\\\");

        // Parse existing hooks (excluding claude-light hooks)
        var existingHooksJson = new Dictionary<string, string>();
        if (root.TryGetProperty("hooks", out var existingHooksProp))
        {
            foreach (var prop in existingHooksProp.EnumerateObject())
            {
                // Skip claude-light hooks
                if (prop.Value.GetRawText().Contains(HookMarker))
                    continue;
                existingHooksJson[prop.Name] = prop.Value.GetRawText();
            }
        }

        // Build final hooks JSON manually
        var hooksContent = "{";
        var first = true;

        // Add existing hooks
        foreach (var kvp in existingHooksJson)
        {
            if (!first) hooksContent += ",";
            hooksContent += $"\"{kvp.Key}\":{kvp.Value}";
            first = false;
        }

        // Add claude-light hooks
        if (!first) hooksContent += ",";
        hooksContent += $@"
  ""PreToolUse"": [{{ ""hooks"": [{{ ""type"": ""command"", ""command"": ""\""{cliPath}\"" hook running $CLAUDE_PROJECT_DIR"" }}] }}],
  ""PostToolUse"": [{{ ""hooks"": [{{ ""type"": ""command"", ""command"": ""\""{cliPath}\"" hook done $CLAUDE_PROJECT_DIR"" }}] }}],
  ""Notification"": [{{ ""hooks"": [{{ ""type"": ""command"", ""command"": ""\""{cliPath}\"" hook confirm $CLAUDE_PROJECT_DIR"" }}] }}]
}}";

        // Rebuild the entire settings object
        var result = new Dictionary<string, string>();
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == "hooks")
                continue; // Skip old hooks, will add merged version
            result[prop.Name] = prop.Value.GetRawText();
        }
        result["hooks"] = hooksContent;

        // Build final JSON
        var finalJson = "{";
        var firstProp = true;
        foreach (var kvp in result)
        {
            if (!firstProp) finalJson += ",";
            finalJson += $"\"{kvp.Key}\":{kvp.Value}";
            firstProp = false;
        }
        finalJson += "}";

        // Re-parse and re-serialize for proper formatting
        var finalDoc = JsonDocument.Parse(finalJson);
        var merged = JsonSerializer.Serialize(finalDoc, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, merged);
    }

    public static void RemoveHooks(string settingsPath)
    {
        if (!File.Exists(settingsPath)) return;

        var json = File.ReadAllText(settingsPath);
        if (!json.Contains(HookMarker)) return;

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement.Clone();

        if (!root.TryGetProperty("hooks", out var hooksProp))
            return;

        // Keep only non-claude-light hooks
        var remainingHooks = new Dictionary<string, string>();
        foreach (var prop in hooksProp.EnumerateObject())
        {
            // Skip claude-light hooks
            if (prop.Value.GetRawText().Contains(HookMarker))
                continue;
            remainingHooks[prop.Name] = prop.Value.GetRawText();
        }

        // Rebuild settings without claude-light hooks
        var result = new Dictionary<string, string>();
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name == "hooks")
                continue;
            result[prop.Name] = prop.Value.GetRawText();
        }

        // Add remaining hooks if any
        if (remainingHooks.Count > 0)
        {
            var hooksContent = "{";
            var first = true;
            foreach (var kvp in remainingHooks)
            {
                if (!first) hooksContent += ",";
                hooksContent += $"\"{kvp.Key}\":{kvp.Value}";
                first = false;
            }
            hooksContent += "}";
            result["hooks"] = hooksContent;
        }

        // Build final JSON
        var finalJson = "{";
        var firstProp = true;
        foreach (var kvp in result)
        {
            if (!firstProp) finalJson += ",";
            finalJson += $"\"{kvp.Key}\":{kvp.Value}";
            firstProp = false;
        }
        finalJson += "}";

        // Re-parse and re-serialize for proper formatting
        var finalDoc = JsonDocument.Parse(finalJson);
        var cleaned = JsonSerializer.Serialize(finalDoc, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(settingsPath, cleaned);
    }
}
