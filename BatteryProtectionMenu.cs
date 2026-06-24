using System.ComponentModel;
using System.Diagnostics;

namespace HonorPCHelper;

internal static class BatteryProtectionMenu
{
    internal static void Build(NativePopupMenu menu)
    {
        var currentMode = HardwareSettings.BatteryProtection ?? BatteryProtectionMode.Home;

        menu.AddItem(
            L.T("Отключено", "Disabled"),
            async () => await ApplyModeAsync(BatteryProtectionMode.Disabled),
            @checked: currentMode == BatteryProtectionMode.Disabled,
            tooltip: L.T(
                "Разрешить обычную зарядку до 100 %.",
                "Allow normal charging to 100%."));
        menu.AddSeparator();
        menu.AddItem(
            L.T("Дом (40-70%) - рекомендуется", "Home (40-70%) - recommended"),
            async () => await ApplyModeAsync(BatteryProtectionMode.Home),
            @checked: currentMode == BatteryProtectionMode.Home,
            tooltip: L.T(
                "Прекращение зарядки при 70 % и возобновление при 40 %.",
                "Stop charging at 70% and resume at 40%."));
        menu.AddItem(
            L.T("Офис (70-90%)", "Office (70-90%)"),
            async () => await ApplyModeAsync(BatteryProtectionMode.Office),
            @checked: currentMode == BatteryProtectionMode.Office,
            tooltip: L.T(
                "Остановка зарядки при 90 % и возобновление при 70 %.",
                "Stop charging at 90% and resume at 70%."));
        menu.AddItem(
            L.T("Путешествия (95-100%)", "Travel (95-100%)"),
            async () => await ApplyModeAsync(BatteryProtectionMode.Travel),
            @checked: currentMode == BatteryProtectionMode.Travel,
            tooltip: L.T(
                "Прекращение зарядки при 100 % и возобновление при 95 %.",
                "Stop charging at 100% and resume at 95%."));
    }

    private static async Task ApplyModeAsync(BatteryProtectionMode mode)
    {
        try
        {
            if (await PrivilegedHardware.TryRunBatteryTaskAsync(mode))
            {
                HardwareSettings.BatteryProtection = mode;
                return;
            }

            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException(L.T(
                    "Не удалось определить путь к HonorPCHelper.exe.",
                    "Could not determine the path to HonorPCHelper.exe."));
            var startInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = true,
                Verb = "runas"
            };
            startInfo.ArgumentList.Add("--set-battery-mode");
            startInfo.ArgumentList.Add(mode.ToString());

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(L.T(
                    "Не удалось запустить настройку батареи.",
                    "Could not start battery configuration."));
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                return;

            HardwareSettings.BatteryProtection = mode;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Honor PC Helper", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
