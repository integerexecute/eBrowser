using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace eBrowser
{
    public class Configuration
    {
        #region Fields
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("api_key")]
        public string ApiKey { get; set; } = string.Empty;

        [JsonPropertyName("hide_to_sys_tray")]
        public bool HideToSystemTray { get; set; } = true;

        [JsonPropertyName("pause_at_hide")]
        public bool PauseVideoAtHide { get; set; } = true;

        [JsonPropertyName("automute_video")]
        public bool AutoMuteVideo { get; set; } = true;

        [JsonPropertyName("autoplay_video")]
        public bool AutoPlayVideo { get; set; } = true;

        [JsonPropertyName("auto_download_images")]
        public bool AutoDownloadImages { get; set; }

        [JsonPropertyName("auto_download_videos")]
        public bool AutoDownloadVideos { get; set; }
        #endregion

        #region Static Methods
        public static string appDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eBrowser");
        public static string configPath => Path.Combine(appDataDirectory, "config.json");
        public static Configuration Current { get; set; } = new();
        public static Action<bool>? OnHideToSystemTrayChanged;

        static Configuration()
        {
            Load();
        }

        public static void EnsureAppDataDirectoryExists()
        {
            if (!Directory.Exists(appDataDirectory))
                Directory.CreateDirectory(appDataDirectory);
        }

        public static void Load()
        {
            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                var data = JsonSerializer.Deserialize<Configuration>(content);
                if (data != null)
                    Current = data;
            }
        }

        public static void Save()
        {
            EnsureAppDataDirectoryExists();
            File.WriteAllText(configPath, JsonSerializer.Serialize(Current));
        }
        #endregion
    }
}
