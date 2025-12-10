using System.Collections.Generic;
using System.IO;
using GTAVInjector.Models;

namespace GTAVInjector.Models
{
    public class AppSettings
    {
        public GameType GameType { get; set; } = GameType.Enhanced;
        public LauncherType LauncherType { get; set; } = LauncherType.Rockstar;
        public List<DllEntry> DllEntries { get; set; } = new();
        public bool AutoInject { get; set; } = false;
        public string Language { get; set; } = "es";
        
        
      
    }
}
