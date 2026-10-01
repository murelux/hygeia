using Hygeia.Hardware;

namespace Hygeia;

public enum FanMode
{
    Auto,
    Manual
}

public sealed class FanSample
{
    public required FanBoard Fans { get; init; }
    public int? CpuShownC { get; init; }
    public bool CpuIsCore { get; init; }
    public int? GpuShownC { get; init; }
    public bool GpuIsCore { get; init; }
    public required string Status { get; init; }
    public bool Emergency { get; init; }
    public FanMode Mode { get; init; }
}

public sealed class FanSession : IDisposable
{
    const int EmergencyC = 95;
    const int EmergencyReleaseC = 90;

    readonly ClevoBridge _bridge;
    readonly TemperatureReader _temps = new();
    readonly CancellationTokenSource _cancel = new();
    readonly Task _loop;
    readonly object _pointsGate = new();
    FanPoint[] _cpuPoints;
    FanPoint[] _gpuPoints;
    int _manualCpu;
    int _manualGpu;
    int _mode;
    bool _emergencyLatched;
    bool _handedToFirmware;
    string _status = UiText.Reading;

    public FanSession(ClevoBridge bridge, FanSettings settings)
    {
        _bridge = bridge;
        _manualCpu = Math.Clamp(settings.ManualCpu, 0, 100);
        _manualGpu = Math.Clamp(settings.ManualGpu, 0, 100);
        _mode = (int)settings.ReadMode();
        _cpuPoints = FanCurves.Normalize(settings.CpuPoints).ToArray();
        _gpuPoints = FanCurves.Normalize(settings.GpuPoints).ToArray();
        FanSafety.Attach(bridge);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                _bridge.SetAuto();
            }
            catch
            {
                // The process is already leaving.
            }
        };
        _loop = Task.Run(Loop);
    }

    public FanMode Mode
    {
        get => (FanMode)Volatile.Read(ref _mode);
        set => Volatile.Write(ref _mode, (int)value);
    }

    public void SetManualTargets(int cpuPercent, int gpuPercent)
    {
        Volatile.Write(ref _manualCpu, Math.Clamp(cpuPercent, 0, 100));
        Volatile.Write(ref _manualGpu, Math.Clamp(gpuPercent, 0, 100));
    }

    public void SetCurves(IReadOnlyList<FanPoint> cpu, IReadOnlyList<FanPoint> gpu)
    {
        lock (_pointsGate)
        {
            _cpuPoints = FanCurves.Normalize(cpu).ToArray();
            _gpuPoints = FanCurves.Normalize(gpu).ToArray();
        }
    }

    public event Action<FanSample>? Updated;

    async Task Loop()
    {
        while (!_cancel.IsCancellationRequested)
        {
            FanSample? sample = null;
            try
            {
                var fans = _bridge.Read();
                var cpuCore = _temps.ReadCpuCore();
                var gpuCore = _temps.ReadGpuCore();
                var cpuShown = ControlTemp(cpuCore, fans.Cpu);
                var gpuShown = ControlTemp(gpuCore, fans.Gpu);
                var mode = Mode;
                var emergency = DecideEmergency(cpuShown, gpuShown, fans);
                var status = UiText.Following;

                if (emergency)
                {
                    _bridge.SetDuty(100, 100);
                    _handedToFirmware = false;
                    status = UiText.Emergency;
                }
                else if (mode == FanMode.Manual)
                {
                    var cpuDuty = Volatile.Read(ref _manualCpu);
                    var gpuDuty = Volatile.Read(ref _manualGpu);
                    _bridge.SetDuty(cpuDuty, gpuDuty);
                    _handedToFirmware = false;
                    status = UiText.Manual(cpuDuty, gpuDuty);
                }
                else if (cpuShown is null && gpuShown is null)
                {
                    if (!_handedToFirmware)
                    {
                        _bridge.SetAuto();
                        _handedToFirmware = true;
                    }

                    status = UiText.NoTemp;
                }
                else
                {
                    FanPoint[] cpuPoints;
                    FanPoint[] gpuPoints;
                    lock (_pointsGate)
                    {
                        cpuPoints = _cpuPoints.ToArray();
                        gpuPoints = _gpuPoints.ToArray();
                    }

                    var cpuDuty = cpuShown is null ? 40 : FanCurves.Interpolate(cpuShown.Value, cpuPoints);
                    var gpuDuty = gpuShown is null ? cpuDuty : FanCurves.Interpolate(gpuShown.Value, gpuPoints);
                    _bridge.SetDuty(cpuDuty, gpuDuty);
                    _handedToFirmware = false;
                    status = UiText.Auto(cpuDuty, gpuDuty);
                }

                _status = status;
                sample = new FanSample
                {
                    Fans = fans,
                    CpuShownC = cpuShown,
                    CpuIsCore = cpuCore is not null && cpuShown == cpuCore,
                    GpuShownC = gpuShown,
                    GpuIsCore = gpuCore is not null && gpuShown == gpuCore,
                    Status = status,
                    Emergency = emergency,
                    Mode = mode
                };
            }
            catch (Exception ex)
            {
                try
                {
                    _bridge.SetAuto();
                    _handedToFirmware = true;
                }
                catch
                {
                    // Keep the original error visible.
                }

                _status = UiText.ReadFailed(ex.Message);
            }

            if (sample is null)
            {
                try
                {
                    sample = new FanSample
                    {
                        Fans = _bridge.Read(),
                        CpuShownC = null,
                        CpuIsCore = false,
                        GpuShownC = null,
                        GpuIsCore = false,
                        Status = _status,
                        Emergency = false,
                        Mode = Mode
                    };
                }
                catch
                {
                    // UI keeps the previous numbers.
                }
            }

            if (sample is not null)
            {
                Updated?.Invoke(sample);
            }

            try
            {
                await Task.Delay(1000, _cancel.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    static int? ControlTemp(int? core, FanReading fan)
    {
        if (core is >= 10 and <= 125)
        {
            return core;
        }

        return fan.Present && fan.HeatsinkC is >= 10 and <= 125 ? fan.HeatsinkC : null;
    }

    bool DecideEmergency(int? cpu, int? gpu, FanBoard fans)
    {
        var hottest = new int?[] { cpu, gpu, fans.Cpu.HeatsinkC, fans.Gpu.HeatsinkC }.Max();
        if (hottest >= EmergencyC)
        {
            _emergencyLatched = true;
        }
        else if (hottest <= EmergencyReleaseC)
        {
            _emergencyLatched = false;
        }

        return _emergencyLatched;
    }

    public void Dispose()
    {
        _cancel.Cancel();
        try
        {
            _loop.Wait(TimeSpan.FromSeconds(3));
        }
        catch
        {
            // The loop is ending with the process.
        }

        _temps.Dispose();
        _bridge.Dispose();
        _cancel.Dispose();
    }
}