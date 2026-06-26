namespace HonorPCHelper;

internal sealed class TouchpadBrightnessService : IDisposable
{
    private const byte GestureIdentifier = 0x0E;
    private static readonly (ushort Vendor, ushort Product)[] SupportedDevices =
    [
        (0x27C6, 0x0F9A),
        (0x35CC, 0x0104)
    ];

    private readonly Action<string> _reportError;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Lock _actionLock = new();
    private (byte Type, byte Direction, long Time) _lastGesture;
    private Task[] _readers = [];

    internal TouchpadBrightnessService(Action<string> reportError)
    {
        _reportError = reportError;
    }

    internal void Start()
    {
        try
        {
            var candidates = HidDevice.Enumerate()
                .Where(device => SupportedDevices.Contains((device.VendorId, device.ProductId)))
                .Where(device => device.UsagePage >= 0xFF00)
                .ToArray();

            if (candidates.Length == 0)
            {
                _reportError("Совместимый тачпад Honor не найден.");
                return;
            }

            _readers = candidates.Select(device => ReadDevice(device, _cancellation.Token)).ToArray();
        }
        catch (Exception exception)
        {
            _reportError($"Ошибка запуска тачпада: {exception.Message}");
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
    }

    private async Task ReadDevice(HidDeviceInfo device, CancellationToken cancellation)
    {
        using var handle = device.OpenForReading();
        if (handle.IsInvalid)
            return;

        try
        {
            await using var stream = new FileStream(handle, FileAccess.Read,
                Math.Max(device.InputReportLength, (ushort)64), true);
            var buffer = new byte[Math.Max(device.InputReportLength, (ushort)64)];
            while (!cancellation.IsCancellationRequested)
            {
                var count = await stream.ReadAsync(buffer, cancellation);
                if (count >= 3 && buffer[0] == GestureIdentifier)
                    ProcessGesture(buffer[1], buffer[2]);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _reportError($"Ошибка чтения тачпада: {exception.Message}");
        }
    }

    private void ProcessGesture(byte type, byte direction)
    {
        if (type != 0x03 || direction is not (0x01 or 0x02))
            return;

        lock (_actionLock)
        {
            var now = Environment.TickCount64;
            if (_lastGesture.Type == type && _lastGesture.Direction == direction && now - _lastGesture.Time < 150)
                return;
            _lastGesture = (type, direction, now);

            try
            {
                var up = direction == 0x01;
                // Сначала пробуем виртуальный HID-драйвер (нативный OSD Windows).
                // Если драйвера нет - откат на WMI (без OSD).
                if (!BrightnessVHid.TrySend(up))
                    BrightnessController.Change(up ? 5 : -5);
            }
            catch (Exception exception)
            {
                _reportError($"Не удалось изменить яркость: {exception.Message}");
            }
        }
    }
}
