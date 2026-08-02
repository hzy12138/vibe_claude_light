using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Drawing;
using Application = System.Windows.Application;

namespace ClaudeLight.Controls;

public class SystemTray : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly List<MainWindow> _windows = new();
    private bool _allHidden;

    public SystemTray()
    {
        var icon = LoadIcon();

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Claude Light",
            Visible = true
        };

        // 右键菜单
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("切换布局", null, (_, _) => ToggleLayout());
        menu.Items.Add("显示/隐藏全部", null, (_, _) => ToggleVisibility());

        var autoStartItem = new System.Windows.Forms.ToolStripMenuItem("开机自启动")
        {
            Checked = ClaudeLight.Services.AutoStartManager.IsAutoStartEnabled()
        };
        autoStartItem.Click += (_, _) =>
        {
            var current = ClaudeLight.Services.AutoStartManager.IsAutoStartEnabled();
            ClaudeLight.Services.AutoStartManager.SetAutoStart(!current);
            autoStartItem.Checked = !current;
        };
        menu.Items.Add(autoStartItem);

        menu.Items.Add("-");
        menu.Items.Add("退出", null, (_, _) => Exit());

        _notifyIcon.ContextMenuStrip = menu;

        // 左键：显示项目列表气泡
        _notifyIcon.Click += (_, _) => ShowProjectList();
        // 双击：显示/隐藏全部窗口
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility();
    }

    public void ShowStartupBalloon()
    {
        var count = _windows.Count;
        var msg = count == 0
            ? "Hook 已安装，等待 Claude Code 活动..."
            : $"已检测到 {count} 个运行中的项目";
        _notifyIcon.ShowBalloonTip(2000, "Claude Light 已启动 🚦", msg, System.Windows.Forms.ToolTipIcon.Info);
    }

    public void AddWindow(MainWindow window)
    {
        _windows.Add(window);
        UpdateTooltip();
    }

    public void RemoveWindow(MainWindow window)
    {
        _windows.Remove(window);
        UpdateTooltip();
    }

    private void ShowProjectList()
    {
        if (_windows.Count == 0) return;

        var names = _windows.Select(w => new DirectoryInfo(w.ProjectDir).Name).ToList();
        var title = $"运行中: {_windows.Count} 个项目";
        var body = string.Join("\n", names.Select(n => $"• {n}"));

        _notifyIcon.ShowBalloonTip(3000, title, body, System.Windows.Forms.ToolTipIcon.Info);
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "lightbulb.ico");
            if (!File.Exists(iconPath))
                return SystemIcons.Application;

            using var fs = new FileStream(iconPath, FileMode.Open, FileAccess.Read);
            return new System.Drawing.Icon(fs);
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private void ToggleLayout()
    {
        foreach (var w in _windows)
            w.ToggleLayout();
    }

    private void ToggleVisibility()
    {
        _allHidden = !_allHidden;
        foreach (var w in _windows)
        {
            if (_allHidden) w.Hide();
            else w.Show();
        }
    }

    private void Exit()
    {
        Application.Current.Shutdown();
    }

    private void UpdateTooltip()
    {
        if (_windows.Count == 0)
        {
            _notifyIcon.Text = "Claude Light — 无运行项目";
            return;
        }
        var names = _windows.Select(w => new DirectoryInfo(w.ProjectDir).Name).Take(3);
        var label = string.Join(", ", names);
        if (_windows.Count > 3) label += $" ...等{_windows.Count}个";
        _notifyIcon.Text = $"Claude Light — {label}";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
