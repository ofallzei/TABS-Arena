using System;
using System.Globalization;
using System.IO;

namespace TABS
{
    public enum SavedWindowMode
    {
        BorderlessFullscreen,
        Windowed
    }

    public static class AppPrefs
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TABSEconomyTracker");

        private static readonly string FilePath = Path.Combine(Folder, "app_prefs.txt");

        public static SavedWindowMode WindowMode { get; set; } = SavedWindowMode.BorderlessFullscreen;
        public static TwoVTwoGameMode.Loc.Language Language { get; set; } = TwoVTwoGameMode.Loc.Language.English;
        public static bool SoundsEnabled { get; set; } = true;
        public static double SoundVolume { get; set; } = 1.0;
        public static double ZoomScale { get; set; } = 1.0;

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return;

                foreach (string line in File.ReadAllLines(FilePath))
                {
                    string[] parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                        continue;

                    if (parts[0] == "WindowMode" &&
                        Enum.TryParse(parts[1], out SavedWindowMode mode))
                        WindowMode = mode;

                    if (parts[0] == "Language" &&
                        Enum.TryParse(parts[1], out TwoVTwoGameMode.Loc.Language lang))
                        Language = lang;

                    if (parts[0] == "SoundsEnabled" &&
                        bool.TryParse(parts[1], out bool soundsEnabled))
                        SoundsEnabled = soundsEnabled;

                    if (parts[0] == "SoundVolume" &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double soundVolume))
                        SoundVolume = Clamp(soundVolume, 0.0, 1.0);

                    if (parts[0] == "ZoomScale" &&
                        double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double zoomScale))
                        ZoomScale = Clamp(zoomScale, 0.5, 2.0);
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllLines(FilePath, new[]
                {
                    "WindowMode=" + WindowMode,
                    "Language=" + Language,
                    "SoundsEnabled=" + SoundsEnabled,
                    "SoundVolume=" + SoundVolume.ToString(CultureInfo.InvariantCulture),
                    "ZoomScale=" + ZoomScale.ToString(CultureInfo.InvariantCulture)
                });
            }
            catch { }
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
