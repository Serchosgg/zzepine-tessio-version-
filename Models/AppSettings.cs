using System.Collections.Generic;

namespace GTAVInjector.Models
{
    public class AppSettings
    {
        public GameType GameType { get; set; } = GameType.Enhanced;
        public LauncherType LauncherType { get; set; } = LauncherType.Rockstar;
        public List<DllEntry> DllEntries { get; set; } = new List<DllEntry>();
        public bool AutoInject { get; set; } = false;
        public string Language { get; set; } = "es";
        public int LaunchDelay { get; set; } = 5;
    }
}