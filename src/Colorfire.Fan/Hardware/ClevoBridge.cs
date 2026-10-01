using System.Runtime.InteropServices;

namespace Hygeia.Hardware;

public sealed class FanReading
{
    public int DutyRaw { get; init; }
    public int DutyPercent { get; init; }
    public int HeatsinkC { get; init; }
    public int Rpm { get; init; }
    public bool Present { get; init; }

    public string DutyText => $"{DutyPercent}%";
}

public sealed class FanBoard
{
    public required FanReading Cpu { get; init; }
    public required FanReading Gpu { get; init; }
}

/// <summary>
/// Insyde AcpiBridge on ACPI\CLV0001. IOCTL 0x00322400 evaluates _DSM
/// UUID 93f224e4-fbdc-4bbf-add6-db71bdc0afad. Live fan data is package 12;
/// duty writes use command 0x68 on a 0-255 scale, and 0x69 with 0xFFFFFFFF
/// returns both fans to the firmware curve.
/// </summary>
public sealed class ClevoBridge : IDisposable
{
    public const string DevicePath = @"\\?\ACPI#CLV0001#1#{86994c74-ad43-4812-b7e7-0c420b5c5fd7}";
    const uint IoctlEval = 0x00322400;
    const uint CmdPackage = 12;
    const uint CmdFanCpu = 0x63;
    const uint CmdFanGpu = 0x64;
    const uint CmdSetDuty = 0x68;
    const uint CmdSetAuto = 0x69;
    const uint OutputMagic = 0x426F6541;
    static readonly byte[] DsmUuid =
    {
        0xE4, 0x24, 0xF2, 0x93, 0xDC, 0xFB, 0xBF, 0x4B,
        0xAD, 0xD6, 0xDB, 0x71, 0xBD, 0xC0, 0xAF, 0xAD
    };

    readonly IntPtr _handle;
    readonly object _gate = new();
    bool _disposed;

    ClevoBridge(IntPtr handle) => _handle = handle;

