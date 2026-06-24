using System.ComponentModel;
using System.Diagnostics;

namespace HonorPCHelper;

internal static class KeyboardBacklightMenu
{
    internal static void Build(NativePopupMenu menu, BacklightScheduleService scheduleService)
    {
        var current = HardwareSettings.KeyboardBacklight;

        menu.AddItem(
            L.T("Выключена", "Off"),
            async () => await ApplyLevelAsync(KeyboardBacklightLevel.Off, scheduleService),
            @checked: current == KeyboardBacklightLevel.Off);
        menu.AddItem(
            L.T("Слабая", "Weak"),
            async () => await ApplyLevelAsync(KeyboardBacklightLevel.Low, scheduleService),
            @checked: current == KeyboardBacklightLevel.Low);
        menu.AddItem(
            L.T("Сильная", "Strong"),
            async () => await ApplyLevelAsync(KeyboardBacklightLevel.High, scheduleService),
            @checked: current == KeyboardBacklightLevel.High);
        menu.AddSeparator();

        BuildTimeoutSubMenu(menu.AddSubMenu(L.T("Таймаут", "Timeout")));
        BuildScheduleSubMenu(menu.AddSubMenu(L.T("Расписание", "Schedule")), scheduleService);
    }

    private static void BuildTimeoutSubMenu(NativePopupMenu menu)
    {
        var current = HardwareSettings.KeyboardBacklightTimeout;
        AddTimeoutItem(menu, L.T("Не выключать", "Never"), 0, current);
        AddTimeoutItem(menu, L.T("15 секунд", "15 seconds"), 15, current);
        AddTimeoutItem(menu, L.T("30 секунд", "30 seconds"), 30, current);
        AddTimeoutItem(menu, L.T("1 минута", "1 minute"), 60, current);
        AddTimeoutItem(menu, L.T("5 минут", "5 minutes"), 300, current);
    }

    private static void AddTimeoutItem(
        NativePopupMenu menu, string text, ushort seconds, ushort? current)
    {
        menu.AddItem(
            text,
            async () => await ApplyTimeoutAsync(seconds),
            @checked: current == seconds);
    }

    private static async Task ApplyTimeoutAsync(ushort seconds)
    {
        try
        {
            if (await PrivilegedHardware.TryRunBacklightTimeoutTaskAsync(seconds))
            {
                HardwareSettings.KeyboardBacklightTimeout = seconds;
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
            startInfo.ArgumentList.Add("--set-keyboard-backlight-timeout");
            startInfo.ArgumentList.Add(seconds.ToString());

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(L.T(
                    "Не удалось изменить таймаут подсветки клавиатуры.",
                    "Could not change the keyboard backlight timeout."));
            await process.WaitForExitAsync();
            if (process.ExitCode == 0)
                HardwareSettings.KeyboardBacklightTimeout = seconds;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "Honor PC Helper", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void BuildScheduleSubMenu(NativePopupMenu menu, BacklightScheduleService scheduleService)
    {
        menu.AddItem(
            L.T("Включено", "Enabled"),
            async () =>
            {
                HardwareSettings.BacklightScheduleEnabled = !HardwareSettings.BacklightScheduleEnabled;
                await scheduleService.SettingsChangedAsync();
            },
            @checked: HardwareSettings.BacklightScheduleEnabled);
        menu.AddSeparator();

        BuildHourSubMenu(menu.AddSubMenu(L.T("Включать в", "Turn on at")),
            HardwareSettings.BacklightOnHour,
            h => HardwareSettings.BacklightOnHour = h,
            scheduleService.SettingsChangedAsync);
        BuildHourSubMenu(menu.AddSubMenu(L.T("Выключать в", "Turn off at")),
            HardwareSettings.BacklightOffHour,
            h => HardwareSettings.BacklightOffHour = h,
            scheduleService.SettingsChangedAsync);

        var levelSub = menu.AddSubMenu(L.T("Уровень при включении", "Level when on"));
        levelSub.AddItem(
            L.T("Слабая", "Weak"),
            async () =>
            {
                HardwareSettings.BacklightScheduleLevel = KeyboardBacklightLevel.Low;
                await scheduleService.SettingsChangedAsync();
            },
            @checked: HardwareSettings.BacklightScheduleLevel == KeyboardBacklightLevel.Low);
        levelSub.AddItem(
            L.T("Сильная", "Strong"),
            async () =>
            {
                HardwareSettings.BacklightScheduleLevel = KeyboardBacklightLevel.High;
                await scheduleService.SettingsChangedAsync();
            },
            @checked: HardwareSettings.BacklightScheduleLevel == KeyboardBacklightLevel.High);
    }

    private static void BuildHourSubMenu(
        NativePopupMenu menu, int current, Action<int> save, Func<Task> settingsChanged)
    {
        for (var hour = 0; hour < 24; hour++)
        {
            var h = hour;
            menu.AddItem(
                $"{hour:00}:00",
                async () =>
                {
                    save(h);
                    await settingsChanged();
                },
                @checked: hour == current);
        }
    }

    private static async Task ApplyLevelAsync(
        KeyboardBacklightLevel level, BacklightScheduleService scheduleService)
    {
        try
        {
            if (await PrivilegedHardware.TryRunBacklightTaskAsync(level))
            {
                HardwareSettings.KeyboardBacklight = level;
                scheduleService.SetManualOverride();
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
            startInfo.ArgumentList.Add("--set-keyboard-backlight");
            startInfo.ArgumentList.Add(level.ToString());

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(L.T(
                    "Не удалось изменить подсветку клавиатуры.",
                    "Could not change the keyboard backlight."));
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                return;

            HardwareSettings.KeyboardBacklight = level;
            scheduleService.SetManualOverride();
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
