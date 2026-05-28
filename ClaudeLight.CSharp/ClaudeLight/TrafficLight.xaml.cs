using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClaudeLight.Models;

namespace ClaudeLight;

public partial class TrafficLight : UserControl
{
    private readonly DispatcherTimer _blinkTimer;
    private bool _blinkState;

    public TrafficLight()
    {
        InitializeComponent();
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += BlinkTimer_Tick;
    }

    public void SetProjectInfo(string name, string path)
    {
        ProjectName.Text = name;
        ProjectPath.Text = path;
        ProjectNameH.Text = name;
        ProjectPathH.Text = path;
        ToolTip = $"{name}\n{path}";
    }

    public void SetStatus(LightStatus? status)
    {
        _blinkTimer.Stop();

        var red = "#333";
        var yellow = "#333";
        var green = "#333";

        switch (status)
        {
            case LightStatus.Running:
                red = "#e74c3c";
                break;
            case LightStatus.Confirm:
                yellow = "#f39c12";
                _blinkTimer.Start();
                break;
            case LightStatus.Done:
                green = "#2ecc71";
                break;
        }

        ApplyColor(red, yellow, green);
    }

    public void SetLayout(bool isHorizontal)
    {
        VerticalLayout.Visibility = isHorizontal ? Visibility.Collapsed : Visibility.Visible;
        HorizontalLayout.Visibility = isHorizontal ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BlinkTimer_Tick(object? sender, EventArgs e)
    {
        _blinkState = !_blinkState;
        var yellow = _blinkState ? "#f39c12" : "#333";
        ApplyColor("#333", yellow, "#333");
    }

    private void ApplyColor(string red, string yellow, string green)
    {
        RedLight.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(red));
        YellowLight.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(yellow));
        GreenLight.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(green));
        RedLightH.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(red));
        YellowLightH.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(yellow));
        GreenLightH.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(green));
    }
}
