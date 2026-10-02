namespace Hygeia;

public static class UiText
{
    public static string Code { get; private set; } = "zh-CN";

    public static bool English => Code == "en";

    public static void Set(string? code) => Code = code == "en" ? "en" : "zh-CN";

    static string Pick(string chinese, string english) => English ? english : chinese;

    public static string AppTitle => "hygeia";
    public static string PageTitle => Pick("风扇与温度", "Fans and temperature");
    public static string Connecting => Pick("正在连接驱动", "Connecting to the driver");
    public static string Connected => Pick("已连接 AcpiBridge", "AcpiBridge connected");
    public static string Reading => Pick("正在读取", "Reading");
    public static string Following => Pick("正在按温度条件调速", "Applying temperature points");
    public static string Emergency => Pick("温度达到 95°C，风扇已拉到 100%", "95°C reached, fans set to 100%");
    public static string Manual(int cpu, int gpu) => English ? $"Manual: CPU {cpu}% / GPU {gpu}%" : $"手动：CPU {cpu}% / GPU {gpu}%";
    public static string NoTemp => Pick("读不到温度，已交回固件自动调速", "No temperature reading, returned to firmware control");
    public static string Auto(int cpu, int gpu) => English ? $"Auto: CPU {cpu}% / GPU {gpu}%" : $"自动：CPU {cpu}% / GPU {gpu}%";
    public static string ReadFailed(string message) => Pick("读取失败，已尝试交回自动：", "Read failed, tried to return to firmware: ") + message;
    public static string SaveFailed(string message) => Pick("设置没有保存：", "Settings were not saved: ") + message;
    public static string Speed => Pick("调速", "Speed");
    public static string AutoMode => Pick("自动", "Auto");
    public static string ManualMode => Pick("手动", "Manual");
    public static string Startup => Pick("开机时在后台运行", "Run in the background at startup");
    public static string LanguageLabel => Pick("语言", "Language");
    public static string AutoHint => Pick(
        "低于第一条用第一条的占空比，高于最后一条用最后一条。至少保留两条，相同温度只留一条。",
        "Below the first point, use the first duty. Above the last point, use the last duty. Keep at least two points, and only one point at each temperature.");
    public static string CpuFan => Pick("CPU 风扇", "CPU fan");
    public static string GpuFan => Pick("GPU 风扇", "GPU fan");
    public static string AddCpu => Pick("添加 CPU 条件", "Add CPU point");
    public static string AddGpu => Pick("添加 GPU 条件", "Add GPU point");
    public static string ManualHint => Pick(
        "拖动后约一秒写入。关闭窗口会留在托盘里继续调速。",
        "The duty is written after about a second. Closing the window leaves hygeia running in the tray.");
    public static string FanPercent(string side, int percent) => English ? $"{side} fan {percent}%" : $"{side} 风扇 {percent}%";
    public static string Footer => Pick(
        "关闭窗口会留在托盘里继续按当前模式调速。从托盘退出，或温度全部读失败时，风扇交回固件。温度达到 95°C 时两路风扇临时拉到 100%。",
        "Closing the window keeps the current mode running from the tray. Fans return to firmware when you exit from the tray, or when every temperature read fails. At 95°C both fans go to 100%.");
    public static string Show => Pick("显示", "Show");
    public static string Exit => Pick("退出", "Exit");
    public static string TempHeader => Pick("温度 °C", "Temperature °C");
    public static string DutyHeader => Pick("占空比 %", "Duty %");
    public static string Delete => Pick("删除", "Delete");
    public static string Core => Pick("核心", "Core");
    public static string Heatsink => Pick("散热器", "Heatsink");
    public static string FanMissing => Pick("未检测到这路风扇", "This fan was not detected");
    public static string FanLine(string rpm, string duty, int heatsink) => English
        ? $"Speed {rpm}    Duty {duty}    Heatsink {heatsink}°C"
        : $"转速 {rpm}    占空比 {duty}    散热器 {heatsink}°C";
    public static string EmergencyTitle => Pick("温度过高", "High temperature");
    public static string EmergencyMessage => Pick(
        "风扇已临时拉到 100%，温度回落后恢复当前模式。",
        "Fans are temporarily at 100% and return to the current mode after the temperature falls.");
    public static string OpenFailed(string path, int error) => English
        ? $"Cannot open the fan device ({path}), Win32 {error}. Check that AcpiBridge is still bound to ACPI\\CLV0001."
        : $"打不开风扇设备（{path}），Win32 {error}。请确认 AcpiBridge 仍绑定在 ACPI\\CLV0001。";
    public static string BadReply(int function) => English
        ? $"Fan command 0x{function:X2} returned data that could not be read."
        : $"风扇命令 0x{function:X2} 返回的数据无法识别。";
    public static string CommandFailed(int function, int error) => English
        ? $"Fan command 0x{function:X2} failed, Win32 {error}."
        : $"风扇命令 0x{function:X2} 失败，Win32 {error}。";
    public static string CpuSliderName => English ? "CPU fan duty" : "CPU风扇占空比";
    public static string GpuSliderName => English ? "GPU fan duty" : "GPU风扇占空比";
    public static string MachineHeader => Pick("本机", "This computer");
    public static string ProcessorLabel => Pick("处理器", "Processor");
    public static string MemoryLabel => Pick("内存", "Installed RAM");
    public static string GraphicsLabel => Pick("显卡", "Graphics card");
    public static string StorageLabel => Pick("存储", "Storage");
    public static string DeviceSection => Pick("设备信息", "Device info");
    public static string WindowsSection => Pick("Windows 信息", "Windows info");
    public static string ComputerLabel => Pick("计算机", "Computer");
    public static string DeviceNameLabel => Pick("设备名称", "Device name");
    public static string BiosLabel => Pick("BIOS", "BIOS");
    public static string FanDriverLabel => Pick("风扇驱动", "Fan driver");
    public static string DriverSection => Pick("驱动", "Drivers");

