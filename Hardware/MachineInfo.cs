using System.Management;
using Microsoft.Win32;

namespace Hygeia.Hardware;

public sealed class DeviceDriver
{
    public required string Category { get; init; }
    public required string Name { get; init; }
    public string? Version { get; init; }
}

public sealed class MachineDetails
{
    public string? DeviceName { get; init; }
    public string? Manufacturer { get; init; }
    public string? Model { get; init; }
    public string? Computer { get; init; }
    public string? WindowsName { get; init; }
    public string? DisplayVersion { get; init; }
    public string? Build { get; init; }
    public bool Is64Bit { get; init; }
    public string? Processor { get; init; }
    public string? ProcessorSpeed { get; init; }
    public string? Memory { get; init; }
    public string? Graphics { get; init; }
    public string? GraphicsDriver { get; init; }
    public string? StorageTotal { get; init; }
    public string? StorageUsed { get; init; }
    public string? Bios { get; init; }
    public bool FanDriverKnown { get; init; }
    public bool? FanDriverRunning { get; init; }
    public string? FanDriverVersion { get; init; }
    public bool? FanDevicePresent { get; init; }
    public IReadOnlyList<DeviceDriver> Drivers { get; init; } = [];

    public static MachineDetails Read()
    {
        var (deviceName, manufacturer, model) = ReadComputer();
        var (processor, speed) = ReadProcessor();
        var (graphics, graphicsDriver) = ReadGraphics();
        var (storageTotal, storageUsed) = ReadStorage();
        return new MachineDetails
        {
            DeviceName = deviceName,
            Manufacturer = manufacturer,
            Model = model,
            Computer = Join(manufacturer, model),
            WindowsName = ReadWindowsName(out var display, out var build),
            DisplayVersion = display,
            Build = build,
            Is64Bit = Environment.Is64BitOperatingSystem,
            Processor = processor,
            ProcessorSpeed = speed,
            Memory = ReadMemory(),
            Graphics = graphics,
            GraphicsDriver = graphicsDriver,
            StorageTotal = storageTotal,
            StorageUsed = storageUsed,
            Bios = ReadBios(),
            FanDriverKnown = ReadFanDriver(out var running, out var version),
            FanDriverRunning = running,
            FanDriverVersion = version,
            FanDevicePresent = ReadFanDevice(),
            Drivers = ReadDrivers()
        };
    }

