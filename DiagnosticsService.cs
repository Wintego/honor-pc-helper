using System.Management;

namespace HonorPCHelper;

internal static class DiagnosticsService
{
    private static long _powerCacheTicks;
    private static double? _powerCache;
    internal static string BuildCompactToolTip()
    {
        var state = HardwareSettings.ReadTooltipState();
        var hasHardwareState = HardwareSensorSnapshot.TryParse(
            state.SensorSnapshot, out var hardwareState) && hardwareState.IsFresh;
        var mode = state.PerformanceModeActive
            ? L.T("производительный", "performance")
            : L.T("умный", "smart");
        var backlightLevel = hasHardwareState
            ? hardwareState.KeyboardBacklightMode switch
            {
                0x02 => KeyboardBacklightLevel.Off,
                0x03 => KeyboardBacklightLevel.Low,
                0x04 => KeyboardBacklightLevel.High,
                _ => state.KeyboardBacklight
            }
            : state.KeyboardBacklight;
        var backlight = backlightLevel switch
        {
            KeyboardBacklightLevel.Off => L.T("выкл.", "off"),
            KeyboardBacklightLevel.Low => L.T("слабая", "weak"),
            KeyboardBacklightLevel.High => L.T("сильная", "strong"),
            _ => "?"
        };
        var protection = hasHardwareState && hardwareState.ChargeStart.HasValue && hardwareState.ChargeEnd.HasValue
            ? $"{hardwareState.ChargeStart}–{hardwareState.ChargeEnd}%"
            : state.BatteryProtection switch
            {
                BatteryProtectionMode.Home => "40–70%",
                BatteryProtectionMode.Office => "70–90%",
                BatteryProtectionMode.Travel => "95–100%",
                BatteryProtectionMode.Disabled => L.T("выкл.", "off"),
                _ => "?"
            };
        var lines = new List<string>
        {
            L.T($"Режим: {mode}", $"Mode: {mode}"),
            L.T($"Подсветка: {backlight}", $"Backlight: {backlight}"),
            L.T($"Ограничение заряда: {protection}", $"Charge limit: {protection}")
        };

        var power = ReadBatteryPowerWatts();
        if (power.HasValue)
            lines.Add(L.T($"Питание: {FormatPower(power.Value)}", $"Power: {FormatPower(power.Value)}"));

        if (hasHardwareState)
        {
            if (hardwareState.CpuTemperature.HasValue || hardwareState.BatteryTemperature.HasValue)
                lines.Add(L.T(
                    $"CPU: {FormatTemperature(hardwareState.CpuTemperature)}; батарея: {FormatTemperature(hardwareState.BatteryTemperature)}",
                    $"CPU: {FormatTemperature(hardwareState.CpuTemperature)}; battery: {FormatTemperature(hardwareState.BatteryTemperature)}"));
            if (hardwareState.Fan1Rpm.HasValue || hardwareState.Fan2Rpm.HasValue)
                lines.Add(L.T(
                    $"Вентиляторы: {FormatFan(hardwareState.Fan1Rpm)}/{FormatFan(hardwareState.Fan2Rpm)} об/мин",
                    $"Fans: {FormatFan(hardwareState.Fan1Rpm)}/{FormatFan(hardwareState.Fan2Rpm)} RPM"));
        }

        var text = string.Join(Environment.NewLine, lines);
        return text.Length <= 127 ? text : text[..127];
    }

    private static string FormatTemperature(int? value) => value.HasValue ? $"{value}°C" : "?";

    private static string FormatFan(int? value) => value?.ToString() ?? "?";

    private static string FormatPower(double watts)
    {
        if (Math.Abs(watts) < 0.05)
            return L.T("0 Вт", "0 W");
        var sign = watts > 0 ? "+" : "";
        return L.T($"{sign}{watts:0.0} Вт", $"{sign}{watts:0.0} W");
    }

    // Charge/discharge power in watts: positive while charging, negative while
    // discharging. Read from the standard root\wmi BatteryStatus class, which is
    // available without administrator rights, and cached briefly so repeated
    // tooltip refreshes during a hover don't re-query WMI each time.
    private static double? ReadBatteryPowerWatts()
    {
        if (_powerCache.HasValue
            && Environment.TickCount64 - Interlocked.Read(ref _powerCacheTicks) < 2000)
            return _powerCache;

        double? result = null;
        try
        {
            var scope = new ManagementScope(@"\\.\root\wmi");
            scope.Connect();
            using var searcher = new ManagementObjectSearcher(
                scope, new ObjectQuery("SELECT ChargeRate, DischargeRate FROM BatteryStatus"));
            using var items = searcher.Get();
            foreach (var item in items.Cast<ManagementObject>())
            {
                using (item)
                {
                    var charge = Convert.ToInt64(item["ChargeRate"] ?? 0L);
                    var discharge = Convert.ToInt64(item["DischargeRate"] ?? 0L);
                    result = (charge - discharge) / 1000.0;
                }
                break;
            }
        }
        catch (Exception exception)
        {
            AppLog.Error("Could not read battery power", exception);
        }

        _powerCache = result;
        Interlocked.Exchange(ref _powerCacheTicks, Environment.TickCount64);
        return result;
    }
}