    public static ClevoBridge Open()
    {
        var handle = CreateFile(DevicePath, 0xC0000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (handle == new IntPtr(-1))
        {
            var error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(UiText.OpenFailed(DevicePath, error));
        }

        return new ClevoBridge(handle);
    }

    public FanBoard Read()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var package = ReadPackage();
            if (package is { Length: >= 32 })
            {
                return DecodePackage(package, Legacy(CmdFanCpu), Legacy(CmdFanGpu));
            }

            return new FanBoard
            {
                Cpu = Legacy(CmdFanCpu),
                Gpu = Legacy(CmdFanGpu)
            };
        }
    }

    public string DescribePackage()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var (returned, output) = EvalRaw(CmdPackage, 0, 0x420);
            var package = ExtractBuffer(output, returned);
            var head = BitConverter.ToString(output, 0, (int)Math.Min(returned, 48));
            if (package is null)
            {
                return $"pkg12 returned={returned} head={head}";
            }

            var body = BitConverter.ToString(package, 0, Math.Min(package.Length, 40));
            return $"pkg12 len={package.Length} body={body} head={head}";
        }
    }

    public void SetDuty(int cpuPercent, int gpuPercent)
    {
        SetChannels((byte)PercentToRaw(cpuPercent), (byte)PercentToRaw(gpuPercent), 0, 0);
    }

    public void SetChannels(byte fan1, byte fan2, byte fan3, byte fan4)
    {
        var packed = (uint)(fan1 | (fan2 << 8) | (fan3 << 16) | (fan4 << 24));
        lock (_gate)
        {
            ThrowIfDisposed();
            Eval(CmdSetDuty, packed);
        }
    }

    public byte[] CopyPackage()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return ReadPackage() ?? Array.Empty<byte>();
        }
    }

    public void SetAuto()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            Eval(CmdSetAuto, 0xFFFFFFFF);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                Eval(CmdSetAuto, 0xFFFFFFFF);
            }
            catch
            {
                // Closing must still release the handle.
            }

            CloseHandle(_handle);
            _disposed = true;
        }
    }

    public static int PercentToRaw(int percent) =>
        (int)Math.Round(Math.Clamp(percent, 0, 100) * 255.0 / 100.0);

    public static int RawToPercent(int raw) =>
        (int)Math.Round(Math.Clamp(raw, 0, 255) * 100.0 / 255.0);

    public static int TachToRpm(int tach)
    {
        if (tach <= 0)
        {
            return 0;
        }

        var rpm = (int)Math.Round(2156220.0 / tach);
        return rpm is >= 100 and <= 9000 ? rpm : 0;
    }

    byte[]? ReadPackage()
    {
        var (returned, output) = EvalRaw(CmdPackage, 0, 0x420);
        return ExtractBuffer(output, returned);
    }

    static byte[]? ExtractBuffer(byte[] output, uint returned)
    {
        if (returned < 20 || BitConverter.ToUInt32(output, 0) != OutputMagic)
        {
            return null;
        }

        var type = BitConverter.ToUInt16(output, 12);
        var dataLength = BitConverter.ToUInt16(output, 14);
        if (type != 2 || dataLength < 32 || 16 + dataLength > returned)
        {
            return null;
        }

        var data = new byte[dataLength];
        Buffer.BlockCopy(output, 16, data, 0, dataLength);
        return data;
    }

    static FanBoard DecodePackage(byte[] data, FanReading cpuLegacy, FanReading gpuLegacy)
    {
        return new FanBoard
        {
            Cpu = FromPackage(data, 2, 0x10, 0x12, cpuLegacy),
            Gpu = FromPackage(data, 4, 0x13, 0x15, gpuLegacy)
        };
    }

    static FanReading FromPackage(byte[] data, int tachOffset, int dutyOffset, int tempOffset, FanReading legacy)
    {
        var tach = data.Length > tachOffset + 1 ? (data[tachOffset] << 8) | data[tachOffset + 1] : 0;
        var dutyRaw = data.Length > dutyOffset ? data[dutyOffset] : legacy.DutyRaw;
        var heatsink = data.Length > tempOffset ? data[tempOffset] : legacy.HeatsinkC;
        if (heatsink is <= 0 or > 125)
        {
            heatsink = legacy.HeatsinkC;
        }

        var rpm = TachToRpm(tach);
        return new FanReading
        {
            DutyRaw = dutyRaw,
            DutyPercent = RawToPercent(dutyRaw),
            HeatsinkC = heatsink,
            Rpm = rpm,
            Present = dutyRaw > 0 || rpm > 0
        };
    }

    FanReading Legacy(uint command)
    {
        var value = Eval(command, 0);
        var dutyRaw = (int)(value & 0xFF);
        var heatsink = (int)((value >> 8) & 0xFF);
        return new FanReading
        {
            DutyRaw = dutyRaw,
            DutyPercent = dutyRaw <= 100 ? dutyRaw : RawToPercent(dutyRaw),
            HeatsinkC = heatsink,
            Rpm = 0,
            Present = dutyRaw > 0
        };
    }

    void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ClevoBridge));
        }
    }

    uint Eval(uint function, uint argument)
    {
        var (returned, output) = EvalRaw(function, argument, 64);
        if (returned < 20 || BitConverter.ToUInt32(output, 0) != OutputMagic)
        {
            throw new InvalidOperationException(UiText.BadReply((int)function));
        }

        return BitConverter.ToUInt32(output, 16);
    }

    (uint Returned, byte[] Output) EvalRaw(uint function, uint argument, int outputSize)
    {
        var input = new byte[0x420];
        Buffer.BlockCopy(DsmUuid, 0, input, 0, 16);
        BitConverter.GetBytes(0x4D53445Fu).CopyTo(input, 0x14);
        BitConverter.GetBytes(function).CopyTo(input, 0x18);
        var package = new byte[8];
        package[2] = 4;
        BitConverter.GetBytes(argument).CopyTo(package, 4);
        BitConverter.GetBytes((ushort)8).CopyTo(input, 0x1C);
        Buffer.BlockCopy(package, 0, input, 0x1E, 8);

        var output = new byte[outputSize];
        if (!DeviceIoControl(_handle, IoctlEval, input, (uint)input.Length, output, (uint)output.Length, out var returned, IntPtr.Zero))
        {
            throw new InvalidOperationException(UiText.CommandFailed((int)function, Marshal.GetLastWin32Error()));
        }

        return (returned, output);
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern IntPtr CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32", SetLastError = true)]
    static extern bool DeviceIoControl(IntPtr device, uint code, byte[] input, uint inputSize, byte[] output, uint outputSize, out uint returned, IntPtr overlapped);

    [DllImport("kernel32", SetLastError = true)]
    static extern bool CloseHandle(IntPtr handle);
}