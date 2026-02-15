using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using Microsoft.Win32;

namespace GTAVInjector.Core
{
    /// <summary>
    /// Validador de requisitos del sistema
    /// </summary>
    public static class SystemRequirementsValidator
    {
        /// <summary>
        /// Valida todos los requisitos del sistema
        /// </summary>
        public static SystemRequirementsResult ValidateAllRequirements()
        {
            var result = new SystemRequirementsResult
            {
                IsAdministrator = CheckAdministratorRights(),
                HasVCRedist2015_2022_x86 = CheckVCRedist(false),
                HasVCRedist2015_2022_x64 = CheckVCRedist(true),
                HasGTAVInstalled = CheckGTAVInstallation(),
                IsWindows10OrNewer = CheckWindowsVersion(),
                HasDotNet8Runtime = CheckDotNetRuntime()
            };

            // 🔥 REQUISITOS CRÍTICOS (bloqueantes): Solo Administrador
            result.HasCriticalRequirements = result.IsAdministrator;

            // 🔥 REQUISITOS OPCIONALES (advertencias): VC++, GTA V, Windows
            result.AllRequirementsMet = result.IsAdministrator &&
                                       result.HasVCRedist2015_2022_x86 &&
                                       result.HasVCRedist2015_2022_x64 &&
                                       result.HasGTAVInstalled &&
                                       result.IsWindows10OrNewer;

            return result;
        }

        /// <summary>
        /// Verifica si la aplicación se está ejecutando como administrador
        /// </summary>
        private static bool CheckAdministratorRights()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

                    Debug.WriteLine($"[REQUIREMENTS] Permisos de administrador: {(isAdmin ? "✅ SÍ" : "❌ NO")}");
                    return isAdmin;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REQUIREMENTS] Error verificando permisos: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si Visual C++ Redistributable 2015-2022 está instalado
        /// </summary>
        private static bool CheckVCRedist(bool x64)
        {
            try
            {
                string[] registryPaths = x64
                    ? new[]
                    {
                        @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64",
                        @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"
                    }
                    : new[]
                    {
                        @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86",
                        @"SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x86"
                    };

                foreach (var path in registryPaths)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(path))
                    {
                        if (key != null)
                        {
                            var installed = key.GetValue("Installed");
                            if (installed != null && installed.ToString() == "1")
                            {
                                var version = key.GetValue("Version");
                                Debug.WriteLine($"[REQUIREMENTS] VC++ Redist {(x64 ? "x64" : "x86")}: ✅ Instalado (Versión: {version})");
                                return true;
                            }
                        }
                    }
                }

                // Verificación adicional para versiones más recientes (2015-2022)
                string[] vcRedistNames = x64
                    ? new[] { "vcruntime140.dll", "msvcp140.dll" }
                    : new[] { "vcruntime140.dll", "msvcp140.dll" };

                string systemPath = x64
                    ? Environment.GetFolderPath(Environment.SpecialFolder.System)
                    : Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

                bool allDllsExist = vcRedistNames.All(dll => File.Exists(Path.Combine(systemPath, dll)));

                if (allDllsExist)
                {
                    Debug.WriteLine($"[REQUIREMENTS] VC++ Redist {(x64 ? "x64" : "x86")}: ✅ Detectado por DLLs del sistema");
                    return true;
                }

