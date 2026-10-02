namespace Hygeia;

public enum FanMode
{
    Auto,
    Manual
}

public enum FanWrite
{
    Firmware,
    Duty
}

public readonly record struct FanWriteCommand(FanWrite Kind, int Cpu, int Gpu);

public static class FanControl
{
    public const int EmergencyC = 95;
    public const int EmergencyReleaseC = 90;

    public static int? ControlTemp(int? core, bool present, int heatsink)
    {
        if (core is >= 10 and <= 125)
        {
            return core;
        }

        return present && heatsink is >= 10 and <= 125 ? heatsink : null;
    }

    public static bool NextEmergency(bool latched, int? cpu, int? gpu)
    {
        int? hottest = cpu;
        if (gpu is int side && (hottest is null || side > hottest))
        {
            hottest = side;
        }

        if (hottest >= EmergencyC)
        {
            return true;
        }

        if (hottest <= EmergencyReleaseC)
        {
            return false;
        }

        return latched;
    }

    public static FanWriteCommand Choose(
        FanMode mode,
        int? cpu,
        int? gpu,
        int manualCpu,
        int manualGpu,
        bool emergency,
        IReadOnlyList<FanPoint> cpuPoints,
        IReadOnlyList<FanPoint> gpuPoints)
    {
        if (cpu is null && gpu is null)
        {
            return new FanWriteCommand(FanWrite.Firmware, 0, 0);
        }

        if (emergency)
        {
            return new FanWriteCommand(FanWrite.Duty, 100, 100);
        }

        if (mode == FanMode.Manual)
        {
            return new FanWriteCommand(FanWrite.Duty, manualCpu, manualGpu);
        }

        var cpuDuty = cpu is null ? 40 : FanCurves.Interpolate(cpu.Value, cpuPoints);
        var gpuDuty = gpu is null ? cpuDuty : FanCurves.Interpolate(gpu.Value, gpuPoints);
        return new FanWriteCommand(FanWrite.Duty, cpuDuty, gpuDuty);
    }
}
