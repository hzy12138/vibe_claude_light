using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ClaudeLight.Models;

namespace ClaudeLight;

public partial class MainWindow : Window
{
    private bool _isHorizontal;
    private Point _dragStart;
    private bool _isDragging;

    public string ProjectDir { get; set; } = "";

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        MouseDoubleClick += Window_MouseDoubleClick;
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

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _isDragging = false;
        CaptureMouse();
    }

    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
        if (IsMouseCaptured)
        {
            var pos = e.GetPosition(this);
            var dx = pos.X - _dragStart.X;
            var dy = pos.Y - _dragStart.Y;

            if (Math.Abs(dx) > 2 || Math.Abs(dy) > 2)
                _isDragging = true;

            Left += dx;
            Top += dy;
        }
    }

    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        ToggleLayout();
    }
}
