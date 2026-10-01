using System.Windows.Input;
using Hygeia.Hardware;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Hygeia;

public sealed partial class MainWindow : Window
{
    readonly DispatcherQueue _queue;
    readonly FanSettings _settings;
    readonly bool _startHidden;
    FanSession? _session;
    bool _loading = true;
    bool _allowClose;
    bool _rebuilding;

    public MainWindow(bool startHidden)
    {
        _startHidden = startHidden;
        _settings = FanSettingsStore.Load();
        InitializeComponent();
        _queue = DispatcherQueue;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(960, 860));
        AppWindow.Closing += OnAppClosing;
        ApplySettings();
        _loading = false;
        try
        {
            var bridge = ClevoBridge.Open();
            _session = new FanSession(bridge, _settings);
            _session.Updated += OnSample;
            StatusText.Text = UiText.Connected;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }

        TrayIcon.Icon = LoadTrayIcon();
        TrayIcon.LeftClickCommand = new TrayCommand(ShowFromTray);
        if (_startHidden)
        {
            RootGrid.Loaded += (_, _) => AppWindow.Hide();
        }
    }

    static System.Drawing.Icon LoadTrayIcon()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exe))
        {
            try
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (icon is not null)
                {
                    return icon;
                }
            }
            catch
            {
                // Fall back to the system application icon.
            }
        }

        return System.Drawing.SystemIcons.Application;
    }

    void ApplySettings()
    {
        CpuSlider.Value = _settings.ManualCpu;
        GpuSlider.Value = _settings.ManualGpu;
        LanguageBox.SelectedItem = UiText.English ? EnglishItem : ChineseItem;
        ApplyLanguage();
        StartupBox.IsChecked = StartupRegistration.IsEnabled();
        if (_settings.ReadMode() == FanMode.Manual)
        {
            ManualMode.IsChecked = true;
        }
        else
        {
            AutoMode.IsChecked = true;
        }

        ShowMode(_settings.ReadMode());
        RebuildPoints(cpu: true);
        RebuildPoints(cpu: false);
    }

    void ApplyLanguage()
    {
        Title = UiText.AppTitle;
        PageTitle.Text = UiText.PageTitle;
        SpeedTitle.Text = UiText.Speed;
        AutoMode.Content = UiText.AutoMode;
        ManualMode.Content = UiText.ManualMode;
        AutomationProperties.SetName(AutoMode, UiText.AutoMode);
        AutomationProperties.SetName(ManualMode, UiText.ManualMode);
        StartupBox.Content = UiText.Startup;
        LanguageLabel.Text = UiText.LanguageLabel;
        AutoHint.Text = UiText.AutoHint;
        CpuFanTitle.Text = UiText.CpuFan;
        GpuFanTitle.Text = UiText.GpuFan;
        AddCpuPointButton.Content = UiText.AddCpu;
        AddGpuPointButton.Content = UiText.AddGpu;
        ManualHint.Text = UiText.ManualHint;
        FooterText.Text = UiText.Footer;
        ShowItem.Text = UiText.Show;
        ExitItem.Text = UiText.Exit;
        EmergencyBar.Title = UiText.EmergencyTitle;
        EmergencyBar.Message = UiText.EmergencyMessage;
        CpuSliderLabel.Text = UiText.FanPercent("CPU", (int)CpuSlider.Value);
        GpuSliderLabel.Text = UiText.FanPercent("GPU", (int)GpuSlider.Value);
        AutomationProperties.SetName(CpuSlider, UiText.CpuSliderName);
        AutomationProperties.SetName(GpuSlider, UiText.GpuSliderName);
    }

    void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        UiText.Set(item.Tag as string);
        _settings.Language = UiText.Code;
        FanSettingsStore.Save(_settings);
        ApplyLanguage();
        RebuildPoints(cpu: true);
        RebuildPoints(cpu: false);
    }

    void OnSample(FanSample sample)
    {
        _queue.TryEnqueue(() =>
        {
            StatusText.Text = sample.Status;
            TrayIcon.ToolTipText = UiText.AppTitle + "  " + sample.Status;
            CpuCoreText.Text = FormatCpuHeadline(sample);
            GpuCoreText.Text = sample.GpuShownC is int gpu ? $"{gpu}°C" : "—";
            GpuSourceText.Text = sample.GpuShownC is null ? "" : sample.GpuIsCore ? UiText.Core : UiText.Heatsink;
            CpuFanText.Text = FormatFan(sample.Fans.Cpu);
            GpuFanText.Text = FormatFan(sample.Fans.Gpu);
            EmergencyBar.IsOpen = sample.Emergency;
        });
    }

    static string FormatCpuHeadline(FanSample sample)
    {
        if (sample.CpuIsCore && sample.CpuShownC is int core)
        {
            return $"{core}°C";
        }

        var heatsink = sample.Fans.Cpu.HeatsinkC;
        return heatsink is >= 10 and <= 125 ? $"{heatsink}°C" : "—";
    }

    static string FormatFan(FanReading fan)
    {
        if (!fan.Present)
        {
            return UiText.FanMissing;
        }

        var rpm = fan.Rpm > 0 ? $"{fan.Rpm} RPM" : "—";
        return UiText.FanLine(rpm, fan.DutyText, fan.HeatsinkC);
    }

    void ModeChanged(object sender, RoutedEventArgs e)
    {
        if (_loading || sender is not RadioButton { IsChecked: true })
        {
            return;
        }

        var mode = ReferenceEquals(sender, ManualMode) ? FanMode.Manual : FanMode.Auto;
        ShowMode(mode);
        if (_session is not null)
        {
            _session.Mode = mode;
        }

        _settings.Mode = mode.ToString();
        if (mode == FanMode.Manual)
        {
            PushManual();
        }

        FanSettingsStore.Save(_settings);
    }

    void ShowMode(FanMode mode)
    {
        var manual = mode == FanMode.Manual;
        ManualPanel.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        AutoPanel.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;
    }

    void SliderChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (CpuSlider is null || GpuSlider is null || CpuSliderLabel is null || GpuSliderLabel is null)
        {
            return;
        }

        var cpu = (int)CpuSlider.Value;
        var gpu = (int)GpuSlider.Value;
        CpuSliderLabel.Text = UiText.FanPercent("CPU", cpu);
        GpuSliderLabel.Text = UiText.FanPercent("GPU", gpu);
        if (_loading)
        {
            return;
        }

        _settings.ManualCpu = cpu;
        _settings.ManualGpu = gpu;
        PushManual();
        FanSettingsStore.Save(_settings);
    }

    void PushManual()
    {
        _session?.SetManualTargets((int)CpuSlider.Value, (int)GpuSlider.Value);
    }

    void AddCpuPoint(object sender, RoutedEventArgs e) => AddPoint(cpu: true);

    void AddGpuPoint(object sender, RoutedEventArgs e) => AddPoint(cpu: false);

    void AddPoint(bool cpu)
    {
        var points = CopyPoints(cpu);
        var used = points.Select(point => point.Temp).ToHashSet();
        var temp = NextFreeTemp(used, Math.Min(120, points[^1].Temp + 5));
        if (temp < 0)
        {
            return;
        }

        points.Add(new FanPoint { Temp = temp, Duty = points[^1].Duty });
        ApplyPoints(cpu, points, rebuild: true);
    }

    static int NextFreeTemp(HashSet<int> used, int preferred)
    {
        if (!used.Contains(preferred))
        {
            return preferred;
        }

        for (var candidate = preferred + 1; candidate <= 120; candidate++)
        {
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        for (var candidate = 0; candidate < preferred; candidate++)
        {
            if (!used.Contains(candidate))
            {
                return candidate;
            }
        }

        return -1;
    }

    List<FanPoint> CopyPoints(bool cpu) =>
        (cpu ? _settings.CpuPoints : _settings.GpuPoints)
        .Select(point => new FanPoint { Temp = point.Temp, Duty = point.Duty })
        .ToList();

    void RebuildPoints(bool cpu)
    {
        _rebuilding = true;
        var panel = cpu ? CpuPointsPanel : GpuPointsPanel;
        var points = cpu ? _settings.CpuPoints : _settings.GpuPoints;
        panel.Children.Clear();
        for (var i = 0; i < points.Count; i++)
        {
            panel.Children.Add(CreateRow(cpu, i, points[i], points.Count > 2));
        }

        _rebuilding = false;
    }

    FrameworkElement CreateRow(bool cpu, int index, FanPoint point, bool canDelete)
    {
        var prefix = cpu ? "CPU" : "GPU";
        var temp = new NumberBox
        {
            Header = UiText.TempHeader,
            Value = point.Temp,
            Minimum = 0,
            Maximum = 120,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 140
        };
        var duty = new NumberBox
        {
            Header = UiText.DutyHeader,
            Value = point.Duty,
            Minimum = 0,
            Maximum = 100,
            SmallChange = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 140
        };
        AutomationProperties.SetName(temp, $"{prefix}温度{index + 1}");
        AutomationProperties.SetName(duty, $"{prefix}占空比{index + 1}");
        temp.ValueChanged += (_, _) => ApplyBoxes(cpu, rebuild: false);
        duty.ValueChanged += (_, _) => ApplyBoxes(cpu, rebuild: false);
        temp.LostFocus += (_, _) => ApplyBoxes(cpu, rebuild: true);
        duty.LostFocus += (_, _) => ApplyBoxes(cpu, rebuild: true);
        var delete = new Button
        {
            Content = UiText.Delete,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsEnabled = canDelete
        };
        AutomationProperties.SetName(delete, $"{prefix}删除{index + 1}");
        delete.Click += (_, _) =>
        {
            var current = ReadBoxes(cpu ? CpuPointsPanel : GpuPointsPanel);
            var rowIndex = PanelIndex(cpu, delete);
            if (current.Count <= 2 || rowIndex < 0 || rowIndex >= current.Count)
            {
                return;
            }

            current.RemoveAt(rowIndex);
            ApplyPoints(cpu, current, rebuild: true);
        };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { temp, duty, delete }
        };
    }

    int PanelIndex(bool cpu, Button delete)
    {
        var panel = cpu ? CpuPointsPanel : GpuPointsPanel;
        for (var i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] is StackPanel row && row.Children.Contains(delete))
            {
                return i;
            }
        }

        return -1;
    }

    void ApplyBoxes(bool cpu, bool rebuild)
    {
        if (_rebuilding || _loading)
        {
            return;
        }

        ApplyPoints(cpu, ReadBoxes(cpu ? CpuPointsPanel : GpuPointsPanel), rebuild);
    }

    static List<FanPoint> ReadBoxes(StackPanel panel)
    {
        var points = new List<FanPoint>();
        foreach (var child in panel.Children)
        {
            if (child is not StackPanel row || row.Children.Count < 2)
            {
                continue;
            }

            var temp = (NumberBox)row.Children[0];
            var duty = (NumberBox)row.Children[1];
            if (double.IsNaN(temp.Value) || double.IsNaN(duty.Value))
            {
                continue;
            }

            points.Add(new FanPoint { Temp = (int)Math.Round(temp.Value), Duty = (int)Math.Round(duty.Value) });
        }

        return points;
    }

    void ApplyPoints(bool cpu, List<FanPoint> raw, bool rebuild)
    {
        var unique = raw.Select(point => Math.Clamp(point.Temp, 0, 120)).Distinct().Count();
        if (unique < 2)
        {
            if (rebuild)
            {
                RebuildPoints(cpu);
            }

            return;
        }

        var normalized = FanCurves.Normalize(raw);
        var previous = cpu ? _settings.CpuPoints : _settings.GpuPoints;
        if (cpu)
        {
            _settings.CpuPoints = normalized;
        }
        else
        {
            _settings.GpuPoints = normalized;
        }

        _session?.SetCurves(_settings.CpuPoints, _settings.GpuPoints);
        FanSettingsStore.Save(_settings);
        if (rebuild && !RowsMatch(cpu, normalized))
        {
            RebuildPoints(cpu);
        }
    }

    bool RowsMatch(bool cpu, IReadOnlyList<FanPoint> normalized)
    {
        var panel = cpu ? CpuPointsPanel : GpuPointsPanel;
        if (panel.Children.Count != normalized.Count)
        {
            return false;
        }

        for (var i = 0; i < normalized.Count; i++)
        {
            if (panel.Children[i] is not StackPanel row || row.Children.Count < 1 || row.Children[0] is not NumberBox temp)
            {
                return false;
            }

            if (double.IsNaN(temp.Value) || (int)Math.Round(temp.Value) != normalized[i].Temp)
            {
                return false;
            }
        }

        return true;
    }
    void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        StartupRegistration.SetEnabled(StartupBox.IsChecked == true);
    }

    void OnAppClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose)
        {
            return;
        }

        args.Cancel = true;
        AppWindow.Hide();
    }

    void ShowFromTray()
    {
        _queue.TryEnqueue(() =>
        {
            AppWindow.Show();
            Activate();
        });
    }

    void ShowFromTray(object sender, RoutedEventArgs e) => ShowFromTray();

    void ExitFromTray(object sender, RoutedEventArgs e)
    {
        _allowClose = true;
        _session?.Dispose();
        _session = null;
        TrayIcon.Dispose();
        Close();
    }
}

sealed class TrayCommand : ICommand
{
    readonly Action _action;

    public TrayCommand(Action action) => _action = action;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _action();
}