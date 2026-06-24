namespace HonorPCHelper;

internal static class PerformanceModePolicy
{
    internal static bool CanEnable(out string reason)
    {
        var status = SystemInformation.PowerStatus;
        if (status.PowerLineStatus != PowerLineStatus.Online)
        {
            reason = L.T(
                "Производительный режим доступен только при подключённом источнике питания.",
                "Performance mode is available only when AC power is connected.");
            return false;
        }

        if (status.BatteryLifePercent >= 0 && status.BatteryLifePercent < 0.20f)
        {
            reason = L.T(
                "Для производительного режима заряд батареи должен быть не менее 20%.",
                "Performance mode requires at least 20% battery charge.");
            return false;
        }

        if (status.BatteryLifePercent < 0)
            AppLog.Info("Battery charge is unknown; performance mode allowed because AC power is connected");

        reason = string.Empty;
        return true;
    }
}
