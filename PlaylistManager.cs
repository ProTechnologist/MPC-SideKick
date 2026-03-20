using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MPC.SideKick
{
    public class PlaylistManager
    {
        private static readonly string PlaylistFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "playlist.json");
        private static PlaylistManager? _instance;
        public static PlaylistManager Instance => _instance ??= new PlaylistManager();

        public ObservableCollection<VideoItem> Items { get; } = new ObservableCollection<VideoItem>();

        private PlaylistManager()
        {
            Load();
        }

        public void Add(VideoItem item)
        {
            if (!Items.Any(i => i.FilePath == item.FilePath))
            {
                Items.Add(item);
                item.IsInPlaylist = true; // Mark as in playlist
                Save();
            }
        }

        public void Remove(VideoItem item)
        {
            var itemToRemove = Items.FirstOrDefault(i => i.FilePath == item.FilePath);
            if (itemToRemove != null)
            {
                Items.Remove(itemToRemove);
                itemToRemove.IsInPlaylist = false;
                Save();
            }
        }

        public void Clear()
        {
            foreach (var item in Items)
            {
                item.IsInPlaylist = false;
            }
            Items.Clear();
            Save();
        }

        public void Shuffle()
        {
            var list = Items.ToList();
            var rng = new Random();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
            Items.Clear();
            foreach (var item in list) Items.Add(item);
            Save();
        }

        public void Move(int oldIndex, int newIndex)
        {
            if (oldIndex < 0 || oldIndex >= Items.Count || newIndex < 0 || newIndex >= Items.Count) return;
            var item = Items[oldIndex];
            Items.RemoveAt(oldIndex);
            Items.Insert(newIndex, item);
            Save();
        }

        public void Save()
        {
            try
            {
                var paths = Items.Select(i => i.FilePath).ToList();
                string json = JsonSerializer.Serialize(paths, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(PlaylistFile, json);
            }
            catch { }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(PlaylistFile))
                {
                    string json = File.ReadAllText(PlaylistFile);
                    var paths = JsonSerializer.Deserialize<List<string>>(json);
                    if (paths != null)
                    {
                        // Note: Thumbnails will be loaded on demand or we can just populate paths first
                        // For simplicity in this app, we'll just store paths and thumbnails will be null 
                        // until we re-generate them or if the VideoItem is added from the library.
                        // However, VideoItem in the UI needs a thumbnail.
                        // We'll let the PlaylistWindow handle the thumbnail generation for loaded paths.
                        foreach (var path in paths)
                        {
                            if (File.Exists(path))
                            {
                                Items.Add(new VideoItem { FilePath = path, FileName = Path.GetFileName(path), IsInPlaylist = true });
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
