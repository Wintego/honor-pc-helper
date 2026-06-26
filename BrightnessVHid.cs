using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HonorPCHelper;

// Клиент виртуального HID-драйвера BrightnessVHid. Шлёт IOCTL, по которому драйвер
// выдаёт системе настоящую HID consumer-команду яркости -> нативный OSD Windows.
// Если драйвер не установлен, TrySend возвращает false (вызывающий откатывается на WMI).
internal static class BrightnessVHid
{
    private static readonly Guid InterfaceGuid = new("A3F8E2C1-4B6D-4E9A-9C2F-1D7B8E5A6C30");
    private const uint IoctlSend = 0x0022A000; // CTL_CODE(FILE_DEVICE_UNKNOWN,0x800,METHOD_BUFFERED,FILE_WRITE_ACCESS)
    private const byte DirUp = 1;
    private const byte DirDown = 2;

    private static readonly Lock Gate = new();
    private static SafeFileHandle? _handle;

    internal static bool TrySend(bool up)
    {
        lock (Gate)
        {
            if (!EnsureOpen())
                return false;

            var buffer = new[] { up ? DirUp : DirDown };
            var ok = NativeMethods.DeviceIoControl(
                _handle!, IoctlSend, buffer, (uint)buffer.Length,
                IntPtr.Zero, 0, out _, IntPtr.Zero);

            if (!ok)
            {
                _handle?.Dispose();
                _handle = null;
                return false;
            }
            return true;
        }
    }

    private static bool EnsureOpen()
    {
        if (_handle is { IsInvalid: false })
            return true;

        var path = FindDevicePath();
        if (path is null)
            return false;

        var handle = NativeMethods.CreateFile(
            path, NativeMethods.GenericWrite,
            NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
            IntPtr.Zero, NativeMethods.OpenExisting, 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return false;
        }

        _handle = handle;
        return true;
    }

    private static string? FindDevicePath()
    {
        var guid = InterfaceGuid;
        var set = NativeMethods.SetupDiGetClassDevsW(
            ref guid, IntPtr.Zero, IntPtr.Zero,
            NativeMethods.DigcfPresent | NativeMethods.DigcfDeviceInterface);

        if (set == IntPtr.Zero || set == new IntPtr(-1))
            return null;

        try
        {
            var data = new NativeMethods.SpDeviceInterfaceData();
            data.Size = (uint)Marshal.SizeOf<NativeMethods.SpDeviceInterfaceData>();
            if (!NativeMethods.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref guid, 0, ref data))
                return null;

            return NativeMethods.GetDevicePath(set, ref data);
        }
        catch
        {
            return null;
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(set);
        }
    }
}
