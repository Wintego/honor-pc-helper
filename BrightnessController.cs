using System.Management;

namespace HonorPCHelper;

internal static class BrightnessController
{
    internal static void Change(int delta)
    {
        using var searcher = new ManagementObjectSearcher(
            @"root\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=True");
        var brightness = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
        if (brightness is null)
            return;

        var current = Convert.ToInt32(brightness["CurrentBrightness"]);
        var target = (byte)Math.Clamp(current + delta, 0, 100);
        using var methods = new ManagementObjectSearcher(
            @"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True");
        foreach (ManagementObject monitor in methods.Get())
        {
            using (monitor)
                monitor.InvokeMethod("WmiSetBrightness", new object[] { 0u, target });
        }
    }
}
