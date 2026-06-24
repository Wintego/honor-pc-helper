using Microsoft.Win32;

namespace HonorPCHelper;

internal static class StartupManager
{
    private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "HonorPCHelper";

    private static string LegacyShortcutPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup), "HonorPCHelper.lnk");

    internal static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return key?.GetValue(ValueName) is string || File.Exists(LegacyShortcutPath);
        }
    }

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath, true);
        if (enabled)
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Не удалось определить путь приложения.");
            key.SetValue(ValueName, $"\"{executable}\"");
        }
        else
        {
            key.DeleteValue(ValueName, false);
            File.Delete(LegacyShortcutPath);
        }
    }
}
