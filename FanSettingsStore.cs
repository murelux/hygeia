using System.Text.Json;

namespace Hygeia;

public sealed class FanSettings
{
    public string Mode { get; set; } = nameof(FanMode.Auto);
    public int ManualCpu { get; set; } = 40;
    public int ManualGpu { get; set; } = 40;
    public string Language { get; set; } = "zh-CN";
    public List<FanPoint> CpuPoints { get; set; } = FanCurves.Default();
    public List<FanPoint> GpuPoints { get; set; } = FanCurves.Default();

    public FanMode ReadMode() =>
        Enum.TryParse<FanMode>(Mode, out var mode) && mode != FanMode.Manual
            ? FanMode.Auto
            : mode == FanMode.Manual ? FanMode.Manual : FanMode.Auto;
}

public static class FanSettingsStore
{
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "data", "settings.json");

    public static FanSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                UiText.Set("zh-CN");
                return new FanSettings();
            }

            var settings = JsonSerializer.Deserialize<FanSettings>(File.ReadAllText(FilePath), Json) ?? new FanSettings();
            settings.ManualCpu = Math.Clamp(settings.ManualCpu, 0, 100);
            settings.ManualGpu = Math.Clamp(settings.ManualGpu, 0, 100);
            settings.CpuPoints = FanCurves.Normalize(settings.CpuPoints ?? FanCurves.Default());
            settings.GpuPoints = FanCurves.Normalize(settings.GpuPoints ?? FanCurves.Default());
            UiText.Set(settings.Language);
            settings.Language = UiText.Code;
            settings.Mode = settings.ReadMode().ToString();
            return settings;
        }
        catch
        {
            UiText.Set("zh-CN");
            return new FanSettings();
        }
    }

    public static void Save(FanSettings settings)
    {
        var directory = Path.GetDirectoryName(FilePath)!;
        Directory.CreateDirectory(directory);
        settings.Language = UiText.Code;
        settings.CpuPoints = FanCurves.Normalize(settings.CpuPoints);
        settings.GpuPoints = FanCurves.Normalize(settings.GpuPoints);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        if (File.Exists(FilePath))
        {
            File.Replace(temp, FilePath, null);
        }
        else
        {
            File.Move(temp, FilePath);
        }
    }
}
