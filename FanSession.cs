using Hygeia.Hardware;

namespace Hygeia;

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
                var cpuShown = FanControl.ControlTemp(cpuCore, fans.Cpu.Present, fans.Cpu.HeatsinkC);
                var gpuShown = FanControl.ControlTemp(gpuCore, fans.Gpu.Present, fans.Gpu.HeatsinkC);
                var mode = Mode;
                var emergency = FanControl.NextEmergency(_emergencyLatched, cpuShown, gpuShown);
                _emergencyLatched = emergency;
                FanPoint[] cpuPoints;
                FanPoint[] gpuPoints;
                lock (_pointsGate)
                {
                    cpuPoints = _cpuPoints.ToArray();
                    gpuPoints = _gpuPoints.ToArray();
                }

                var manualCpu = Volatile.Read(ref _manualCpu);
                var manualGpu = Volatile.Read(ref _manualGpu);
                var command = FanControl.Choose(mode, cpuShown, gpuShown, manualCpu, manualGpu, emergency, cpuPoints, gpuPoints);
                var status = UiText.Following;
                if (command.Kind == FanWrite.Firmware)
                {
                    if (!_handedToFirmware)
                    {
                        _bridge.SetAuto();
                        _handedToFirmware = true;
                    }

                    status = UiText.NoTemp;
                }
                else if (emergency)
                {
                    _bridge.SetDuty(command.Cpu, command.Gpu);
                    _handedToFirmware = false;
                    status = UiText.Emergency;
                }
                else if (mode == FanMode.Manual)
                {
                    _bridge.SetDuty(command.Cpu, command.Gpu);
                    _handedToFirmware = false;
                    status = UiText.Manual(command.Cpu, command.Gpu);
                }
                else
                {
                    _bridge.SetDuty(command.Cpu, command.Gpu);
                    _handedToFirmware = false;
                    status = UiText.Auto(command.Cpu, command.Gpu);
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