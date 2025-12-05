using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GTAVInjector.Core;
using GTAVInjector.Models;
using Microsoft.Win32;

namespace GTAVInjector
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<DllEntry> DllEntries { get; set; }
        private System.Windows.Threading.DispatcherTimer? _gameCheckTimer;
        private System.Windows.Threading.DispatcherTimer? _autoInjectTimer;
        private bool _gameWasRunning = false;
        private bool _autoInjectionCompleted = false;

        public MainWindow()
        {
            InitializeComponent();
            DllEntries = new ObservableCollection<DllEntry>();
            DllListView.ItemsSource = DllEntries;
            
            LoadSettings();
            InitializeTimers();
            CheckForUpdates();
            StartVersionMonitoring();
            
            // Mover la llamada a UpdateUI() al evento Loaded para asegurar que los controles estén inicializados
            Loaded += (s, e) => 
            {
                UpdateUI();
                UpdateVersionText();
                // Delay para asegurar que la UI esté completamente renderizada
                this.Dispatcher.BeginInvoke(new Action(() => {
                    StartParallaxAnimation();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;
            
            // Cargar tipo de juego
            if (settings.GameType == GameType.Legacy)
                LegacyRadio.IsChecked = true;
            else
                EnhancedRadio.IsChecked = true;
            
            // Cargar launcher
            switch (settings.LauncherType)
            {
                case LauncherType.Rockstar:
                    RockstarRadio.IsChecked = true;
                    break;
                case LauncherType.EpicGames:
                    EpicRadio.IsChecked = true;
                    break;
                case LauncherType.Steam:
                    SteamRadio.IsChecked = true;
                    break;
            }
            
            // Cargar DLLs
            foreach (var dll in settings.DllEntries)
            {
                DllEntries.Add(dll);
            }
            
            // Cargar auto-inject
            AutoInjectCheckbox.IsChecked = settings.AutoInject;
            
            // Iniciar timer de auto-inject si está habilitado
            if (settings.AutoInject)
            {
                System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Habilitado en configuración - iniciando timer");
                _autoInjectionCompleted = false; // Resetear estado al cargar
                _autoInjectTimer?.Start();
            }
            
            // Cargar idioma
            var langTag = settings.Language;
            foreach (System.Windows.Controls.ComboBoxItem item in LanguageSelector.Items)
            {
                if (item.Tag?.ToString() == langTag)
                {
                    LanguageSelector.SelectedItem = item;
                    break;
                }
            }
        }

        private void InitializeTimers()
        {
            // Timer para verificar estado del juego
            _gameCheckTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _gameCheckTimer.Tick += (s, e) => UpdateGameStatus();
            _gameCheckTimer.Start();
            
            // Timer para auto-inyección
            _autoInjectTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2) // Reducir intervalo para mejor responsividad
            };
            _autoInjectTimer.Tick += AutoInjectTimer_Tick;
            
            // Iniciar timer si auto-inject ya está habilitado
            if (SettingsManager.Settings.AutoInject)
            {
                _autoInjectTimer.Start();
                System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Timer iniciado en InitializeTimers");
            }
        }

        private async void CheckForUpdates()
        {
            try
            {
                VersionStatusText.Text = LocalizationManager.GetString("CheckingUpdates");
                var updateAvailable = await VersionChecker.CheckForUpdatesAsync();
                
                UpdateVersionStatus(updateAvailable);
            }
            catch
            {
                VersionStatusText.Text = LocalizationManager.GetString("UpdateCheckFailed");
                VersionStatusText.Foreground = System.Windows.Media.Brushes.Gray;
            }
        }

        private void UpdateVersionText()
        {
            if (VersionText != null)
            {
                VersionText.Text = $"v{VersionChecker.GetCurrentVersion()}";
            }
        }

        private void UpdateVersionStatus(bool isOutdated)
        {
            if (isOutdated)
            {
                VersionStatusText.Text = $"DESACTUALIZADO - v{VersionChecker.GetCurrentVersion()} → v{VersionChecker.GetLatestVersion()}";
                VersionStatusText.Foreground = System.Windows.Media.Brushes.Red;
                UpdateButton.Visibility = Visibility.Visible;
                UpdateButton.Content = "Ir a Discord para Actualizar";
                ChangelogButton.Visibility = Visibility.Collapsed;
                
                // DESHABILITAR LOS 3 BOTONES PRINCIPALES
                LaunchButton.IsEnabled = false;
                InjectButton.IsEnabled = false;
                KillButton.IsEnabled = false;
            }
            else
            {
                VersionStatusText.Text = $"ACTUALIZADO - v{VersionChecker.GetCurrentVersion()}";
                VersionStatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                UpdateButton.Visibility = Visibility.Collapsed;
                ChangelogButton.Visibility = Visibility.Visible;
                
                // Rehabilitar botones según estado del juego
                UpdateGameStatus();
            }
        }

        private async void StartVersionMonitoring()
        {
            await VersionChecker.StartVersionMonitoring(isOutdated =>
            {
                Dispatcher.Invoke(() => UpdateVersionStatus(isOutdated));
            });
        }

        private void UpdateGameStatus()
        {
            bool isRunning = InjectionManager.IsGameRunning();
            bool isOutdated = VersionChecker.IsOutdated();
            
            if (isRunning)
            {
                GameStatusText.Text = LocalizationManager.GetString("GameRunning");
                GameStatusText.Foreground = System.Windows.Media.Brushes.LimeGreen;
                
                // Solo habilitar botones si no está desactualizado
                LaunchButton.IsEnabled = false;
                InjectButton.IsEnabled = !isOutdated;
                KillButton.IsEnabled = !isOutdated;
                
                // Si el juego no estaba corriendo antes y ahora sí, resetear auto-inject
                if (!_gameWasRunning)
                {
                    _autoInjectionCompleted = false;
                    System.Diagnostics.Debug.WriteLine("Juego iniciado - Estado de auto-inyección reseteado para nueva sesión");
                }
                
                _gameWasRunning = true;
            }
            else
            {
                GameStatusText.Text = LocalizationManager.GetString("GameNotRunning");
                GameStatusText.Foreground = System.Windows.Media.Brushes.Red;
                
                // Solo habilitar botones si no está desactualizado
                LaunchButton.IsEnabled = !isOutdated;
                InjectButton.IsEnabled = false;
                KillButton.IsEnabled = false;
                
                // Si el juego estaba ejecutándose antes y ahora no, resetear el estado
                if (_gameWasRunning)
                {
                    _autoInjectionCompleted = false;
                    _gameWasRunning = false;
                    
                    // Resetear estados de inyección
                    foreach (var dll in DllEntries)
                    {
                        dll.Status = LocalizationManager.GetString("NotInjected");
                    }
                    
                    System.Diagnostics.Debug.WriteLine("Juego cerrado - Estado de auto-inyección reseteado");
                }
            }
        }

        private async void AutoInjectTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                // Debug logging mejorado
                bool gameRunning = InjectionManager.IsGameRunning();
                bool autoInjectEnabled = SettingsManager.Settings.AutoInject;
                
                System.Diagnostics.Debug.WriteLine($"[AUTO-INJECT] Tick - Habilitado: {autoInjectEnabled}, Juego: {gameRunning}, Completado: {_autoInjectionCompleted}, GameWasRunning: {_gameWasRunning}");
                
                if (!autoInjectEnabled)
                {
                    System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Deshabilitado - saliendo del timer");
                    return;
                }
                
                if (!gameRunning)
                {
                    System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Juego no ejecutándose - saliendo del timer");
                    return;
                }
                
                // Verificar si hay DLLs habilitadas
                var enabledDlls = DllEntries.Where(d => d.Enabled).ToList();
                if (!enabledDlls.Any())
                {
                    System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] No hay DLLs habilitadas - saliendo del timer");
                    return;
                }
                
                // Verificar si hay DLLs habilitadas no inyectadas
                var notInjected = enabledDlls.Where(d => 
                    d.Status == LocalizationManager.GetString("NotInjected") ||
                    d.Status.StartsWith("Error:")).ToList();
                
                System.Diagnostics.Debug.WriteLine($"[AUTO-INJECT] DLLs habilitadas: {enabledDlls.Count}, No inyectadas: {notInjected.Count}");
                
                // Si hay DLLs no inyectadas, intentar inyectar independientemente del estado de completado
                if (notInjected.Any())
                {
                    System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Iniciando inyección automática...");
                    StatusText.Text = LocalizationManager.GetString("AutoInjecting");
                    
                    // Esperar a que el juego cargue completamente
                    await Task.Delay(2000);
                    
                    // Solo inyectar si el juego sigue ejecutándose después del delay
                    if (InjectionManager.IsGameRunning())
                    {
                        await InjectDllsAsync();
                        
                        // Verificar resultados después de la inyección
                        var stillNotInjected = enabledDlls.Where(d => 
                            d.Status == LocalizationManager.GetString("NotInjected") ||
                            d.Status.StartsWith("Error:")).ToList();
                        
                        if (!stillNotInjected.Any())
                        {
                            _autoInjectionCompleted = true;
                            System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] ✅ Todas las DLLs inyectadas exitosamente");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[AUTO-INJECT] ⚠️ {stillNotInjected.Count} DLLs aún no inyectadas, reintentará en próximo ciclo");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Juego cerrado durante el delay - cancelando inyección");
                    }
                }
                else
                {
                    if (!_autoInjectionCompleted)
                    {
                        _autoInjectionCompleted = true;
                        System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] ✅ Todas las DLLs ya están inyectadas - marcando como completado");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTO-INJECT] ❌ Error: {ex.Message}");
            }
        }

        private void UpdateUI()
        {
            try
            {
                // Verificar que los controles no sean nulos antes de acceder a ellos
                if (DllListTitle != null) DllListTitle.Text = LocalizationManager.GetString("DllList");
                if (AddDllButton != null) AddDllButton.Content = LocalizationManager.GetString("AddDll");
                if (AutoInjectCheckbox != null) AutoInjectCheckbox.Content = LocalizationManager.GetString("AutoInject");
                if (LaunchButton != null) LaunchButton.Content = LocalizationManager.GetString("LaunchGame");
                if (InjectButton != null) InjectButton.Content = LocalizationManager.GetString("InjectDlls");
                if (KillButton != null) KillButton.Content = LocalizationManager.GetString("KillGame");
                if (GameTypeTitle != null) GameTypeTitle.Text = LocalizationManager.GetString("GameType");
                if (LauncherTitle != null) LauncherTitle.Text = LocalizationManager.GetString("Launcher");
                if (RequirementsTitle != null) RequirementsTitle.Text = LocalizationManager.GetString("Requirements");
                if (VersionStatusTitle != null) VersionStatusTitle.Text = LocalizationManager.GetString("VersionStatus");
                if (UpdateButton != null) UpdateButton.Content = LocalizationManager.GetString("UpdateAvailable");
                if (ChangelogButton != null) ChangelogButton.Content = LocalizationManager.GetString("ViewChangelog");
                
                // Actualizar textos de requisitos
                if (VcRequirementText != null) VcRequirementText.Text = LocalizationManager.GetString("VcRequirement");
                if (GtaRequirementText != null) GtaRequirementText.Text = LocalizationManager.GetString("GtaRequirement");
                if (AdminRequirementText != null) AdminRequirementText.Text = LocalizationManager.GetString("AdminRequirement");
                
                // Actualizar texto "Idioma"
                if (LanguageLabel != null) LanguageLabel.Text = LocalizationManager.GetString("Language");
                
                // Actualizar botones "Remove" en la lista de DLLs
                UpdateRemoveButtonsText();
                
                // Actualizar StatusText según idioma
                var currentLang = LocalizationManager.CurrentLanguage;
                if (currentLang.ToLower() == "es")
                {
                    if (StatusText != null && (StatusText.Text == "Ready" || StatusText.Text == "Listo"))
                        StatusText.Text = "Listo";
                }
                else
                {
                    if (StatusText != null && (StatusText.Text == "Listo" || StatusText.Text == "Ready"))
                        StatusText.Text = "Ready";
                }
            }
            catch (Exception ex)
            {
                // Registrar el error para depuración
                System.Diagnostics.Debug.WriteLine($"Error en UpdateUI: {ex.Message}");
            }
        }

        private void UpdateRemoveButtonsText()
        {
            try
            {
                // Obtener idioma directamente del ComboBox seleccionado
                string currentLang = "en";
                if (LanguageSelector?.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
                {
                    currentLang = selectedItem.Tag?.ToString() ?? "en";
                }
                
                var removeText = currentLang.ToLower() == "es" ? "Quitar" : "Remove";
                
                System.Diagnostics.Debug.WriteLine($"Idioma detectado: {currentLang}, Texto del botón: {removeText}");
                
                // Forzar regeneración completa del ListView
                if (DllListView != null && DllListView.ItemsSource != null)
                {
                    var items = DllListView.ItemsSource;
                    DllListView.ItemsSource = null;
                    
                    // Actualizar el texto por defecto en el XAML
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DllListView.ItemsSource = items;
                        
                        // Esperar a que se regeneren los items y luego actualizar
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            UpdateVisualRemoveButtons(removeText);
                        }), System.Windows.Threading.DispatcherPriority.Loaded);
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en UpdateRemoveButtonsText: {ex.Message}");
            }
        }

        private void UpdateVisualRemoveButtons(string text)
        {
            try
            {
                if (DllListView == null) return;
                
                for (int i = 0; i < DllListView.Items.Count; i++)
                {
                    var container = DllListView.ItemContainerGenerator.ContainerFromIndex(i) as System.Windows.Controls.ListViewItem;
                    if (container != null)
                    {
                        var textBlock = FindVisualChild<System.Windows.Controls.TextBlock>(container, "RemoveButtonText");
                        if (textBlock != null)
                        {
                            textBlock.Text = text;
                            System.Diagnostics.Debug.WriteLine($"Botón {i} actualizado a: {text}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en UpdateVisualRemoveButtons: {ex.Message}");
            }
        }

        private T? FindVisualChild<T>(System.Windows.DependencyObject parent, string name) where T : System.Windows.DependencyObject
        {
            try
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
                {
                    var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                    
                    if (child is T typedChild && (child as System.Windows.FrameworkElement)?.Name == name)
                    {
                        return typedChild;
                    }
                    
                    var result = FindVisualChild<T>(child, name);
                    if (result != null)
                        return result;
                }
            }
            catch
            {
                // Ignorar errores de búsqueda visual
            }
            
            return null;
        }

        private void AddDll_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*",
                Multiselect = true,
                Title = LocalizationManager.GetString("SelectDlls")
            };
            
            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!DllEntries.Any(d => d.Path == file))
                    {
                        DllEntries.Add(new DllEntry
                        {
                            Path = file,
                            FileName = System.IO.Path.GetFileName(file),
                            Enabled = true,
                            Status = LocalizationManager.GetString("NotInjected")
                        });
                    }
                }
                
                SettingsManager.Settings.DllEntries = DllEntries.ToList();
                SettingsManager.SaveSettings();
            }
        }

        private void RemoveDll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is DllEntry dll)
            {
                DllEntries.Remove(dll);
                SettingsManager.Settings.DllEntries = DllEntries.ToList();
                SettingsManager.SaveSettings();
            }
        }

        private async void LaunchGame_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = LocalizationManager.GetString("LaunchingGame");
                InjectionManager.LaunchGame();
                await Task.Delay(1000);
                StatusText.Text = LocalizationManager.GetString("GameLaunched");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = LocalizationManager.GetString("LaunchFailed");
            }
        }

        private async void InjectDlls_Click(object sender, RoutedEventArgs e)
        {
            await InjectDllsAsync();
        }

        private async Task InjectDllsAsync()
        {
            try
            {
                var enabledDlls = DllEntries.Where(d => d.Enabled).ToList();
                
                if (!enabledDlls.Any())
                {
                    MessageBox.Show(LocalizationManager.GetString("NoDllsEnabled"), 
                        "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                StatusText.Text = LocalizationManager.GetString("Injecting");
                
                int injected = 0;
                foreach (var dll in enabledDlls)
                {
                    var result = await Task.Run(() => InjectionManager.InjectDll(dll.Path));
                    
                    switch (result)
                    {
                        case InjectionResult.INJECT_OK:
                            MostrarEstado("Estado: TSV2 Cargado exitosamente.", "Inyectado", System.Windows.Media.Brushes.LimeGreen);
                            dll.Status = "Inyectado";
                            injected++;
                            break;
                        case InjectionResult.ERROR_OPEN_PROCESS:
                            MostrarEstado("Estado: No se pudo abrir el proceso de GTA5.", "No inyectado.", System.Windows.Media.Brushes.Red);
                            dll.Status = "Error: No se pudo abrir GTA5";
                            break;
                        case InjectionResult.ERROR_DLL_NOTFOUND:
                            MostrarEstado("Estado: No se encontró TessioScriptV2.dll.", "No inyectado.", System.Windows.Media.Brushes.Red);
                            dll.Status = "Error: DLL no encontrada";
                            break;
                        case InjectionResult.ERROR_ALLOC:
                            MostrarEstado("Estado: No se pudo asignar memoria remota (¿Battleye activado?).", "No inyectado.", System.Windows.Media.Brushes.Red);
                            dll.Status = "Error: Memoria no asignada";
                            break;
                        case InjectionResult.ERROR_WRITE:
                            MostrarEstado("Estado: Fallo al escribir en la memoria del juego.", "No inyectado.", System.Windows.Media.Brushes.Red);
                            dll.Status = "Error: Escritura fallida";
                            break;
                        case InjectionResult.ERROR_CREATE_THREAD:
                            MostrarEstado("Estado: No se pudo ejecutar el hilo remoto.", "No inyectado.", System.Windows.Media.Brushes.Red);
                            dll.Status = "Error: Hilo remoto fallido";
                            break;
                        default:
                            MostrarEstado("Fallo en la inyección.", "Error.", System.Windows.Media.Brushes.Yellow);
                            dll.Status = "Error: Fallo desconocido";
                            break;
                    }
                }
                
                StatusText.Text = $"Inyección completada: ({injected}/{enabledDlls.Count})";
                
                if (injected == enabledDlls.Count)
                {
                    MessageBox.Show($"Inyección exitosa: ({injected}/{enabledDlls.Count})", 
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Inyección falló";
            }
        }

        private void MostrarEstado(string mensaje, string estado, System.Windows.Media.Brush color)
        {
            StatusText.Text = mensaje;
            StatusText.Foreground = color;
            
            // También actualizar el estado del juego si es necesario
            GameStatusText.Text = estado;
            GameStatusText.Foreground = color;
        }

        private void KillGame_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InjectionManager.KillGame();
                StatusText.Text = LocalizationManager.GetString("GameKilled");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GameType_Changed(object sender, RoutedEventArgs e)
        {
            if (LegacyRadio.IsChecked == true)
                SettingsManager.Settings.GameType = GameType.Legacy;
            else
                SettingsManager.Settings.GameType = GameType.Enhanced;
            
            SettingsManager.SaveSettings();
        }

        private void Launcher_Changed(object sender, RoutedEventArgs e)
        {
            if (RockstarRadio.IsChecked == true)
                SettingsManager.Settings.LauncherType = LauncherType.Rockstar;
            else if (EpicRadio.IsChecked == true)
                SettingsManager.Settings.LauncherType = LauncherType.EpicGames;
            else if (SteamRadio.IsChecked == true)
                SettingsManager.Settings.LauncherType = LauncherType.Steam;
            
            SettingsManager.SaveSettings();
        }

        private void AutoInject_Changed(object sender, RoutedEventArgs e)
        {
            bool isEnabled = AutoInjectCheckbox.IsChecked == true;
            SettingsManager.Settings.AutoInject = isEnabled;
            SettingsManager.SaveSettings();
            
            if (isEnabled)
            {
                // Resetear estado cuando se activa manualmente
                _autoInjectionCompleted = false;
                _gameWasRunning = false; // Resetear para forzar nueva detección
                _autoInjectTimer?.Start();
                System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] ✅ Activado manualmente - timer iniciado y estado reseteado");
            }
            else
            {
                _autoInjectTimer?.Stop();
                _autoInjectionCompleted = false; // Resetear para próxima activación
                System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] ❌ Desactivado manualmente - timer detenido");
            }
        }

        private void LanguageSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (LanguageSelector.SelectedItem is System.Windows.Controls.ComboBoxItem item)
            {
                var lang = item.Tag?.ToString() ?? "en";
                LocalizationManager.SetLanguage(lang);
                SettingsManager.Settings.Language = lang;
                SettingsManager.SaveSettings();
                
                // Forzar actualización completa
                UpdateUI();
                
                // Forzar actualización específica de botones Remove después de un pequeño delay
                Dispatcher.BeginInvoke(new Action(() => {
                    UpdateRemoveButtonsText();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
                
                // Actualizar texto de StatusText según idioma (verificar que no sea null)
                if (StatusText != null)
                {
                    if (lang.ToLower() == "es")
                    {
                        if (StatusText.Text == "Ready" || StatusText.Text == "Listo")
                            StatusText.Text = "Listo";
                    }
                    else
                    {
                        if (StatusText.Text == "Listo" || StatusText.Text == "Ready")
                            StatusText.Text = "Ready";
                    }
                }
            }
        }

        private void ComboBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Abrir el dropdown del ComboBox cuando se hace clic en cualquier parte
            if (LanguageSelector != null && e.LeftButton == MouseButtonState.Pressed)
            {
                LanguageSelector.IsDropDownOpen = !LanguageSelector.IsDropDownOpen;
                e.Handled = true; // Evitar que el evento se propague
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Tu versión está desactualizada. ¿Quieres ir al Discord de TessioScript para obtener la actualización?",
                    "Actualización Disponible",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    VersionChecker.OpenDiscordUpdate();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir Discord: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Changelog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/Tessio/TessioScript-Launcher/releases",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir changelog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Solo permitir arrastrar la ventana, no maximizar
            DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        // Función de maximizar removida - ya no es necesaria

        private void Discord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://discord.gg/NH6pArJB",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir Discord: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TikTok_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://www.tiktok.com/@tessiogg",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir TikTok: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Twitch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://www.twitch.tv/tessiogg",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir Twitch: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void YouTube_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://www.youtube.com/@TessioScript",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir YouTube: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void StartParallaxAnimation()
        {
            try
            {
                // Buscar y comenzar la animación de parallax
                var storyboard = (System.Windows.Media.Animation.Storyboard)FindResource("ParallaxAnimation");
                if (storyboard != null)
                {
                    // Forzar inicio de la animación en este window
                    storyboard.Begin(this, true);
                    System.Diagnostics.Debug.WriteLine("Animación parallax iniciada correctamente");
                    
                    // Verificar que las capas estén visibles
                    if (ParallaxLayer1 != null && ParallaxLayer2 != null)
                    {
                        ParallaxLayer1.Visibility = Visibility.Visible;
                        ParallaxLayer2.Visibility = Visibility.Visible;
                        System.Diagnostics.Debug.WriteLine("Capas parallax configuradas como visibles");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No se pudo encontrar la animación ParallaxAnimation");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al iniciar animación parallax: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _gameCheckTimer?.Stop();
            _autoInjectTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
