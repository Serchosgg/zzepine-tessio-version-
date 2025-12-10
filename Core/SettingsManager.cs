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

        public static AppSettings Settings { get; private set; } = null!;
        
        // Copia de los settings cargados para detectar cambios
        private static AppSettings? _loadedSettings = null;

        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    LoadFromConfigFile();
                    // Crear copia de los settings cargados para detectar cambios
                    _loadedSettings = CloneSettings(Settings);
                }
                else
                {
                    // Buscar archivo JSON antiguo para migración
                    var oldJsonPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "GTA GGS Launcher",
                        "settings.json"
                    );
                    
                    if (File.Exists(oldJsonPath))
                    {
                        MigrateFromJson(oldJsonPath);
                        // Solo guardar cuando se migra desde JSON antiguo
                        SaveSettings(true); // Forzar guardado en migración
                    }
                    else
                    {
                        Settings = new AppSettings();
                        // Solo crear el archivo si no existe (primera vez)
                        SaveSettings(true); // Forzar guardado en primera creación
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
                
                // Ignorar comentarios y líneas vacías
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                    continue;
                
                // Detectar secciones
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    continue;
                }
                
                // Procesar configuraciones
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
                // Establecer también el FileName desde el Path
                dllEntry.FileName = Path.GetFileName(value);
                // Establecer status por defecto
                dllEntry.Status = "No inyectado"; // TODO: usar LocalizationManager aquí si es necesario
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
            SaveSettings(false); // Por defecto, verificar cambios
        }
        
        public static void SaveSettings(bool force = false)
        {
            try
            {
                // Debug: mostrar quién está llamando a SaveSettings
                var stackTrace = new System.Diagnostics.StackTrace(true);
                var callerMethod1 = stackTrace.GetFrame(1)?.GetMethod()?.Name ?? "Unknown";
                var callerMethod2 = stackTrace.GetFrame(2)?.GetMethod()?.Name ?? "Unknown";
                var callerMethod3 = stackTrace.GetFrame(3)?.GetMethod()?.Name ?? "Unknown";
                System.Diagnostics.Debug.WriteLine($"[SAVE DEBUG] Stack: {callerMethod3} -> {callerMethod2} -> {callerMethod1}");
                
                // Si no es forzado y no hay cambios, no guardar
                if (!force && !HasSettingsChanged())
                {
                    System.Diagnostics.Debug.WriteLine("No hay cambios en la configuración - saltando guardado");
                    return;
                }
                
                System.Diagnostics.Debug.WriteLine($"[SAVE DEBUG] GUARDANDO - LauncherType: {Settings.LauncherType}, Language: {Settings.Language}");
                
                // Crear directorio si no existe
                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                // Crear contenido del archivo .conf
                var configContent = CreateConfigContent();
                File.WriteAllText(SettingsPath, configContent);
                
                // Actualizar la copia de configuración cargada
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
                "",
                "[DLLPaths]"
            };
            
            // Agregar rutas de DLLs
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
            {
                System.Diagnostics.Debug.WriteLine($"[CHANGE DEBUG] No hay configuración previa - forzando guardado");
                return true; // Si no hay configuración previa, hay cambios
            }
            
            // Comparar propiedades principales
            if (Settings.GameType != _loadedSettings.GameType)
            {
                System.Diagnostics.Debug.WriteLine($"[CHANGE DEBUG] GameType: {_loadedSettings.GameType} -> {Settings.GameType}");
                return true;
            }
            if (Settings.LauncherType != _loadedSettings.LauncherType)
            {
                System.Diagnostics.Debug.WriteLine($"[CHANGE DEBUG] LauncherType: {_loadedSettings.LauncherType} -> {Settings.LauncherType}");
                return true;
            }
            if (Settings.AutoInject != _loadedSettings.AutoInject)
            {
                System.Diagnostics.Debug.WriteLine($"[CHANGE DEBUG] AutoInject: {_loadedSettings.AutoInject} -> {Settings.AutoInject}");
                return true;
            }
            if (Settings.Language != _loadedSettings.Language)
            {
                System.Diagnostics.Debug.WriteLine($"[CHANGE DEBUG] Language: {_loadedSettings.Language} -> {Settings.Language}");
                return true;
            }
            
            // Comparar DLL entries
            if (Settings.DllEntries.Count != _loadedSettings.DllEntries.Count)
            {
                return true;
            }
            
            for (int i = 0; i < Settings.DllEntries.Count; i++)
            {
                var current = Settings.DllEntries[i];
                var loaded = _loadedSettings.DllEntries[i];
                
                if (current.Path != loaded.Path ||
                    current.Enabled != loaded.Enabled)
                {
                    return true;
                }
            }
            
            return false; // No hay cambios
        }
    }
}
