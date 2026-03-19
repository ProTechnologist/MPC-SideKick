using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace PanelApp
{
    public class VideoItem : INotifyPropertyChanged
    {
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public BitmapSource? Thumbnail { get; set; }

        private bool _isInPlaylist;
        public bool IsInPlaylist
        {
            get => _isInPlaylist;
            set
            {
                if (_isInPlaylist != value)
                {
                    _isInPlaylist = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
