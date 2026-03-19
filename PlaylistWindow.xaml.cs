using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Shell;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace PanelApp
{
    public partial class PlaylistWindow : Window
    {
        private StickyWindowHelper _stickyHelper;
        private DispatcherTimer _mouseTimer;
        private bool _isPanelVisible = false;
        private AppSettings _settings;
        private System.Windows.Point _dragStartPoint;

        private const int TRIGGER_ZONE = 80;
        private const int WINDOW_OFFSET = 5;
        private const int HEIGHT_OFFSET = 10;

        public PlaylistWindow()
        {
            InitializeComponent();
            _settings = AppSettings.Load();
            PlaylistListView.ItemsSource = PlaylistManager.Instance.Items;
            
            this.Opacity = 0;
            _stickyHelper = new StickyWindowHelper(this, StickSide.Right, WINDOW_OFFSET, HEIGHT_OFFSET, Path.GetFileNameWithoutExtension(_settings.MediaPlayerPath));
            _stickyHelper.MpcFound += (hwnd) => this.Opacity = 1;
            _stickyHelper.MpcLost += () => this.Opacity = 0;

            _mouseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _mouseTimer.Tick += MouseTimer_Tick;
            _mouseTimer.Start();

            this.Loaded += (s, e) => {
                _stickyHelper.UpdatePosition();
                LoadThumbnailsForPlaylist();
            };
        }

        private async void LoadThumbnailsForPlaylist()
        {
            foreach (var item in PlaylistManager.Instance.Items.ToList())
            {
                if (item.Thumbnail == null && File.Exists(item.FilePath))
                {
                    item.Thumbnail = await Task.Run(() => GenerateThumbnail(item.FilePath));
                }
            }
        }

        private BitmapSource? GenerateThumbnail(string filePath)
        {
            try
            {
                using (var shellFile = ShellFile.FromFilePath(filePath))
                {
                    var bitmap = shellFile.Thumbnail.LargeBitmap;
                    return (BitmapSource)System.Windows.Application.Current.Dispatcher.Invoke(() => 
                    {
                        return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                            bitmap.GetHbitmap(),
                            IntPtr.Zero,
                            Int32Rect.Empty,
                            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                    });
                }
            }
            catch { return null; }
        }

        private void MouseTimer_Tick(object? sender, EventArgs e)
        {
            if (_stickyHelper.MpcHwnd == IntPtr.Zero) return;
            if (PinToggle.IsChecked == true) return;

            System.Windows.Point mousePos = GetMousePosition();
            if (!WinApi.GetWindowRect(_stickyHelper.MpcHwnd, out WinApi.RECT rect)) return;

            bool isOverPlayer = mousePos.X >= rect.Left && mousePos.X <= rect.Right &&
                               mousePos.Y >= rect.Top && mousePos.Y <= rect.Bottom;

            if (isOverPlayer)
            {
                double relativeXFromRight = rect.Right - mousePos.X;
                if (relativeXFromRight < TRIGGER_ZONE && !_isPanelVisible) ShowPanel();
                else if (relativeXFromRight > this.Width && _isPanelVisible) HidePanel();
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

        private void PlaylistListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PlaylistListView.SelectedItem is VideoItem selectedItem)
            {
                try { Process.Start(new ProcessStartInfo(_settings.MediaPlayerPath, $"\"{selectedItem.FilePath}\"") { UseShellExecute = true }); }
                catch (Exception ex) { System.Windows.MessageBox.Show($"Could not launch Media Player: {ex.Message}"); }
            }
        }

        private void RemoveFromPlaylist_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is VideoItem item)
            {
                PlaylistManager.Instance.Remove(item);
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e) => PlaylistManager.Instance.Clear();
        private void Shuffle_Click(object sender, RoutedEventArgs e) => PlaylistManager.Instance.Shuffle();
        private void PinToggle_Click(object sender, RoutedEventArgs e)
        {
            if (PinToggle.IsChecked == true && !_isPanelVisible) ShowPanel();
        }

        // Drag and Drop reordering
        private void PlaylistListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void PlaylistListView_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            System.Windows.Point mousePos = e.GetPosition(null);
            Vector diff = _dragStartPoint - mousePos;

            if (e.LeftButton == MouseButtonState.Pressed &&
                (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                 Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance))
            {
                var listView = sender as System.Windows.Controls.ListView;
                var listViewItem = FindAncestor<System.Windows.Controls.ListViewItem>((DependencyObject)e.OriginalSource);

                if (listViewItem != null)
                {
                    var videoItem = (VideoItem)listView?.ItemContainerGenerator.ItemFromContainer(listViewItem)!;
                    System.Windows.DataObject dragData = new System.Windows.DataObject("VideoItem", videoItem);
                    System.Windows.DragDrop.DoDragDrop(listViewItem, dragData, System.Windows.DragDropEffects.Move);
                }
            }
        }

        private void PlaylistListView_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("VideoItem"))
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void PlaylistListView_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent("VideoItem"))
            {
                var droppedData = e.Data.GetData("VideoItem") as VideoItem;
                var targetItem = FindAncestor<System.Windows.Controls.ListViewItem>((DependencyObject)e.OriginalSource);

                if (droppedData != null && targetItem != null)
                {
                    int oldIndex = PlaylistManager.Instance.Items.IndexOf(droppedData);
                    int newIndex = PlaylistListView.ItemContainerGenerator.IndexFromContainer(targetItem);

                    if (oldIndex != -1 && newIndex != -1 && oldIndex != newIndex)
                    {
                        PlaylistManager.Instance.Move(oldIndex, newIndex);
                    }
                }
            }
        }

        private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            do
            {
                if (current is T t) return t;
                current = VisualTreeHelper.GetParent(current);
            }
            while (current != null);
            return null;
        }
    }
}
