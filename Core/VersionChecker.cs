using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace GTAVInjector.Core
{
    public static class VersionChecker
    {
        // URL del repositorio de GitHub para verificación de versiones
        private const string VERSION_JSON_URL = "https://raw.githubusercontent.com/Tessio/Translations/master/version_l.txt";
        private const string TESSIO_DISCORD_URL = "https://gtaggs.wirdland.xyz/discord";
        
        private static string? _currentVersion;

        private static string? _latestVersion;
        private static bool _isOutdated = false;
        private static readonly HttpClient _httpClient = new();

        private static string GetCurrentVersionFromFile()
        {
            if (_currentVersion == null)
            {
                try
                {
                    _currentVersion = File.ReadAllText("version.txt").Trim();
                }
                catch
                {
                    _currentVersion = "2.0.0"; // Fallback version
                }
            }
            return _currentVersion;
        }
        public static async Task<bool> CheckForUpdatesAsync()
        {
            try
            {
                // Obtener la versión desde el repositorio de GitHub
                var response = await _httpClient.GetStringAsync(VERSION_JSON_URL);
                _latestVersion = response.Trim();

                if (!string.IsNullOrEmpty(_latestVersion))
                {
                    // Comparar versiones - CORREGIDO: 
                    // Si la versión local es menor que la del sitio web, está desactualizada
                    // Si la versión local es igual o mayor, está actualizada
                    var current = new Version(GetCurrentVersionFromFile());
                    var latest = new Version(_latestVersion);

                    _isOutdated = current < latest; // CORREGIDO: era latest > current
                    return _isOutdated;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void OpenDiscordUpdate()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = TESSIO_DISCORD_URL,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to open Discord: {ex.Message}");
            }
        }

        public static string GetCurrentVersion()
        {
            return GetCurrentVersionFromFile();
        }

        public static string? GetLatestVersion()
        {
            return _latestVersion;
        }

        public static bool IsOutdated()
        {
            return _isOutdated;
        }

        // Timer para verificar constantemente las actualizaciones
        public static async Task StartVersionMonitoring(Action<bool> onVersionChanged)
        {
            var timer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    bool wasOutdated = _isOutdated;
                    await CheckForUpdatesAsync();
                    
                    if (wasOutdated != _isOutdated)
                    {
                        onVersionChanged?.Invoke(_isOutdated);
                    }
                }
                catch
                {
                    // Ignorar errores de red silenciosamente
                }
            }, null, TimeSpan.Zero, TimeSpan.FromMinutes(5)); // Verificar cada 5 minutos
        }
    }
}
