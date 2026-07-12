using System;
using System.IO;

namespace RaidEye.Agent
{
    internal static class AppPaths
    {
        public static string AppDataDirectory
        {
            get
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "RaidEye");
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(AppDataDirectory, "settings.ini"); }
        }

        public static string EventsPath
        {
            get { return Path.Combine(AppDataDirectory, "events.jsonl"); }
        }

        public static void EnsureDirectory()
        {
            Directory.CreateDirectory(AppDataDirectory);
        }
    }
}
