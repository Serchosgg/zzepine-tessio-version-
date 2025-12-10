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
            
          
        }

       
    }
}
