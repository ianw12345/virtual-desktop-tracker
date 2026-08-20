using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using VirtualDesktopHelper.Services;

namespace VirtualDesktopDisplayer.Services
{
    /// <summary>
    /// Listens for the dedicated Back and Forward mouse buttons while the application is running.
    /// It only installs a global hook when the user enabled the feature.
    /// </summary>
    public sealed class GlobalMouseNavigationService : IDisposable
    {
        private const int WhMouseLl = 14;
        private const int WmXButtonDown = 0x020B;
        private const int XButton1 = 1;
        private const int XButton2 = 2;

        private readonly LowLevelMouseProc _hookCallback;
        private IntPtr _hookHandle;

        public GlobalMouseNavigationService()
        {
            _hookCallback = HookCallback;
        }

        public event Action<DesktopNavigationDirection>? NavigationRequested;

        public bool IsRunning => _hookHandle != IntPtr.Zero;

        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            _hookHandle = SetWindowsHookEx(WhMouseLl, _hookCallback, IntPtr.Zero, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the global mouse-button hook.");
            }
        }

        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        public void Dispose() => Stop();

        private IntPtr HookCallback(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && message.ToInt64() == WmXButtonDown)
            {
                var mouseData = Marshal.PtrToStructure<MsllHookStruct>(data);
                int button = (int)((mouseData.MouseData >> 16) & 0xffff);
                DesktopNavigationDirection? direction = button switch
                {
                    XButton1 => DesktopNavigationDirection.Previous,
                    XButton2 => DesktopNavigationDirection.Next,
                    _ => null
                };

                if (direction.HasValue)
                {
                    NavigationRequested?.Invoke(direction.Value);
                    // The enabled feature remaps the dedicated buttons, so do not also let the
                    // foreground application interpret them as browser Back/Forward commands.
                    return new IntPtr(1);
                }
            }

            return CallNextHookEx(_hookHandle, code, message, data);
        }

        private delegate IntPtr LowLevelMouseProc(int code, IntPtr message, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct MsllHookStruct
        {
            public Point Point;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelMouseProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int code, IntPtr message, IntPtr data);
    }
}
