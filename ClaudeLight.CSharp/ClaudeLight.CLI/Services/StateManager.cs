using System;
using System.IO;
using System.Text.Json;
using ClaudeLight.Models;

namespace ClaudeLight.Services;

public static class StateManager
{
    public static void WriteState(string path, string projectName, string projectDir, string status, int pid)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var state = new
        {
            project_name = projectName,
            project_dir = projectDir,
            status = status,
            pid = pid,
            updated_at = DateTime.UtcNow.ToString("o")
        };

        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static LightState? ReadState(string path)
    {
        if (!File.Exists(path)) return null;
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<LightState>(json);
    }

    public static void DeleteState(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}
