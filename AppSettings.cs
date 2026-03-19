using System;
using System.IO;
using System.Text.Json;

namespace PanelApp
{
    public class AppSettings
    {
        public string MediaPlayerPath { get; set; } = @"C:\Program Files\MPC-BE\mpc-be64.exe";
        public bool RememberLastFolder { get; set; } = true;
        public string LastFolderPath { get; set; } = string.Empty;

        private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }
    }
}
