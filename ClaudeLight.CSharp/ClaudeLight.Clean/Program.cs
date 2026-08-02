using System;
using System.IO;
using System.Text.Json;
using ClaudeLight.Services;

namespace ClaudeLight.Clean;

public class Program
{
    public static int Main(string[] args)
    {
        bool force = args.Contains("--force");

        if (!force)
        {
            Console.Write("This will remove all Claude Light hooks and data. Continue? (y/N): ");
            var input = Console.ReadLine();
            if (input?.ToLower() != "y")
            {
                Console.WriteLine("Cancelled.");
                return 0;
            }
        }

        // 1. Remove hooks from settings.json
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude", "settings.json");

        if (File.Exists(settingsPath))
        {
            HookInstaller.RemoveHooks(settingsPath);
            Console.WriteLine($"Removed hooks from {settingsPath}");
        }

        // 2. Delete state directory
        var stateDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".claude-lights");
        if (Directory.Exists(stateDir))
        {
            Directory.Delete(stateDir, true);
            Console.WriteLine($"Deleted {stateDir}");
        }

        Console.WriteLine("Claude Light has been uninstalled.");
        return 0;
    }
}
