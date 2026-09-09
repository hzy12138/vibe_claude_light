using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeLight.Models;

public enum LightStatus
{
    Running,
    Confirm,
    Done,
    Idle
}

public class LightState
{
    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = "";

    [JsonPropertyName("project_dir")]
    public string ProjectDir { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";

    [JsonPropertyName("pid")]
    public int Pid { get; set; }

    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; set; } = "";

    public LightStatus? GetStatus() => Status switch
    {
        "running" => LightStatus.Running,
        "confirm" => LightStatus.Confirm,
        "done" => LightStatus.Done,
        "idle" => LightStatus.Idle,
        _ => null
    };

    public static string GetStateDir()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-lights");
    }

    /// <summary>
    /// 规范化项目路径：统一小写 + 正斜杠，用于窗口字典键、隐藏集合键和去重。
    /// </summary>
    public static string NormalizeDir(string projectDir) =>
        projectDir.Replace("\\", "/").ToLowerInvariant();

    public static string GetStateFilePath(string projectDir)
    {
        var sanitized = projectDir
            .Replace("\\", "_")
            .Replace("/", "_")
            .Replace(":", "_");
        if (sanitized.Length > 100)
            sanitized = sanitized[^100..];
        return Path.Combine(GetStateDir(), $"{sanitized}.json");
    }

    public static LightState? ReadFromFile(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<LightState>(json);
    }

    public void WriteToFile(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
