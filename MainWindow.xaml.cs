using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Shell;

namespace PanelApp
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<VideoItem> _videoItems = new ObservableCollection<VideoItem>();
        private ICollectionView _videoView;
        private AppSettings _settings;
        private IntPtr _mpcHwnd = IntPtr.Zero;
        private WinApi.WinEventDelegate _winEventDelegate;
        private IntPtr _hHook = IntPtr.Zero;
        private DispatcherTimer _monitorTimer;
        private DispatcherTimer _mouseTimer;
        private bool _isPanelVisible = false;
        
        private const string MPC_PROCESS_NAME = "mpc-be64";
        private const int TRIGGER_ZONE = 80;
        private const int WINDOW_OFFSET = 5;
        private const int HEIGHT_OFFSET = 10;

        private System.Windows.Forms.NotifyIcon? _notifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            try { _settings = AppSettings.Load(); } catch { _settings = new AppSettings(); }
            
            _videoView = CollectionViewSource.GetDefaultView(_videoItems);
            _videoView.Filter = FilterVideos;
            VideoListView.ItemsSource = _videoView;
            
            SetupTrayIcon();
            this.Opacity = 0;
            
            _winEventDelegate = new WinApi.WinEventDelegate(WinEventCallback);
            
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _monitorTimer.Tick += MonitorTimer_Tick;
            _monitorTimer.Start();

            _mouseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _mouseTimer.Tick += MouseTimer_Tick;
            _mouseTimer.Start();

            this.Loaded += MainWindow_Loaded;
            this.Closed += MainWindow_Closed;

            try
            {
                if (_settings.RememberLastFolder && !string.IsNullOrEmpty(_settings.LastFolderPath) && Directory.Exists(_settings.LastFolderPath))
                {
                    _ = LoadVideosAsync(_settings.LastFolderPath);
                    FolderTooltip.Content = _settings.LastFolderPath;
                }
            }
            catch { }
        }

        private bool FilterVideos(object item)
        {
            if (item is VideoItem video)
            {
                if (string.IsNullOrWhiteSpace(SearchBox.Text)) return true;
                return video.FileName?.Contains(SearchBox.Text, StringComparison.OrdinalIgnoreCase) ?? false;
            }
            return false;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _videoView.Refresh();
        }

        private void SetupTrayIcon()
        {
            try
            {
                _notifyIcon = new System.Windows.Forms.NotifyIcon();
                _notifyIcon.Visible = true;
                _notifyIcon.Text = "MPC-BE Companion Panel";
                _notifyIcon.Icon = System.Drawing.SystemIcons.Application;

                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    try { _notifyIcon.Icon = new System.Drawing.Icon(iconPath); } catch {}
                }

                var contextMenu = new System.Windows.Forms.ContextMenuStrip();
                contextMenu.Items.Add("Exit", null, (s, e) => System.Windows.Application.Current.Shutdown());
                _notifyIcon.ContextMenuStrip = contextMenu;
            }
            catch (Exception ex) { File.WriteAllText("tray_error.txt", ex.ToString()); }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e) => UpdatePosition();

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            if (_hHook != IntPtr.Zero) WinApi.UnhookWinEvent(_hHook);
            if (_notifyIcon != null) _notifyIcon.Dispose();
            _settings.Save();
        }

        private void MonitorTimer_Tick(object? sender, EventArgs e) => FindMpcWindow();

        private void FindMpcWindow()
        {
            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(_settings.MediaPlayerPath));
            if (processes.Length > 0)
            {
                IntPtr hwnd = processes[0].MainWindowHandle;
                if (hwnd != IntPtr.Zero && hwnd != _mpcHwnd)
                {
                    _mpcHwnd = hwnd;
                    SetupHook();
                    UpdatePosition();
                    this.Opacity = 1; 
                }
            }
            else if (_mpcHwnd != IntPtr.Zero)
            {
                _mpcHwnd = IntPtr.Zero;
                this.Opacity = 0;
                if (_hHook != IntPtr.Zero) { WinApi.UnhookWinEvent(_hHook); _hHook = IntPtr.Zero; }
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

        private void UpdatePosition()
        {
            if (_mpcHwnd == IntPtr.Zero || !WinApi.GetWindowRect(_mpcHwnd, out WinApi.RECT rect))
            {
                this.Hide();
                return;
            }

            this.Show();
            double screenX = rect.Left + WINDOW_OFFSET;
            double screenY = rect.Top;
            double screenHeight = rect.Height - HEIGHT_OFFSET;

            WinApi.SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero, (int)screenX, (int)screenY, (int)this.Width, (int)screenHeight, WinApi.SWP_NOZORDER | WinApi.SWP_SHOWWINDOW);
            this.Height = screenHeight;
        }

        private void MouseTimer_Tick(object? sender, EventArgs e)
        {
            if (_mpcHwnd == IntPtr.Zero) return;
            if (PinToggle.IsChecked == true) return; // Skip auto-hide if pinned

            System.Windows.Point mousePos = GetMousePosition();
            if (!WinApi.GetWindowRect(_mpcHwnd, out WinApi.RECT rect)) return;

            bool isOverPlayer = mousePos.X >= rect.Left && mousePos.X <= rect.Right &&
                               mousePos.Y >= rect.Top && mousePos.Y <= rect.Bottom;

            if (isOverPlayer)
            {
                double relativeX = mousePos.X - rect.Left;
                if (relativeX < TRIGGER_ZONE && !_isPanelVisible) ShowPanel();
                else if (relativeX > this.Width && _isPanelVisible) HidePanel();
            }
            else if (_isPanelVisible) HidePanel();
        }

        private void ShowPanel()
        {
            _isPanelVisible = true;
            Storyboard? sb = this.FindResource("SlideIn") as Storyboard;
            sb?.Begin();
        }

        private void HidePanel()
        {
            _isPanelVisible = false;
            Storyboard? sb = this.FindResource("SlideOut") as Storyboard;
            sb?.Begin();
        }

        private System.Windows.Point GetMousePosition()
        {
            WinApi.POINT w32Mouse = new WinApi.POINT();
            WinApi.GetCursorPos(out w32Mouse);
            return new System.Windows.Point(w32Mouse.X, w32Mouse.Y);
        }

        private async void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                _settings.LastFolderPath = dialog.FolderName;
                FolderTooltip.Content = _settings.LastFolderPath;
                await LoadVideosAsync(dialog.FolderName);
                _settings.Save();
            }
        }

        private void PinToggle_Click(object sender, RoutedEventArgs e)
        {
            PinImage.Opacity = PinToggle.IsChecked == true ? 1.0 : 0.5;
            if (PinToggle.IsChecked == true && !_isPanelVisible) ShowPanel();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_settings);
            if (win.ShowDialog() == true)
            {
                _settings = win.CurrentSettings;
                _settings.Save();
                UpdatePosition();
            }
        }

        private async Task LoadVideosAsync(string folderPath)
        {
            _videoItems.Clear();
            var extensions = new[] { ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv" };
            if (!Directory.Exists(folderPath)) return;
            
            var files = Directory.EnumerateFiles(folderPath)
                                 .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                                 .ToList();

            foreach (var file in files)
            {
                var item = await Task.Run(() => CreateVideoItem(file));
                if (item != null) _videoItems.Add(item);
            }
        }

        private VideoItem? CreateVideoItem(string filePath)
        {
            try
            {
                using (var shellFile = ShellFile.FromFilePath(filePath))
                {
                    var bitmap = shellFile.Thumbnail.LargeBitmap;
                    var bitmapSource = System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        return Imaging.CreateBitmapSourceFromHBitmap(
                            bitmap.GetHbitmap(),
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                    });
                    return new VideoItem { FilePath = filePath, FileName = Path.GetFileName(filePath), Thumbnail = bitmapSource };
                }
            }
            catch { return null; }
        }

        private void VideoListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (VideoListView.SelectedItem is VideoItem selectedItem)
            {
                try { Process.Start(new ProcessStartInfo(_settings.MediaPlayerPath, $"\"{selectedItem.FilePath}\"") { UseShellExecute = true }); }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Could not launch Media Player: {ex.Message}"); }
            }
        }
    }
}
