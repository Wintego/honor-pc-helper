namespace HonorPCHelper;

internal sealed class NativePopupMenu : IDisposable
{
    internal IntPtr Handle { get; }
    private readonly Dictionary<int, Action> _callbacks;
    private readonly Dictionary<int, string> _tooltips;
    private readonly Dictionary<(IntPtr Menu, int Index), string> _subMenuTooltips;
    private readonly bool _ownsHandle;
    private static int _nextGlobalId = 1000;

    internal NativePopupMenu(
        Dictionary<int, Action>? callbacks = null,
        Dictionary<int, string>? tooltips = null,
        Dictionary<(IntPtr Menu, int Index), string>? subMenuTooltips = null,
        bool ownsHandle = true)
    {
        Handle = NativeMethods.CreatePopupMenu();
        _callbacks = callbacks ?? new Dictionary<int, Action>();
        _tooltips = tooltips ?? new Dictionary<int, string>();
        _subMenuTooltips = subMenuTooltips ?? new Dictionary<(IntPtr Menu, int Index), string>();
        _ownsHandle = ownsHandle;
    }

    internal NativePopupMenu AddSubMenu(string text, string? tooltip = null)
    {
        var index = NativeMethods.GetMenuItemCount(Handle);
        var sub = new NativePopupMenu(_callbacks, _tooltips, _subMenuTooltips, ownsHandle: false);
        NativeMethods.AppendMenuW(Handle, NativeMethods.MfPopup | NativeMethods.MfString | NativeMethods.MfEnabled,
            sub.Handle, text);
        if (tooltip is not null)
            _subMenuTooltips[(Handle, index)] = tooltip;
        return sub;
    }

    internal int AddItem(string text, Action? onClick, bool enabled = true, bool @checked = false, string? tooltip = null)
    {
        var id = Interlocked.Increment(ref _nextGlobalId);
        if (onClick != null)
            _callbacks[id] = onClick;
        if (tooltip != null)
            _tooltips[id] = tooltip;
        var flags = NativeMethods.MfString;
        if (enabled)
            flags |= NativeMethods.MfEnabled;
        else
            flags |= NativeMethods.MfGrayed | NativeMethods.MfDisabled;
        if (@checked)
            flags |= NativeMethods.MfChecked;
        NativeMethods.AppendMenuW(Handle, flags, id, text);
        return id;
    }

    internal void AddSeparator()
    {
        NativeMethods.AppendMenuW(Handle, NativeMethods.MfSeparator, 0, null);
    }

    internal int Show(IntPtr owner)
    {
        NativeMethods.SetForegroundWindow(owner);
        var pos = Control.MousePosition;
        lock (_activeMenuLock)
        {
            _activeMenu = this;
        }
        try
        {
            return NativeMethods.TrackPopupMenuEx(
                Handle,
                NativeMethods.TpmReturnCmd | NativeMethods.TpmLeftAlign | NativeMethods.TpmTopAlign | NativeMethods.TpmRightButton,
                pos.X, pos.Y,
                owner,
                IntPtr.Zero);
        }
        finally
        {
            lock (_activeMenuLock)
            {
                if (_activeMenu == this)
                    _activeMenu = null;
            }
        }
    }

    internal bool TryInvoke(int commandId)
    {
        if (_callbacks.TryGetValue(commandId, out var action))
        {
            action();
            return true;
        }
        return false;
    }

    internal static string? GetTooltip(int commandId)
    {
        NativePopupMenu? menu;
        lock (_activeMenuLock)
        {
            menu = _activeMenu;
        }
        if (menu == null)
            return null;
        lock (menu._tooltips)
        {
            return menu._tooltips.TryGetValue(commandId, out var t) ? t : null;
        }
    }

    internal static string? GetSubMenuTooltip(IntPtr menuHandle, int itemIndex)
    {
        NativePopupMenu? menu;
        lock (_activeMenuLock)
            menu = _activeMenu;
        if (menu is null)
            return null;
        lock (menu._subMenuTooltips)
            return menu._subMenuTooltips.TryGetValue((menuHandle, itemIndex), out var text) ? text : null;
    }

    private static NativePopupMenu? _activeMenu;
    private static readonly object _activeMenuLock = new();

    public void Dispose()
    {
        if (_ownsHandle && Handle != IntPtr.Zero)
            NativeMethods.DestroyMenu(Handle);
    }
}
