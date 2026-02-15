using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using GTAVInjector.Models;

namespace GTAVInjector.Core
{
    public static class SettingsManager
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TessioLauncher"
        );

        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "config.conf");

        public static AppSettings Settings { get; private set; } = new AppSettings();

        private static AppSettings? _loadedSettings = null;

        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    LoadFromConfigFile();
                    _loadedSettings = CloneSettings(Settings);
                }
                else
                {
                    var oldJsonPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "GTA GGS Launcher",
                        "settings.json"
                    );

                    if (File.Exists(oldJsonPath))
                    {
                        MigrateFromJson(oldJsonPath);
                        SaveSettings(true);
                    }
                    else
                    {
                        Settings = new AppSettings();
                        SaveSettings(true);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                Settings = new AppSettings();
            }
        }

        private static void LoadFromConfigFile()
        {
            var lines = File.ReadAllLines(SettingsPath);
            Settings = new AppSettings();
            var currentSection = "";

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    continue;
                }

                var parts = trimmedLine.Split('=', 2);
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (currentSection)
                {
                    case "GameSettings":
                        ParseGameSettings(key, value);
                        break;
                    case "Interface":
                        ParseInterfaceSettings(key, value);
                        break;
                    case "DLLPaths":
                        ParseDllSettings(key, value);
                        break;
                }
            }
        }

        private static void ParseGameSettings(string key, string value)
        {
            switch (key)
            {
                case "GameType":
                    if (Enum.TryParse<GameType>(value, out var gameType))
                        Settings.GameType = gameType;
                    break;
                case "LauncherType":
                    if (Enum.TryParse<LauncherType>(value, out var launcherType))
                        Settings.LauncherType = launcherType;
                    break;
            }
        }

        private static void ParseInterfaceSettings(string key, string value)
        {
            switch (key)
            {
                case "Language":
                    Settings.Language = value;
                    break;
                case "AutoInject":
                    if (bool.TryParse(value, out var autoInject))
                        Settings.AutoInject = autoInject;
                    break;
                case "LaunchDelay":
                    if (int.TryParse(value, out var launchDelay))
                        Settings.LaunchDelay = launchDelay;
                    break;
            }
        }

        private static void ParseDllSettings(string key, string value)
        {
            if (key.EndsWith("_Path"))
            {
                var dllNumber = ExtractDllNumber(key);
                EnsureDllEntry(dllNumber);
                var dllEntry = Settings.DllEntries[dllNumber - 1];
                dllEntry.Path = value;
                dllEntry.FileName = Path.GetFileName(value);
                dllEntry.Status = "No inyectado";
            }
            else if (key.EndsWith("_Enabled"))
            {
                var dllNumber = ExtractDllNumber(key);
                EnsureDllEntry(dllNumber);
                if (bool.TryParse(value, out var enabled))
                    Settings.DllEntries[dllNumber - 1].Enabled = enabled;
            }
        }

        private static int ExtractDllNumber(string key)
        {
            var numberPart = key.Substring(3, key.IndexOf('_') - 3);
            return int.TryParse(numberPart, out var number) ? number : 1;
        }

        private static void EnsureDllEntry(int number)
        {
            while (Settings.DllEntries.Count < number)
            {
                Settings.DllEntries.Add(new DllEntry());
            }
        }

        private static void MigrateFromJson(string jsonPath)
        {
            try
            {
                var json = File.ReadAllText(jsonPath);
                Settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                System.Diagnostics.Debug.WriteLine("Configuración migrada desde JSON a .conf");
            }
            catch
            {
                Settings = new AppSettings();
            }
        }

        public static void SaveSettings()
        {
            SaveSettings(false);
        }

        public static void SaveSettings(bool force = false)
        {
            try
            {
                if (!force && !HasSettingsChanged())
                {
                    System.Diagnostics.Debug.WriteLine("No hay cambios en la configuración - saltando guardado");
                    return;
                }

                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                var configContent = CreateConfigContent();
                File.WriteAllText(SettingsPath, configContent);

                _loadedSettings = CloneSettings(Settings);

                System.Diagnostics.Debug.WriteLine($"Configuración guardada en: {SettingsPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private static string CreateConfigContent()
        {
            var lines = new List<string>
            {
                "# Tessio Launcher Configuration File",
                "# Generated automatically - Do not edit manually unless you know what you're doing",
                $"# Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                "",
                "[GameSettings]",
                $"GameType={Settings.GameType}",
                $"LauncherType={Settings.LauncherType}",
                "",
                "[Interface]",
                $"Language={Settings.Language}",
                $"AutoInject={Settings.AutoInject}",
                $"LaunchDelay={Settings.LaunchDelay}",
                "",
                "[DLLPaths]"
            };

            for (int i = 0; i < Settings.DllEntries.Count; i++)
            {
                var dll = Settings.DllEntries[i];
                lines.Add($"DLL{i + 1}_Path={dll.Path}");
                lines.Add($"DLL{i + 1}_Enabled={dll.Enabled}");
            }

            lines.Add("");
            lines.Add("[System]");
            lines.Add($"LastModified={DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            return string.Join(Environment.NewLine, lines);
        }

        private static AppSettings CloneSettings(AppSettings settings)
        {
            return new AppSettings
            {
                GameType = settings.GameType,
                LauncherType = settings.LauncherType,
                AutoInject = settings.AutoInject,
                Language = settings.Language,
                LaunchDelay = settings.LaunchDelay,
                DllEntries = settings.DllEntries.Select(dll => new DllEntry
                {
                    Path = dll.Path,
                    FileName = dll.FileName,
                    Enabled = dll.Enabled,
                    Status = dll.Status
                }).ToList()
            };
        }

        private static bool HasSettingsChanged()
        {
            if (_loadedSettings == null)
                return true;

            if (Settings.GameType != _loadedSettings.GameType ||
                Settings.LauncherType != _loadedSettings.LauncherType ||
                Settings.AutoInject != _loadedSettings.AutoInject ||
                Settings.Language != _loadedSettings.Language ||
                Settings.LaunchDelay != _loadedSettings.LaunchDelay)
                return true;

            if (Settings.DllEntries.Count != _loadedSettings.DllEntries.Count)
                return true;

            for (int i = 0; i < Settings.DllEntries.Count; i++)
            {
                var current = Settings.DllEntries[i];
                var loaded = _loadedSettings.DllEntries[i];

                if (current.Path != loaded.Path || current.Enabled != loaded.Enabled)
                    return true;
            }

            return false;
        }
    }
}