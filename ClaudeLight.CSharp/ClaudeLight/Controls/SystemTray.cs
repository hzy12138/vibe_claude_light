using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;

namespace ClaudeLight.Controls;

public class SystemTray : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon;
    private readonly List<MainWindow> _windows = new();
    private bool _allHidden;

    public SystemTray()
    {
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = new System.Drawing.Icon(
                Application.GetResourceStream(new Uri("pack://application:,,,/Resources/lightbulb.ico")).Stream),
            Text = "Claude Light",
            Visible = true
        };

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Toggle Layout", null, (_, _) => ToggleLayout());
        menu.Items.Add("Show/Hide All", null, (_, _) => ToggleVisibility());

        var autoStartItem = new System.Windows.Forms.ToolStripMenuItem("Auto-Start")
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
        menu.Items.Add("Exit", null, (_, _) => Exit());

        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => ToggleVisibility();
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
        _notifyIcon.Text = $"Claude Light — {_windows.Count} project(s)";
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
