using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ClaudeLight.Models;

namespace ClaudeLight;

public partial class MainWindow : Window
{
    private bool _isHorizontal = true; // 默认横排

    public string ProjectDir { get; set; } = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        MouseDoubleClick += (_, _) => ToggleLayout();
    }

    private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var dir = new DirectoryInfo(ProjectDir);
        TrafficLightControl.SetProjectInfo(dir.Name, ProjectDir);
        TrafficLightControl.SetLayout(_isHorizontal);
    }

    public void UpdateStatus(LightStatus? status)
    {
        TrafficLightControl.SetStatus(status);

        if (status == null)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    public void ToggleLayout()
    {
        _isHorizontal = !_isHorizontal;
        TrafficLightControl.SetLayout(_isHorizontal);

        if (_isHorizontal)
        {
            Width = 140;
            Height = 120;
        }
        else
        {
            Width = 100;
            Height = 200;
        }
    }
}
