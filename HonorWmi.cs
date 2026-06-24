using System.Management;

namespace HonorPCHelper;

internal sealed class HonorWmiCommandException(int errorCode, string message) : InvalidOperationException(message)
{
    internal int ErrorCode { get; } = errorCode;
}

internal static class HonorWmi
{
    internal static byte[] Call(ulong command)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\wmi");
                scope.Options.EnablePrivileges = true;
                scope.Connect();

                using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM OemWMIMethod"));
                using var instances = searcher.Get();
                var instance = instances.Cast<ManagementObject>().FirstOrDefault()
                    ?? throw new InvalidOperationException(L.T(
                        "Интерфейс HONOR BIOS WMI не найден.",
                        "HONOR BIOS WMI interface was not found."));

                using (instance)
                using (var parameters = instance.GetMethodParameters("OemWMIfun"))
                {
                    var input = new byte[64];
                    BitConverter.GetBytes(command).CopyTo(input, 0);
                    parameters["u8Input"] = input;
                    using var result = instance.InvokeMethod("OemWMIfun", parameters, null);
                    var output = (byte[]?)result?["u8Output"]
                        ?? throw new InvalidOperationException(L.T(
                            "BIOS не вернул ответ.",
                            "BIOS did not return a response."));

                    if (output.Length > 0 && output[0] == 0)
                        return output;

                    var errorCode = output.Length > 0 ? output[0] : 255;
                    lastError = new HonorWmiCommandException(errorCode, L.T(
                        $"BIOS отклонил команду (код {errorCode}).",
                        $"BIOS rejected the command (code {errorCode})."));
                }
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new InvalidOperationException(L.T(
                    "Для обращения к HONOR BIOS нужны права администратора.",
                    "Administrator privileges are required to access HONOR BIOS."), exception);
            }
            catch (ManagementException exception)
            {
                lastError = new InvalidOperationException(L.T(
                    "Ошибка HONOR BIOS WMI: ",
                    "HONOR BIOS WMI error: ") + exception.Message, exception);
            }
        }

        var finalError = lastError ?? new InvalidOperationException(L.T(
            "Команда BIOS не выполнена.",
            "BIOS command was not executed."));
        AppLog.Error($"HONOR WMI command 0x{command:X} failed", finalError);
        throw finalError;
    }
}
