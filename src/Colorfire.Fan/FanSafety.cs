using Hygeia.Hardware;

namespace Hygeia;

public static class FanSafety
{
    static readonly object Gate = new();
    static ClevoBridge? _bridge;

    public static void Attach(ClevoBridge bridge)
    {
        lock (Gate)
        {
            _bridge = bridge;
        }
    }

    public static void Release()
    {
        lock (Gate)
        {
            try
            {
                _bridge?.SetAuto();
            }
            catch
            {
                // Best effort when the process is already failing.
            }
        }
    }
}
