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
    MachineDetails? _machine;
    bool _loading = true;
    bool _allowClose;
    string? _saveError;
    bool _rebuilding;

    public MainWindow(bool startHidden)
    {
        _startHidden = startHidden;
        _settings = FanSettingsStore.Load();
        InitializeComponent();
        _queue = DispatcherQueue;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1120, 780));
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

        _ = Task.Run(MachineDetails.Read).ContinueWith(task =>
        {
            if (!task.IsCompletedSuccessfully)
            {
                return;
            }

            _queue.TryEnqueue(() =>
            {
                _machine = task.Result;
                ApplyLanguage();
            });
        });
        TrayIcon.Icon = LoadTrayIcon();
        TrayIcon.LeftClickCommand = new TrayCommand(ShowFromTray);
        TrayIcon.RightClickCommand = new TrayCommand(OpenTrayMenu);
        ShowItem.Command = new TrayCommand(ShowFromTray);
        ExitItem.Command = new TrayCommand(ExitFromTray);
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
        Nav.SelectedItem = FansNav;
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

    void NavChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        ShowPage();
    }

    void ShowPage()
    {
        var machine = ReferenceEquals(Nav.SelectedItem, MachineNav);
        FansPage.Visibility = machine ? Visibility.Collapsed : Visibility.Visible;
        MachinePage.Visibility = machine ? Visibility.Visible : Visibility.Collapsed;
        Nav.Header = machine ? UiText.MachineHeader : UiText.PageTitle;
    }

    void ApplyLanguage()
    {
        Title = UiText.AppTitle;
        Nav.PaneTitle = UiText.AppTitle;
        FansNav.Content = UiText.PageTitle;
        MachineNav.Content = UiText.MachineHeader;
        ShowPage();
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
        CardProcessorLabel.Text = UiText.ProcessorLabel;
        CardMemoryLabel.Text = UiText.MemoryLabel;
        CardGraphicsLabel.Text = UiText.GraphicsLabel;
        CardStorageLabel.Text = UiText.StorageLabel;
        DeviceSectionTitle.Text = UiText.DeviceSection;
        WindowsSectionTitle.Text = UiText.WindowsSection;
        ComputerLabel.Text = UiText.ComputerLabel;
        DeviceNameLabel.Text = UiText.DeviceNameLabel;
        ProcessorLabel.Text = UiText.ProcessorLabel;
        MemoryLabel.Text = UiText.MemoryLabel;
        GraphicsLabel.Text = UiText.GraphicsLabel;
        StorageLabel.Text = UiText.StorageLabel;
        BiosLabel.Text = UiText.BiosLabel;
        FanDriverLabel.Text = UiText.FanDriverLabel;
        EditionLabel.Text = UiText.EditionLabel;
        VersionLabel.Text = UiText.VersionLabel;
        BuildLabel.Text = UiText.BuildLabel;
        SystemTypeLabel.Text = UiText.SystemTypeLabel;
        var machine = _machine;
        DeviceTitle.Text = UiText.Value(machine?.DeviceName);
        ModelSubtitle.Text = UiText.Value(machine?.Model);
        CardProcessor.Text = UiText.Value(machine?.Processor);
        CardProcessorSpeed.Text = machine?.ProcessorSpeed ?? "";
        CardMemory.Text = UiText.Value(machine?.Memory);
        CardGraphics.Text = UiText.Value(machine?.Graphics);
        CardStorage.Text = UiText.Value(machine?.StorageTotal);
        CardStorageUsed.Text = machine?.StorageUsed is null ? "" : UiText.StorageLine(machine.StorageUsed, machine.StorageTotal);
        ComputerValue.Text = UiText.Value(machine?.Computer);
        DeviceNameValue.Text = UiText.Value(machine?.DeviceName);
        ProcessorValue.Text = UiText.Value(machine?.Processor);
        MemoryValue.Text = UiText.Value(machine?.Memory);
        GraphicsValue.Text = UiText.GraphicsLine(machine?.Graphics, machine?.GraphicsDriver);
        StorageValue.Text = UiText.StorageLine(machine?.StorageUsed, machine?.StorageTotal);
        BiosValue.Text = UiText.Value(machine?.Bios);
        FanDriverValue.Text = UiText.MachineFan(machine?.FanDriverKnown ?? false, machine?.FanDriverRunning, machine?.FanDriverVersion, machine?.FanDevicePresent);
        DriverSectionTitle.Text = UiText.DriverSection;
        FillDrivers(machine?.Drivers);
        EditionValue.Text = UiText.Value(machine?.WindowsName);
        VersionValue.Text = UiText.Value(machine?.DisplayVersion);
        BuildValue.Text = UiText.Value(machine?.Build);
        SystemTypeValue.Text = UiText.SystemType(machine?.Is64Bit ?? Environment.Is64BitOperatingSystem);
    }

    void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || LanguageBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        UiText.Set(item.Tag as string);
        _settings.Language = UiText.Code;
        TrySave();
        ApplyLanguage();
        RebuildPoints(cpu: true);
        RebuildPoints(cpu: false);
    }

    void OnSample(FanSample sample)
    {
        _queue.TryEnqueue(() =>
        {
            StatusText.Text = _saveError ?? sample.Status;
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

        TrySave();
    }

    void FillDrivers(IReadOnlyList<Hygeia.Hardware.DeviceDriver>? drivers)
    {
        DriverRows.Children.Clear();
        if (drivers is null || drivers.Count == 0)
        {
            DriverRows.Children.Add(new TextBlock { Text = "—" });
            return;
        }

        foreach (var driver in drivers)
        {
            var row = new Grid { ColumnSpacing = 24 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            var label = new TextBlock
            {
                Text = UiText.DriverCategory(driver.Category),
                Opacity = 0.8,
                TextWrapping = TextWrapping.Wrap
            };
            var version = string.IsNullOrWhiteSpace(driver.Version) ? "—" : driver.Version;
            var value = new TextBlock
            {
                Text = $"{driver.Name}  {version}",
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(value, 1);
            row.Children.Add(label);
            row.Children.Add(value);
            DriverRows.Children.Add(row);
        }
    }

    void TrySave()
    {
        try
        {
            FanSettingsStore.Save(_settings);
            _saveError = null;
        }
        catch (Exception ex)
        {
            _saveError = UiText.SaveFailed(ex.Message);
            StatusText.Text = _saveError;
        }
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
        TrySave();
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
        TrySave();
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

    void OpenTrayMenu()
    {
        if (!GetCursorPos(out var point))
        {
            return;
        }

        TrayIcon.ShowContextMenu(point);
    }

    void ExitFromTray()
    {
        _allowClose = true;
        _session?.Dispose();
        _session = null;
        TrayIcon.Dispose();
        Close();
    }

    void ExitFromTray(object sender, RoutedEventArgs e) => ExitFromTray();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool GetCursorPos(out System.Drawing.Point point);
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