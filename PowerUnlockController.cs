namespace HonorPCHelper;

internal sealed class PowerUnlockController
{
    private const ulong PowerUnlockSetCommand = 0x00000F04;

    internal void SetEnabled(bool enabled)
    {
        var command = PowerUnlockSetCommand | ((enabled ? 1UL : 0UL) << 16);
        HonorWmi.Call(command);
    }
}
