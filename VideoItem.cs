using System.Windows.Media.Imaging;

namespace PanelApp
{
    public class VideoItem
    {
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public BitmapSource? Thumbnail { get; set; }
    }
}
