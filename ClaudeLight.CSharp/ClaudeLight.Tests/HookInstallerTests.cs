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

        HookInstaller.InstallHooks(_testSettingsPath, "/fake/ClaudeLight.CLI.exe");

        var json = File.ReadAllText(_testSettingsPath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("hooks", out var hooks));
        Assert.True(hooks.TryGetProperty("PreToolUse", out var pre));
        Assert.True(hooks.TryGetProperty("PostToolUse", out var post));
        Assert.True(hooks.TryGetProperty("PermissionRequest", out var notif));
        Assert.True(hooks.TryGetProperty("Stop", out _));

        // PreToolUse 和 PostToolUse 必须有 matcher
        var preFirst = pre[0];
        Assert.True(preFirst.TryGetProperty("matcher", out var preMatcher));
        Assert.Equal("*", preMatcher.GetString());

        var postFirst = post[0];
        Assert.True(postFirst.TryGetProperty("matcher", out var postMatcher));
        Assert.Equal("*", postMatcher.GetString());
    }

    [Fact]
    public void InstallHooks_RegistersSessionEndHook_ToExit()
    {
        File.WriteAllText(_testSettingsPath, "{}");

        HookInstaller.InstallHooks(_testSettingsPath, "/fake/ClaudeLight.CLI.exe");

        var json = File.ReadAllText(_testSettingsPath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("hooks", out var hooks));
        Assert.True(hooks.TryGetProperty("SessionEnd", out var sessionEnd));
        var command = sessionEnd[0].GetProperty("hooks")[0].GetProperty("command").GetString();
        Assert.NotNull(command);
        Assert.Contains("hook exit", command);
    }

    [Fact]
    public void InstallHooks_DoesNotDuplicate_WhenAlreadyInstalled()
    {
        File.WriteAllText(_testSettingsPath, "{}");
        HookInstaller.InstallHooks(_testSettingsPath, "/fake/ClaudeLight.CLI.exe");
        var countBefore = File.ReadAllText(_testSettingsPath).Split("claude-light").Length;

        HookInstaller.InstallHooks(_testSettingsPath, "/fake/ClaudeLight.CLI.exe");
        var countAfter = File.ReadAllText(_testSettingsPath).Split("claude-light").Length;

        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public void RemoveHooks_ClearsHookConfig()
    {
        File.WriteAllText(_testSettingsPath, "{}");
        HookInstaller.InstallHooks(_testSettingsPath, "/fake/ClaudeLight.CLI.exe");

        HookInstaller.RemoveHooks(_testSettingsPath);

        var json = File.ReadAllText(_testSettingsPath);
        var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("hooks", out _));
    }
}
