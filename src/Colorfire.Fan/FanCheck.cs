using Hygeia.Hardware;

namespace Hygeia;

public static class FanCheck
{
    public static int Run()
    {
        var lines = new List<string>();
        var path = Path.Combine(AppContext.BaseDirectory, "verify-result.txt");
        using var bridge = ClevoBridge.Open();
        try
        {
            void Snap(string label)
            {
                var board = bridge.Read();
                lines.Add($"{label} cpu duty={board.Cpu.DutyRaw}/{board.Cpu.DutyPercent}% rpm={board.Cpu.Rpm} hs={board.Cpu.HeatsinkC} gpu duty={board.Gpu.DutyRaw}/{board.Gpu.DutyPercent}% rpm={board.Gpu.Rpm} hs={board.Gpu.HeatsinkC}");
            }

            lines.Add(bridge.DescribePackage());
            Snap("baseline");
            Hold(bridge, 25, 25, 8);
            var low = bridge.Read();
            Snap("duty25");
            Hold(bridge, 100, 100, 8);
            var high = bridge.Read();
            Snap("duty100");
            bridge.SetAuto();
            Thread.Sleep(3000);
            var restored = bridge.Read();
            Snap("auto");

            var dutyOk = Near(low.Cpu.DutyRaw, ClevoBridge.PercentToRaw(25))
                && Near(low.Gpu.DutyRaw, ClevoBridge.PercentToRaw(25))
                && Near(high.Cpu.DutyRaw, 255)
                && Near(high.Gpu.DutyRaw, 255);
            var rpmOk = high.Cpu.Rpm >= low.Cpu.Rpm + 400 || high.Gpu.Rpm >= low.Gpu.Rpm + 400;
            var autoOk = restored.Cpu.DutyRaw != 255 && restored.Gpu.DutyRaw != 255
                && Math.Abs(restored.Cpu.DutyRaw - high.Cpu.DutyRaw) > 10;
            lines.Add($"dutyOk={dutyOk} rpmOk={rpmOk} autoOk={autoOk}");
            lines.Add(dutyOk && rpmOk && autoOk ? "PASS" : "FAIL");
            File.WriteAllLines(path, lines);
            return dutyOk && rpmOk && autoOk ? 0 : 1;
        }
        catch (Exception ex)
        {
            try { bridge.SetAuto(); } catch { }
            lines.Add(ex.ToString());
            lines.Add("FAIL");
            File.WriteAllLines(path, lines);
            return 1;
        }
    }

    static void Hold(ClevoBridge bridge, int cpu, int gpu, int seconds)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until)
        {
            bridge.SetDuty(cpu, gpu);
            Thread.Sleep(1000);
        }
    }

    static bool Near(int actual, int expected) => Math.Abs(actual - expected) <= 8;

    public static int Status()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "status-result.txt");
        using var bridge = ClevoBridge.Open();
        using var temps = new TemperatureReader();
        var board = bridge.Read();
        var cpu = temps.ReadCpuCore();
        var gpu = temps.ReadGpuCore();
        File.WriteAllLines(path, new[]
        {
            "cpu " + (cpu is null ? "none" : cpu.ToString()) + " fan=" + board.Cpu.DutyPercent + "% rpm=" + board.Cpu.Rpm + " hs=" + board.Cpu.HeatsinkC,
            "gpu " + (gpu is null ? "none" : gpu.ToString()) + " fan=" + board.Gpu.DutyPercent + "% rpm=" + board.Gpu.Rpm + " hs=" + board.Gpu.HeatsinkC
        });        return 0;
    }
}