    static string? Join(string? left, string? right)
    {
        var text = string.Join(" ", new[] { left, right }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    static (string? DeviceName, string? Manufacturer, string? Model) ReadComputer()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, Manufacturer, Model FROM Win32_ComputerSystem");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    return (Clean(item["Name"]), Clean(item["Manufacturer"]), Clean(item["Model"]));
                }
            }
        }
        catch
        {
            return (null, null, null);
        }

        return (null, null, null);
    }

    static string? ReadWindowsName(out string? displayVersion, out string? build)
    {
        displayVersion = null;
        build = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var name = key?.GetValue("ProductName") as string;
            displayVersion = Clean(key?.GetValue("DisplayVersion"));
            build = Clean(key?.GetValue("CurrentBuild"));
            var buildNumber = build;
            if (build is not null && key?.GetValue("UBR") is int ubr)
            {
                build = $"{build}.{ubr}";
            }
            if (int.TryParse(buildNumber, out var number) && number >= 22000 && name is not null)
            {
                name = name.Replace("Windows 10", "Windows 11", StringComparison.Ordinal);
            }

            return Clean(name);
        }
        catch
        {
            return null;
        }
    }

    static (string? Name, string? Speed) ReadProcessor()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, MaxClockSpeed FROM Win32_Processor");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    string? speed = null;
                    if (item["MaxClockSpeed"] is not null && uint.TryParse(item["MaxClockSpeed"].ToString(), out var mhz) && mhz > 0)
                    {
                        speed = $"{mhz / 1000d:0.00} GHz";
                    }

                    return (Clean(item["Name"]), speed);
                }
            }
        }
        catch
        {
            return (null, null);
        }

        return (null, null);
    }

    static string? ReadMemory()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory");
            using var results = searcher.Get();
            ulong total = 0;
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    if (ulong.TryParse(item["Capacity"]?.ToString(), out var capacity))
                    {
                        total += capacity;
                    }
                }
            }

            return total == 0 ? null : FormatBytes(total);
        }
        catch
        {
            return null;
        }
    }

    static (string? Name, string? Driver) ReadGraphics()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            using var results = searcher.Get();
            string? name = null;
            string? driver = null;
            ulong bestRam = 0;
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var current = Clean(item["Name"]);
                    if (string.IsNullOrWhiteSpace(current))
                    {
                        continue;
                    }

                    ulong ram = 0;
                    ulong.TryParse(item["AdapterRAM"]?.ToString(), out ram);
                    if (name is null || ram >= bestRam)
                    {
                        name = current;
                        driver = Clean(item["DriverVersion"]);
                        bestRam = ram;
                    }
                }
            }

            return (name, driver);
        }
        catch
        {
            return (null, null);
        }
    }

    static (string? Total, string? Used) ReadStorage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3");
            using var results = searcher.Get();
            ulong size = 0;
            ulong free = 0;
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    if (ulong.TryParse(item["Size"]?.ToString(), out var disk))
                    {
                        size += disk;
                    }

                    if (ulong.TryParse(item["FreeSpace"]?.ToString(), out var space))
                    {
                        free += space;
                    }
                }
            }

            if (size == 0)
            {
                return (null, null);
            }

            var used = free < size ? size - free : 0;
            return (FormatBytes(size), used == 0 ? null : FormatBytes(used));
        }
        catch
        {
            return (null, null);
        }
    }

    static string FormatBytes(ulong bytes)
    {
        var tb = bytes / 1099511627776d;
        if (tb >= 1)
        {
            return $"{tb:0.00} TB";
        }

        return $"{bytes / 1073741824d:0.0} GB";
    }

    static string? ReadBios()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    return Clean(item["SMBIOSBIOSVersion"]);
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    static IReadOnlyList<DeviceDriver> ReadDrivers()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceName, DriverVersion, DeviceClass, DeviceID FROM Win32_PnPSignedDriver WHERE DeviceClass='NET' OR DeviceClass='MEDIA' OR DeviceClass='BLUETOOTH'");
            using var results = searcher.Get();
            var found = new List<DeviceDriver>();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var deviceClass = Clean(item["DeviceClass"]);
                    var name = Clean(item["DeviceName"]);
                    var deviceId = item["DeviceID"]?.ToString();
                    if (!IncludeDriver(deviceClass, name, deviceId))
                    {
                        continue;
                    }

                    found.Add(new DeviceDriver
                    {
                        Category = deviceClass switch
                        {
                            "MEDIA" => "audio",
                            "BLUETOOTH" => "bluetooth",
                            _ => "net"
                        },
                        Name = name!,
                        Version = Clean(item["DriverVersion"])
                    });
                }
            }

            return found
                .OrderBy(driver => driver.Category switch
                {
                    "net" => 0,
                    "audio" => 1,
                    _ => 2
                })
                .ThenBy(driver => driver.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    static bool IncludeDriver(string? deviceClass, string? name, string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return deviceClass switch
        {
            "NET" => deviceId?.StartsWith(@"PCI\", StringComparison.OrdinalIgnoreCase) == true,
            "MEDIA" => !name.Contains("Streaming Service Proxy", StringComparison.OrdinalIgnoreCase),
            "BLUETOOTH" => true,
            _ => false
        };
    }

    static bool ReadFanDriver(out bool? running, out string? version)
    {
        running = null;
        version = null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT State, PathName FROM Win32_SystemDriver WHERE Name='AcpiBridge'");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var state = Clean(item["State"]);
                    running = string.Equals(state, "Running", StringComparison.OrdinalIgnoreCase);
                    version = FileVersion(item["PathName"]?.ToString());
                    return true;
                }
            }

            running = null;
            return true;
        }
        catch
        {
            return false;
        }
    }

    static bool? ReadFanDevice()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\ACPI\CLV0001");
            return key?.GetSubKeyNames().Length > 0;
        }
        catch
        {
            return null;
        }
    }

    static string? FileVersion(string? path)
    {
        var expanded = ExpandDriverPath(path);
        if (expanded is null || !File.Exists(expanded))
        {
            return null;
        }

        try
        {
            return Clean(System.Diagnostics.FileVersionInfo.GetVersionInfo(expanded).FileVersion);
        }
        catch
        {
            return null;
        }
    }

    static string? ExpandDriverPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        path = path.Trim().Trim('"');
        if (path.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            path = path[4..];
        }

        var root = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrEmpty(root))
        {
            path = path.Replace(@"\SystemRoot\", root + @"\", StringComparison.OrdinalIgnoreCase);
            if (path.StartsWith(@"System32\", StringComparison.OrdinalIgnoreCase))
            {
                path = Path.Combine(root, path);
            }
        }

        return path;
    }

    static string? Clean(object? value)
    {
        var text = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
