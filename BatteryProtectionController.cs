namespace HonorPCHelper;

internal enum BatteryProtectionMode
{
    Disabled,
    Home,
    Office,
    Travel
}

internal sealed class BatteryProtectionController
{
    private const ulong SetThresholdsCommand = 0x00001003;

    internal void SetMode(BatteryProtectionMode mode)
    {
        var thresholds = mode switch
        {
            BatteryProtectionMode.Home => (Start: 40, End: 70),
            BatteryProtectionMode.Office => (Start: 70, End: 90),
            BatteryProtectionMode.Travel => (Start: 95, End: 100),
            BatteryProtectionMode.Disabled => (Start: 0, End: 100),
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };

        if (mode == BatteryProtectionMode.Disabled)
        {
            SetThresholds(0, 0);
            Thread.Sleep(1000);
        }

        SetThresholds(thresholds.Start, thresholds.End);
    }

    private static void SetThresholds(int start, int end)
    {
        var command = SetThresholdsCommand | ((ulong)start << 16) | ((ulong)end << 24);
        HonorWmi.Call(command);
    }
}