    public static string DriverCategory(string category) => category switch
    {
        "audio" => Pick("声卡", "Audio"),
        "bluetooth" => Pick("蓝牙", "Bluetooth"),
        _ => Pick("网卡", "Network")
    };
    public static string EditionLabel => Pick("版本", "Edition");
    public static string VersionLabel => Pick("版本号", "Version");
    public static string BuildLabel => Pick("内部版本", "OS build");
    public static string SystemTypeLabel => Pick("系统类型", "System type");

    public static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    public static string StorageLine(string? used, string? total)
    {
        if (string.IsNullOrWhiteSpace(total))
        {
            return "—";
        }

        if (string.IsNullOrWhiteSpace(used))
        {
            return total;
        }

        return English ? $"{used} of {total} used" : $"已用 {used} / {total}";
    }

    public static string GraphicsLine(string? name, string? driver)
    {
        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(driver))
        {
            return "—";
        }

        if (string.IsNullOrWhiteSpace(driver))
        {
            return name ?? "—";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return driver;
        }

        return English ? $"{name}, driver {driver}" : $"{name}，驱动 {driver}";
    }

    public static string SystemType(bool is64Bit) => is64Bit
        ? Pick("64 位操作系统，基于 x64 的处理器", "64-bit operating system, x64-based processor")
        : Pick("32 位操作系统", "32-bit operating system");

    public static string MachineFan(bool known, bool? running, string? version, bool? device)
    {
        if (!known)
        {
            return "—";
        }

        var service = running switch
        {
            true => Pick("AcpiBridge 正在运行", "AcpiBridge is running"),
            false => Pick("AcpiBridge 已停止", "AcpiBridge is stopped"),
            _ => Pick("AcpiBridge 未安装", "AcpiBridge is not installed")
        };
        var fileVersion = string.IsNullOrWhiteSpace(version) ? "—" : version;
        var present = device switch
        {
            true => Pick("设备 ACPI\\CLV0001 存在", "device ACPI\\CLV0001 is present"),
            false => Pick("设备 ACPI\\CLV0001 不存在", "device ACPI\\CLV0001 is missing"),
            _ => "—"
        };
        return English
            ? $"{service}, driver {fileVersion}, {present}"
            : $"{service}，驱动 {fileVersion}，{present}";
    }
}