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

        // 左键：弹出项目列表菜单（再次左键或点击外部即关闭）。
        // MouseDown 记录菜单打开状态，MouseClick 决定打开/关闭，避免"关闭后立即重开"。
        _notifyIcon.MouseDown += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
                _wasOpenAtMouseDown = _menuOpen;
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button != System.Windows.Forms.MouseButtons.Left) return;

            if (_wasOpenAtMouseDown)
            {
                _wasOpenAtMouseDown = false;
                CloseLeftMenu();
            }
            else
            {
                OpenLeftMenu();
            }
        };
    }

    /// <summary>用户点击某项目的"隐藏/显示灯"（参数为 projectDir）。</summary>
    public event Action<string>? HideToggleRequested;

    /// <summary>用户点击某项目的"关闭并从列表移除"（参数为 projectDir）。</summary>
    public event Action<string>? CloseRequested;

    /// <summary>查询某项目当前是否已隐藏灯（由 App 注入，判断是否在 _hidden 集合中）。</summary>
    public Func<string, bool>? IsProjectHidden { get; set; }

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

    private System.Windows.Controls.Primitives.Popup? _leftPopup;
    private bool _menuOpen;
    private bool _wasOpenAtMouseDown;

    /// <summary>
    /// 打开左键项目列表菜单。菜单是 WPF Popup（StaysOpen=false），点击外部自动关闭。
    /// </summary>
    private void OpenLeftMenu()
    {
        if (_leftPopup == null)
        {
            _leftPopup = new System.Windows.Controls.Primitives.Popup
            {
                StaysOpen = false,
                Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                AllowsTransparency = true,
                PopupAnimation = System.Windows.Controls.Primitives.PopupAnimation.None
            };
            _leftPopup.Closed += (_, _) => _menuOpen = false;
        }

        _menuOpen = true;
        _leftPopup.Child = BuildLeftMenuContent();
        _leftPopup.IsOpen = true;
    }

    private void CloseLeftMenu()
    {
        _menuOpen = false;
        if (_leftPopup != null)
            _leftPopup.IsOpen = false;
    }

    /// <summary>
    /// 左键菜单内容：列出当前运行的项目（含已隐藏灯但保留在列表的）。
    /// 每个项目两个选项：隐藏/显示灯（项目保留在列表）、关闭并从列表移除（下次 running 才恢复）。
    /// </summary>
    private System.Windows.UIElement BuildLeftMenuContent()
    {
        var panel = new System.Windows.Controls.StackPanel { MinWidth = 220 };

        if (_windows.Count == 0)
        {
            panel.Children.Add(MenuText("暂无运行的项目"));
        }
        else
        {
            panel.Children.Add(MenuText($"运行中 {_windows.Count} 个项目", bold: true));
            panel.Children.Add(MenuSeparator());

            foreach (var w in _windows)
            {
                var name = new DirectoryInfo(w.ProjectDir).Name;
                var dir = w.ProjectDir;
                var isHidden = IsProjectHidden?.Invoke(dir) ?? false;

                panel.Children.Add(MenuText(name, bold: true));

                // 选项1：隐藏/显示灯（项目保留在列表）
                panel.Children.Add(MenuItem(isHidden ? "显示灯" : "隐藏灯",
                    () => HideToggleRequested?.Invoke(dir)));

                // 选项2：关闭并从列表移除（下次 running 才恢复）
                panel.Children.Add(MenuItem("关闭并从列表移除",
                    () => CloseRequested?.Invoke(dir)));
            }
        }

        return new System.Windows.Controls.Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1e, 0x1e, 0x1e)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55)),
            BorderThickness = new System.Windows.Thickness(1),
            CornerRadius = new System.Windows.CornerRadius(6),
            Padding = new System.Windows.Thickness(4),
            Child = new System.Windows.Controls.ScrollViewer { MaxHeight = 480, Content = panel }
        };
    }

    private static System.Windows.Controls.TextBlock MenuText(string text, bool bold = false)
    {
        return new System.Windows.Controls.TextBlock
        {
            Text = text,
            Foreground = bold ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.LightGray,
            FontWeight = bold ? System.Windows.FontWeights.Bold : System.Windows.FontWeights.Normal,
            Margin = new System.Windows.Thickness(8, 3, 4, 1),
            Padding = new System.Windows.Thickness(6, 3, 6, 3)
        };
    }

    private static System.Windows.UIElement MenuSeparator()
    {
        return new System.Windows.Controls.Border
        {
            Height = 1,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x44, 0x44, 0x44)),
            Margin = new System.Windows.Thickness(4, 2, 4, 2)
        };
    }

    private System.Windows.Controls.TextBlock MenuItem(string text, Action onClick)
    {
        var tb = new System.Windows.Controls.TextBlock
        {
            Text = text,
            Foreground = System.Windows.Media.Brushes.White,
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new System.Windows.Thickness(16, 3, 8, 3),
            Margin = new System.Windows.Thickness(8, 0, 4, 0)
        };
        tb.MouseEnter += (_, _) => tb.Background =
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2d, 0x6b, 0xc5));
        tb.MouseLeave += (_, _) => tb.Background = System.Windows.Media.Brushes.Transparent;
        tb.MouseLeftButtonDown += (_, _) =>
        {
            onClick();
            CloseLeftMenu(); // 点击选项后关闭菜单
        };
        return tb;
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
