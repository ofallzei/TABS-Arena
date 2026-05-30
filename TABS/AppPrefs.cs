using System;
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
                    "Language=" + Language
                });
            }
            catch { }
        }
    }
}