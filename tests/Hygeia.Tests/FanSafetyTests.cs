using Hygeia;
using Xunit;

namespace Hygeia.Tests;

public class FanSafetyTests
{
    static IReadOnlyList<FanPoint> Points => FanCurves.Default();

    [Fact]
    public void BothTemperaturesMissingReturnsFirmwareInManualMode()
    {
        var command = FanControl.Choose(FanMode.Manual, null, null, 30, 40, emergency: true, Points, Points);
        Assert.Equal(FanWrite.Firmware, command.Kind);
    }

    [Fact]
    public void OneTemperatureMissingKeepsManualDuties()
    {
        var command = FanControl.Choose(FanMode.Manual, 50, null, 30, 40, emergency: false, Points, Points);
        Assert.Equal(FanWrite.Duty, command.Kind);
        Assert.Equal(30, command.Cpu);
        Assert.Equal(40, command.Gpu);
    }

    [Fact]
    public void OneTemperatureMissingUsesAutoFallback()
    {
        var cpuMissing = FanControl.Choose(FanMode.Auto, null, 60, 30, 40, emergency: false, Points, Points);
        Assert.Equal(40, cpuMissing.Cpu);
        Assert.Equal(FanCurves.Interpolate(60, Points), cpuMissing.Gpu);

        var gpuMissing = FanControl.Choose(FanMode.Auto, 40, null, 30, 40, emergency: false, Points, Points);
        Assert.Equal(35, gpuMissing.Cpu);
        Assert.Equal(gpuMissing.Cpu, gpuMissing.Gpu);
    }

    [Fact]
    public void EmergencyLatchesAt95AndReleasesAt90()
    {
        Assert.False(FanControl.NextEmergency(false, null, null));
        Assert.False(FanControl.NextEmergency(false, FanControl.ControlTemp(null, present: true, heatsink: 200), null));
        Assert.True(FanControl.NextEmergency(false, 95, null));
        Assert.True(FanControl.NextEmergency(true, 92, 40));
        Assert.False(FanControl.NextEmergency(true, 90, 40));
    }

    [Fact]
    public void NormalizeDropsDuplicateTempsSortsAndKeepsAtLeastTwoPoints()
    {
        var normalized = FanCurves.Normalize(
        [
            new FanPoint { Temp = 70, Duty = 10 },
            new FanPoint { Temp = 40, Duty = 20 },
            new FanPoint { Temp = 40, Duty = 30 }
        ]);
        Assert.Equal(2, normalized.Count);
        Assert.Equal(40, normalized[0].Temp);
        Assert.Equal(30, normalized[0].Duty);
        Assert.Equal(70, normalized[1].Temp);

        var fallback = FanCurves.Normalize([new FanPoint { Temp = 10, Duty = 10 }]);
        Assert.Equal(5, fallback.Count);
        Assert.Equal(40, fallback[0].Temp);
        Assert.Equal(90, fallback[^1].Temp);
    }
}
