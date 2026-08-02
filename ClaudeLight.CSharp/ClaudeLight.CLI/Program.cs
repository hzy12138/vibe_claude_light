using System;
using System.Diagnostics;
using System.IO;
using ClaudeLight.Models;

namespace ClaudeLight.CLI;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 3 || args[0] != "hook")
        {
            Console.Error.WriteLine("Usage: claude-light hook <running|confirm|done|idle|exit> <project_dir>");
            return 1;
        }

        var action = args[1];
        var projectDir = args[2];

        if (action == "exit")
        {
            var exitPath = LightState.GetStateFilePath(projectDir);
            if (File.Exists(exitPath))
                File.Delete(exitPath);
            return 0;
        }

        // 状态映射：
        //   running  → PreToolUse 触发，Claude 开始工作 → 红灯
        //   confirm  → PermissionRequest 触发，等待用户确认 → 黄灯闪烁
        //   done     → PostToolUse 触发，工具执行完成，但会话还在继续 → 保持红灯
        //   idle     → Stop 触发，Claude 真正完成了一次回复 → 空闲定时器 → 绿灯
        var status = action switch
        {
            "running" => "running",
            "confirm" => "confirm",
            "done" => "running",   // 工具完成 ≠ 会话完成，保持 working 状态
            "idle" => "idle",       // Stop hook → 真的空闲了 → 定时器 → 绿灯
            _ => null
        };

        if (status == null)
        {
            Console.Error.WriteLine($"Unknown action: {action}");
            return 1;
        }

        var projectName = new DirectoryInfo(projectDir).Name;
        var state = new LightState
        {
            ProjectName = projectName,
            ProjectDir = projectDir,
            Status = status,
            Pid = Process.GetCurrentProcess().Id,
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };

        var path = LightState.GetStateFilePath(projectDir);
        state.WriteToFile(path);
        return 0;
    }
}
