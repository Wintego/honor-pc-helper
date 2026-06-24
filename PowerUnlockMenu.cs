using System.ComponentModel;
using System.Diagnostics;

namespace HonorPCHelper;

internal static class PowerUnlockMenu
{
    internal static int Build(NativePopupMenu menu, Action modeChanged)
    {
        return menu.AddItem(
            L.T("Производительный режим", "Performance mode"),
            async () => await ApplyModeAsync(modeChanged),
            @checked: HardwareSettings.PerformanceModeActive,
            tooltip: L.T(
                "Галочка: производительный режим. Без галочки: умный режим. Переключение также доступно через Fn+P.",
                "Checked: performance mode. Unchecked: smart mode. Fn+P also switches modes."));
    }

    private static async Task ApplyModeAsync(Action modeChanged)
    {
        var target = !HardwareSettings.PerformanceModeActive;
        try
        {
            if (target && !PerformanceModePolicy.CanEnable(out var reason))
            {
                MessageBox.Show(reason, "Honor PC Helper", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (await PrivilegedHardware.TryRunPowerUnlockTaskAsync(target))
            {
                HardwareSettings.PowerUnlock = target;
                HardwareSettings.PerformanceModeActive = target;
                modeChanged();
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
            startInfo.ArgumentList.Add("--set-power-unlock");
            startInfo.ArgumentList.Add(target.ToString());

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(L.T(
                    "Не удалось изменить режим производительности.",
                    "Could not change performance mode."));
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                return;

            HardwareSettings.PowerUnlock = target;
            HardwareSettings.PerformanceModeActive = target;
            modeChanged();
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
