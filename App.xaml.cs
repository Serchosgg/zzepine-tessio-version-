using System.Threading.Tasks;
using System.Windows;

namespace GTAVInjector
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Cargar configuración
            Core.SettingsManager.LoadSettings();
            
            // Verificar que la configuración se haya cargado correctamente
            if (Core.SettingsManager.Settings == null)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: Settings es null después de LoadSettings()");
                return;
            }
            
            // Cargar idioma guardado
            Core.LocalizationManager.SetLanguage(Core.SettingsManager.Settings.Language);
            
            // Iniciar verificación de versiones al arrancar la aplicación
            _ = InitializeVersionCheckerAsync();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // No forzar guardado al salir - solo si hay cambios pendientes
            // Los cambios ya se guardan automáticamente cuando el usuario los hace
            base.OnExit(e);
        }

        private async Task InitializeVersionCheckerAsync()
        {
            try
            {
                // Realizar primera verificación al iniciar
                await Core.VersionChecker.CheckForUpdatesAsync();
                
                // Iniciar monitoreo continuo (cada 10 segundos)
                await Core.VersionChecker.StartVersionMonitoring((isOutdated) =>
                {
                    // Callback cuando cambia el estado de la versión
                    System.Diagnostics.Debug.WriteLine($"📱 Estado de versión actualizado - Desactualizada: {isOutdated}");
                });
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error al inicializar VersionChecker: {ex.Message}");
            }
        }
    }
}