                Debug.WriteLine($"[REQUIREMENTS] VC++ Redist {(x64 ? "x64" : "x86")}: ❌ NO instalado");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REQUIREMENTS] Error verificando VC++ Redist {(x64 ? "x64" : "x86")}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si GTA V está instalado
        /// </summary>
        private static bool CheckGTAVInstallation()
        {
            try
            {
                // Verificar mediante registro (Rockstar)
                string[] registryPaths = new[]
                {
                    @"SOFTWARE\WOW6432Node\Rockstar Games\Grand Theft Auto V",
                    @"SOFTWARE\Rockstar Games\Grand Theft Auto V",
                    @"SOFTWARE\WOW6432Node\Rockstar Games\GTAV Enhanced",
                    @"SOFTWARE\Rockstar Games\GTAV Enhanced"
                };

                foreach (var path in registryPaths)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(path))
                    {
                        if (key != null)
                        {
                            var installPath = key.GetValue("InstallFolder")?.ToString();
                            if (!string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
                            {
                                if (File.Exists(Path.Combine(installPath, "GTA5.exe")))
                                {
                                    Debug.WriteLine($"[REQUIREMENTS] GTA V: ✅ Instalado (Ruta: {installPath})");
                                    return true;
                                }
                            }
                        }
                    }
                }

                // Verificar rutas comunes
                string[] commonPaths = new[]
                {
                    @"C:\Program Files\Rockstar Games\Grand Theft Auto V\GTA5.exe",
                    @"C:\Program Files (x86)\Rockstar Games\Grand Theft Auto V\GTA5.exe",
                    @"D:\Program Files\Rockstar Games\Grand Theft Auto V\GTA5.exe",
                    @"E:\Program Files\Rockstar Games\Grand Theft Auto V\GTA5.exe",
                    @"C:\Program Files\Epic Games\GTAV\GTA5.exe",
                    @"D:\Program Files\Epic Games\GTAV\GTA5.exe",
                    @"D:\SteamLibrary\steamapps\common\Grand Theft Auto V\GTA5.exe",
                    @"C:\Program Files (x86)\Steam\steamapps\common\Grand Theft Auto V\GTA5.exe",
                    @"C:\Program Files\Steam\steamapps\common\Grand Theft Auto V\GTA5.exe"
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        Debug.WriteLine($"[REQUIREMENTS] GTA V: ✅ Encontrado en {path}");
                        return true;
                    }
                }

                Debug.WriteLine("[REQUIREMENTS] GTA V: ❌ NO encontrado");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REQUIREMENTS] Error verificando GTA V: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si es Windows 10 o superior
        /// </summary>
        private static bool CheckWindowsVersion()
        {
            try
            {
                var osVersion = Environment.OSVersion;
                bool isWin10OrNewer = osVersion.Platform == PlatformID.Win32NT &&
                                     osVersion.Version.Major >= 10;

                Debug.WriteLine($"[REQUIREMENTS] Windows: {osVersion.VersionString} - {(isWin10OrNewer ? "✅ Compatible" : "❌ Requiere Windows 10+")}");
                return isWin10OrNewer;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REQUIREMENTS] Error verificando Windows: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si .NET 8.0 Runtime está instalado
        /// </summary>
        private static bool CheckDotNetRuntime()
        {
            try
            {
                // Si la aplicación está ejecutándose, significa que .NET está instalado
                var version = Environment.Version;
                bool isDotNet8 = version.Major >= 8;

                Debug.WriteLine($"[REQUIREMENTS] .NET Runtime: {version} - {(isDotNet8 ? "✅ Compatible" : "⚠️ Versión antigua")}");
                return isDotNet8;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REQUIREMENTS] Error verificando .NET: {ex.Message}");
                return true; // Si está ejecutando, asumimos que está instalado
            }
        }

        /// <summary>
        /// Obtiene mensaje de error detallado según requisitos faltantes
        /// </summary>
        public static string GetRequirementsErrorMessage(SystemRequirementsResult result, string language = "es")
        {
            if (result.AllRequirementsMet)
                return string.Empty;

            var errors = new System.Collections.Generic.List<string>();

            if (language.ToLower() == "es")
            {
                if (!result.IsAdministrator)
                    errors.Add("❌ Permisos de Administrador requeridos");
                if (!result.HasVCRedist2015_2022_x86)
                    errors.Add("❌ Visual C++ Redistributable 2015-2022 (x86) no instalado");
                if (!result.HasVCRedist2015_2022_x64)
                    errors.Add("❌ Visual C++ Redistributable 2015-2022 (x64) no instalado");
                if (!result.HasGTAVInstalled)
                    errors.Add("❌ GTA V no encontrado en el sistema");
                if (!result.IsWindows10OrNewer)
                    errors.Add("❌ Se requiere Windows 10 o superior");
            }
            else
            {
                if (!result.IsAdministrator)
                    errors.Add("❌ Administrator permissions required");
                if (!result.HasVCRedist2015_2022_x86)
                    errors.Add("❌ Visual C++ Redistributable 2015-2022 (x86) not installed");
                if (!result.HasVCRedist2015_2022_x64)
                    errors.Add("❌ Visual C++ Redistributable 2015-2022 (x64) not installed");
                if (!result.HasGTAVInstalled)
                    errors.Add("❌ GTA V not found on the system");
                if (!result.IsWindows10OrNewer)
                    errors.Add("❌ Windows 10 or newer required");
            }

            return string.Join("\n", errors);
        }

        /// <summary>
        /// Abre la página de descarga de VC++ Redistributable
        /// </summary>
        public static void OpenVCRedistDownload()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://aka.ms/vs/17/release/vc_redist.x64.exe",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[REQUIREMENTS] Error abriendo descarga VC++: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Resultado de validación de requisitos del sistema
    /// </summary>
    public class SystemRequirementsResult
    {
        public bool IsAdministrator { get; set; }
        public bool HasVCRedist2015_2022_x86 { get; set; }
        public bool HasVCRedist2015_2022_x64 { get; set; }
        public bool HasGTAVInstalled { get; set; }
        public bool IsWindows10OrNewer { get; set; }
        public bool HasDotNet8Runtime { get; set; }
        public bool AllRequirementsMet { get; set; }

        /// <summary>
        /// Indica si los requisitos CRÍTICOS están cumplidos (solo Administrador)
        /// </summary>
        public bool HasCriticalRequirements { get; set; }

        /// <summary>
        /// Obtiene un resumen de los requisitos cumplidos
        /// </summary>
        public string GetSummary()
        {
            int metCount = 0;
            int totalCount = 5;

            if (IsAdministrator) metCount++;
            if (HasVCRedist2015_2022_x86) metCount++;
            if (HasVCRedist2015_2022_x64) metCount++;
            if (HasGTAVInstalled) metCount++;
            if (IsWindows10OrNewer) metCount++;

            return $"{metCount}/{totalCount} requisitos cumplidos";
        }

        /// <summary>
        /// Obtiene un resumen con categorización de requisitos
        /// </summary>
        public string GetDetailedSummary(string language = "es")
        {
            if (language.ToLower() == "es")
            {
                if (AllRequirementsMet)
                    return "✅ Todos los requisitos cumplidos";

                if (HasCriticalRequirements)
                    return "⚠️ Algunos requisitos opcionales no se cumplen";

                return "❌ Requisitos críticos no cumplidos";
            }
            else
            {
                if (AllRequirementsMet)
                    return "✅ All requirements met";

                if (HasCriticalRequirements)
                    return "⚠️ Some optional requirements not met";

                return "❌ Critical requirements not met";
            }
        }
    }
}