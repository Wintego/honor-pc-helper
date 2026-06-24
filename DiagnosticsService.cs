namespace HonorPCHelper;

internal static class DiagnosticsService
{
    internal static string BuildCompactToolTip()
    {
        var hasHardwareState = HardwareSensorSnapshot.TryParse(
            HardwareSettings.SensorSnapshot, out var hardwareState) && hardwareState.IsFresh;
        var mode = HardwareSettings.PerformanceModeActive
            ? L.T("производительный", "performance")
            : L.T("умный", "smart");
        var backlightLevel = hasHardwareState
            ? hardwareState.KeyboardBacklightMode switch
            {
                0x02 => KeyboardBacklightLevel.Off,
                0x03 => KeyboardBacklightLevel.Low,
                0x04 => KeyboardBacklightLevel.High,
                _ => HardwareSettings.KeyboardBacklight
            }
            : HardwareSettings.KeyboardBacklight;
        var backlight = backlightLevel switch
        {
            KeyboardBacklightLevel.Off => L.T("выкл.", "off"),
            KeyboardBacklightLevel.Low => L.T("слабая", "weak"),
            KeyboardBacklightLevel.High => L.T("сильная", "strong"),
            _ => "?"
        };
        var protection = hasHardwareState && hardwareState.ChargeStart.HasValue && hardwareState.ChargeEnd.HasValue
            ? $"{hardwareState.ChargeStart}–{hardwareState.ChargeEnd}%"
            : HardwareSettings.BatteryProtection switch
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
}
