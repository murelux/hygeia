using System.Diagnostics;
using LibreHardwareMonitor.Hardware;

namespace Hygeia.Hardware;

public sealed class TemperatureReader : IDisposable
{
    Computer? _computer;
    bool _cpuUnavailable;

    public int? ReadGpuCore()
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=temperature.gpu --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(info);
            if (process is null)
            {
                return null;
            }

            var text = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);
            var line = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            return int.TryParse(line, out var value) && value is >= 10 and <= 125 ? value : null;
        }
        catch
        {
            return null;
        }
    }

    public int? ReadCpuCore()
    {
        if (_cpuUnavailable)
        {
            return null;
        }

        try
        {
            _computer ??= OpenComputer();
            if (_computer is null)
            {
                _cpuUnavailable = true;
                return null;
            }

            float? best = null;
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
                foreach (var sub in hardware.SubHardware)
                {
                    sub.Update();
                    Consider(sub.Sensors, ref best);
                }

                Consider(hardware.Sensors, ref best);
            }

            return best is null ? null : (int)Math.Round(best.Value);
        }
        catch
        {
            _cpuUnavailable = true;
            _computer?.Close();
            _computer = null;
            return null;
        }
    }

    static Computer? OpenComputer()
    {
        var computer = new Computer
        {
            IsCpuEnabled = true
        };
        computer.Open();
        return computer;
    }

    static void Consider(IEnumerable<ISensor> sensors, ref float? best)
    {
        foreach (var sensor in sensors)
        {
            if (sensor.SensorType != SensorType.Temperature || sensor.Value is null)
            {
                continue;
            }

            var name = sensor.Name ?? "";
            var core = name.Contains("Core", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Tctl", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Tdie", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Package", StringComparison.OrdinalIgnoreCase);
            if (!core)
            {
                continue;
            }

            var value = sensor.Value.Value;
            if (float.IsNaN(value) || value is < 10 or > 125)
            {
                continue;
            }

            best = best is null ? value : Math.Max(best.Value, value);
        }
    }

    public void Dispose()
    {
        try
        {
            _computer?.Close();
        }
        catch
        {
            // Sensor shutdown should not block fan release.
        }

        _computer = null;
    }
}
