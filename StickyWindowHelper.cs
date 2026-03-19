using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PanelApp
{
    public enum StickSide
    {
        Left,
        Right
    }

    public class StickyWindowHelper
    {
        private readonly Window _window;
        private readonly StickSide _side;
        private readonly int _offset;
        private readonly int _heightOffset;
        private IntPtr _mpcHwnd = IntPtr.Zero;
        private WinApi.WinEventDelegate _winEventDelegate;
        private IntPtr _hHook = IntPtr.Zero;
        private DispatcherTimer _monitorTimer;
        private readonly string _processName;
        
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOACTIVATE = 0x0010;

        public event Action<IntPtr>? MpcFound;
        public event Action? MpcLost;
        public event Action? PositionUpdated;

        public IntPtr MpcHwnd => _mpcHwnd;

        public StickyWindowHelper(Window window, StickSide side, int offset, int heightOffset, string processName = "mpc-be64")
        {
            _window = window;
            _side = side;
            _offset = offset;
            _heightOffset = heightOffset;
            _processName = processName;
            
            _winEventDelegate = new WinApi.WinEventDelegate(WinEventCallback);
            
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _monitorTimer.Tick += (s, e) => FindMpcWindow();
            _monitorTimer.Start();

            _window.Closed += (s, e) => {
                if (_hHook != IntPtr.Zero) WinApi.UnhookWinEvent(_hHook);
            };
        }

        private void FindMpcWindow()
        {
            var processes = Process.GetProcessesByName(_processName);
            if (processes.Length > 0)
            {
                IntPtr hwnd = processes[0].MainWindowHandle;
                if (hwnd != IntPtr.Zero && hwnd != _mpcHwnd)
                {
                    _mpcHwnd = hwnd;
                    SetupHook();
                    UpdatePosition();
                    MpcFound?.Invoke(_mpcHwnd);
                }
            }
            else if (_mpcHwnd != IntPtr.Zero)
            {
                _mpcHwnd = IntPtr.Zero;
                if (_hHook != IntPtr.Zero)
                {
                    WinApi.UnhookWinEvent(_hHook);
                    _hHook = IntPtr.Zero;
                }
                MpcLost?.Invoke();
            }
        }

        private void SetupHook()
        {
            if (_hHook != IntPtr.Zero) WinApi.UnhookWinEvent(_hHook);
            uint processId;
            uint threadId = WinApi.GetWindowThreadProcessId(_mpcHwnd, out processId);
            _hHook = WinApi.SetWinEventHook(WinApi.EVENT_OBJECT_LOCATIONCHANGE, WinApi.EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, _winEventDelegate, processId, threadId, WinApi.WINEVENT_OUTOFCONTEXT);
        }

        private void WinEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (hwnd == _mpcHwnd) UpdatePosition();
        }

        public void UpdatePosition()
        {
            if (_mpcHwnd == IntPtr.Zero || !WinApi.GetWindowRect(_mpcHwnd, out WinApi.RECT rect))
            {
                _window.Hide();
                return;
            }

            _window.Show();
            double screenX;
            if (_side == StickSide.Left)
            {
                screenX = rect.Left + _offset;
            }
            else
            {
                screenX = rect.Right - _window.Width - _offset;
            }

            double screenY = rect.Top;
            double screenHeight = rect.Height - _heightOffset;

            WinApi.SetWindowPos(new WindowInteropHelper(_window).Handle, HWND_TOPMOST, (int)screenX, (int)screenY, (int)_window.Width, (int)screenHeight, SWP_NOACTIVATE | WinApi.SWP_SHOWWINDOW);
            _window.Height = screenHeight;
            PositionUpdated?.Invoke();
        }
    }
}
