using System;
using System.IO;
using System.Text.Json;
using ClaudeLight.Services;
using Xunit;

namespace ClaudeLight.Tests;

public class StateManagerTests : IDisposable
{
    private readonly string _testDir;

    public StateManagerTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"claude-light-sm-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void WriteState_CreatesFile_WithCorrectContent()
    {
        var path = Path.Combine(_testDir, "test.json");
        StateManager.WriteState(path, "my-project", "E:/projects/my-project", "running", 1234);

        Assert.True(File.Exists(path));
        var json = File.ReadAllText(path);
        var doc = JsonDocument.Parse(json);
        Assert.Equal("running", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("my-project", doc.RootElement.GetProperty("project_name").GetString());
    }

    [Fact]
    public void ReadState_ReturnsNull_WhenFileNotFound()
    {
        var result = StateManager.ReadState(Path.Combine(_testDir, "nonexistent.json"));
        Assert.Null(result);
    }

    [Fact]
    public void DeleteState_RemovesFile()
    {
        var path = Path.Combine(_testDir, "to-delete.json");
        StateManager.WriteState(path, "proj", "dir", "running", 1);
        StateManager.DeleteState(path);
        Assert.False(File.Exists(path));
    }
}
