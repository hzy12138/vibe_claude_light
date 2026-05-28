using System;
using System.IO;
using System.Text.Json;
using ClaudeLight.Services;
using Xunit;

namespace ClaudeLight.Tests;

public class HookInstallerTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testSettingsPath;

    public HookInstallerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"claude-light-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
        _testSettingsPath = Path.Combine(_testDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void InstallHooks_CreatesHookConfig_WhenNoExistingHooks()
    {
        File.WriteAllText(_testSettingsPath, "{}");

        HookInstaller.InstallHooks(_testSettingsPath, "/fake/claude-light.exe");

        var json = File.ReadAllText(_testSettingsPath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("hooks", out var hooks));
        Assert.True(hooks.TryGetProperty("PreToolUse", out var pre));
        Assert.True(hooks.TryGetProperty("PostToolUse", out var post));
        Assert.True(hooks.TryGetProperty("Notification", out var notif));
    }

    [Fact]
    public void InstallHooks_DoesNotDuplicate_WhenAlreadyInstalled()
    {
        File.WriteAllText(_testSettingsPath, "{}");
        HookInstaller.InstallHooks(_testSettingsPath, "/fake/claude-light.exe");
        var countBefore = File.ReadAllText(_testSettingsPath).Split("claude-light").Length;

        HookInstaller.InstallHooks(_testSettingsPath, "/fake/claude-light.exe");
        var countAfter = File.ReadAllText(_testSettingsPath).Split("claude-light").Length;

        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void RemoveHooks_ClearsHookConfig()
    {
        File.WriteAllText(_testSettingsPath, "{}");
        HookInstaller.InstallHooks(_testSettingsPath, "/fake/claude-light.exe");

        HookInstaller.RemoveHooks(_testSettingsPath);

        var json = File.ReadAllText(_testSettingsPath);
        var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("hooks", out _));
    }
}
