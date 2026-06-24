namespace HonorPCHelper;

internal readonly record struct HardwareSensorSnapshot(
    DateTime SampledAt,
    int? Fan1Rpm,
    int? Fan2Rpm,
    int? CpuTemperature,
    int? BatteryTemperature,
    int? KeyboardBacklightMode,
    int? ChargeStart,
    int? ChargeEnd)
{
    internal bool IsFresh => DateTime.UtcNow - SampledAt < TimeSpan.FromSeconds(30);

    internal string Serialize(string requestId)
        => string.Join('|', requestId, SampledAt.Ticks, Fan1Rpm, Fan2Rpm, CpuTemperature,
            BatteryTemperature, KeyboardBacklightMode, ChargeStart, ChargeEnd);

    internal static bool TryParse(string? value, out HardwareSensorSnapshot snapshot)
    {
        snapshot = default;
        var parts = value?.Split('|');
        if (parts is not { Length: 9 } || !long.TryParse(parts[1], out var ticks))
            return false;

        snapshot = new HardwareSensorSnapshot(
            new DateTime(ticks, DateTimeKind.Utc),
            ParseNullable(parts[2]), ParseNullable(parts[3]), ParseNullable(parts[4]),
            ParseNullable(parts[5]), ParseNullable(parts[6]), ParseNullable(parts[7]),
            ParseNullable(parts[8]));
        return true;
    }

    private static int? ParseNullable(string value)
        => int.TryParse(value, out var result) ? result : null;
}

internal static class HardwareSensorController
{
    private const ulong FanSpeedGetCommand = 0x00000802;
    private const ulong TemperatureGetCommand = 0x00000202;
    private const ulong KeyboardBacklightModeGetCommand = 0x00001306;
    private const ulong BatteryThresholdsGetCommand = 0x00001103;

    internal static void ReadAndStore(string requestId)
    {
        var backlightMode = ReadValue(KeyboardBacklightModeGetCommand, 1, "keyboard backlight mode");
        var chargeStart = ReadValue(BatteryThresholdsGetCommand, 1, "battery charge start threshold");
        var chargeEnd = ReadValue(BatteryThresholdsGetCommand, 2, "battery charge end threshold");
        var snapshot = new HardwareSensorSnapshot(
            DateTime.UtcNow, ReadFan(0), ReadFan(1), ReadTemperature(0x00), ReadTemperature(0x0E),
            backlightMode, chargeStart, chargeEnd);
        HardwareSettings.SensorSnapshot = snapshot.Serialize(requestId);

        HardwareSettings.KeyboardBacklight = backlightMode switch
        {
            0x02 => KeyboardBacklightLevel.Off,
            0x03 => KeyboardBacklightLevel.Low,
            0x04 => KeyboardBacklightLevel.High,
            _ => HardwareSettings.KeyboardBacklight
        };
        HardwareSettings.BatteryProtection = (chargeStart, chargeEnd) switch
        {
            (40, 70) => BatteryProtectionMode.Home,
            (70, 90) => BatteryProtectionMode.Office,
            (95, 100) => BatteryProtectionMode.Travel,
            (0, 100) => BatteryProtectionMode.Disabled,
            _ => HardwareSettings.BatteryProtection
        };
    }

    private static int? ReadFan(byte index)
    {
        try
        {
            var output = HonorWmi.Call(FanSpeedGetCommand | ((ulong)index << 16));
            return output.Length >= 3 ? output[1] | (output[2] << 8) : null;
        }
        catch (Exception exception)
        {
            AppLog.Error($"Could not read fan {index + 1} speed", exception);
            return null;
        }
    }

    private static int? ReadTemperature(byte zone)
    {
        try
        {
            var output = HonorWmi.Call(TemperatureGetCommand | ((ulong)zone << 16));
            return output.Length >= 3 ? output[2] : null;
        }
        catch (Exception exception)
        {
            AppLog.Error($"Could not read temperature zone 0x{zone:X2}", exception);
            return null;
        }
    }

    private static int? ReadValue(ulong command, int offset, string name)
    {
        try
        {
            var output = HonorWmi.Call(command);
            return output.Length > offset ? output[offset] : null;
        }
        catch (Exception exception)
        {
            AppLog.Error($"Could not read {name}", exception);
            return null;
        }
    }
}
