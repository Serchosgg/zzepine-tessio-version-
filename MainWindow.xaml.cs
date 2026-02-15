using GTAVInjector.Core;
using GTAVInjector.Models;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

// Alias para evitar conflicto con System.Windows.Controls.ValidationResult
using GameValidationResult = GTAVInjector.Core.ValidationResult;

namespace GTAVInjector
{
    public partial class MainWindow : Window
    {
        private const string TESSIO_DISCORD_URL = "https://gtaggs.wirdland.xyz/discord";

        public ObservableCollection<DllEntry> DllEntries { get; set; }
        private DispatcherTimer? _gameCheckTimer;
        private DispatcherTimer? _autoInjectTimer;
        private DispatcherTimer? _validationTimer;
        private bool _gameWasRunning = false;
        private bool _autoInjectionCompleted = false;
        private bool _isLoadingSettings = false;

        private int _launchDelay = 5;
        private bool _isUpdatingDelayControls = false;
        private DateTime _gameStartTime = DateTime.MinValue;

        private readonly DispatcherTimer versionCheckTimer = new DispatcherTimer();
        private string currentLocalVersion = "1.1.0";
        private bool _updateAvailable = false;
        private GameValidationResult? _currentValidation = null;

        public MainWindow()
        {
            _isLoadingSettings = true;
            InitializeComponent();

            // 🔥 CARGAR SETTINGS PRIMERO (para tener el idioma)
            SettingsManager.LoadSettings();
            LocalizationManager.SetLanguage(SettingsManager.Settings.Language);

            // 🔥 VALIDAR REQUISITOS DEL SISTEMA AL INICIO
            var requirementsResult = SystemRequirementsValidator.ValidateAllRequirements();

            // 🔥 MOSTRAR VENTANA si NO todos los requisitos están cumplidos
            if (!requirementsResult.AllRequirementsMet)
            {
                // Abrir ventana de requisitos
                var requirementsWindow = new RequirementsWindow(requirementsResult, LocalizationManager.CurrentLanguage);
                var dialogResult = requirementsWindow.ShowDialog();

                // Si el usuario canceló o cerró la ventana, verificar si puede continuar
                if (dialogResult != true && !requirementsResult.IsAdministrator)
                {
                    // Si no tiene admin y canceló, cerrar aplicación
                    Application.Current.Shutdown();
                    return;
                }
            }

            // 🔥 VERIFICACIÓN FINAL: Solo cerrar si definitivamente falta ADMINISTRADOR
            if (!requirementsResult.IsAdministrator)
            {
                MessageBox.Show(
                    LocalizationManager.CurrentLanguage == "es"
                        ? "❌ ERROR CRÍTICO: Esta aplicación requiere permisos de administrador.\n\nPor favor, ejecuta el launcher como Administrador.\n\nEl launcher se cerrará ahora."
                        : "❌ CRITICAL ERROR: This application requires administrator permissions.\n\nPlease run the launcher as Administrator.\n\nThe launcher will now close.",
                    "Administrator Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Application.Current.Shutdown();
                return;
            }

            versionCheckTimer.Interval = TimeSpan.FromMinutes(5);
            versionCheckTimer.Tick += VersionCheckTimer_Tick;
            versionCheckTimer.Start();

            _ = CheckVersionAsync();

            DllEntries = new ObservableCollection<DllEntry>();
            DllListView.ItemsSource = DllEntries;

            LoadSettings();
            InitializeTimers();

            Loaded += (s, e) =>
            {
                UpdateUI();
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    StartParallaxAnimation();
                    _isLoadingSettings = false;
                    System.Diagnostics.Debug.WriteLine("[LOADING] Bandera _isLoadingSettings desactivada");

                    if (SettingsManager.Settings.AutoInject)
                    {
                        _autoInjectionCompleted = false;
                        _autoInjectTimer?.Start();

                        if (InjectionManager.IsGameRunning())
                        {
                            System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Juego detectado al iniciar");
                            Task.Run(async () =>
                            {
                                await Task.Delay(2000);
                                Dispatcher.Invoke(() => AutoInjectTimer_Tick(null, EventArgs.Empty));
                            });
                        }
                    }
                }), DispatcherPriority.Loaded);
            };
        }

        private void VersionCheckTimer_Tick(object? sender, EventArgs e)
        {
            _ = CheckVersionAsync();
        }

        private async Task CheckVersionAsync()
        {
            Dispatcher.Invoke(() =>
            {
                VersionStatusText.Text = "COMPROBANDO VERSIÓN...";
                VersionStatusText.Foreground = (Brush)Application.Current.Resources["LavenderBrush"];
            });

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string remoteVersion = await client.GetStringAsync("https://raw.githubusercontent.com/Tessio/Translations/refs/heads/master/version_l.txt");
                    remoteVersion = remoteVersion.Trim();

                    if (remoteVersion != currentLocalVersion)
                    {
                        _updateAvailable = true;

                        Dispatcher.Invoke(() =>
                        {
                            VersionStatusText.Text = $"NUEVA VERSIÓN DISPONIBLE: {currentLocalVersion} > {remoteVersion}";
                            VersionStatusText.Foreground = Brushes.Red;
                            UpdateButton.Visibility = Visibility.Visible;
                            UpdateButton.Content = "Actualizar Ahora";
                            UpdateButton.IsEnabled = true;
                            ChangelogButton.Visibility = Visibility.Collapsed;

                            LaunchButton.IsEnabled = false;
                            InjectButton.IsEnabled = false;
                            KillButton.IsEnabled = false;
                        });
                    }
                    else
                    {
                        _updateAvailable = false;

                        Dispatcher.Invoke(() =>
                        {
                            VersionStatusText.Text = $"ULTIMA VERSIÓN: {currentLocalVersion}";
                            VersionStatusText.Foreground = Brushes.LightGreen;
                            UpdateButton.Visibility = Visibility.Collapsed;
                            ChangelogButton.Visibility = Visibility.Visible;

                            if (_currentValidation == null || _currentValidation.CanInject)
                            {
                                InjectButton.IsEnabled = true;
                                KillButton.IsEnabled = true;
                                if (!InjectionManager.IsGameRunning())
                                {
                                    LaunchButton.IsEnabled = true;
                                }
                            }
                        });
                    }
                }
            }
            catch
            {
                Dispatcher.Invoke(() =>
                {
                    VersionStatusText.Text = "ERROR AL COMPROBAR VERSIÓN";
                    _updateAvailable = false;
                });
            }
        }

        private void LoadSettings()
        {
            var settings = SettingsManager.Settings;

            if (settings.GameType == GameType.Legacy)
                LegacyRadio.IsChecked = true;
            else
                EnhancedRadio.IsChecked = true;

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

            foreach (var dll in settings.DllEntries)
            {
                DllEntries.Add(dll);
            }

            AutoInjectCheckbox.IsChecked = settings.AutoInject;

            var langTag = settings.Language;
            foreach (ComboBoxItem item in LanguageSelector.Items)
            {
                if (item.Tag?.ToString() == langTag)
                {
                    LanguageSelector.SelectedItem = item;
                    break;
                }
            }

            _launchDelay = settings.LaunchDelay;
            if (LaunchDelaySlider != null)
                LaunchDelaySlider.Value = _launchDelay;
            if (LaunchDelayTextBox != null)
                LaunchDelayTextBox.Text = _launchDelay.ToString();

            bool gameRunning = InjectionManager.IsGameRunning();
            _gameWasRunning = gameRunning;
            _gameStartTime = gameRunning ? DateTime.Now : DateTime.MinValue;
            _autoInjectionCompleted = false;
        }

        private void InitializeTimers()
        {
            _gameCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _gameCheckTimer.Tick += (s, e) => UpdateGameStatus();
            _gameCheckTimer.Start();

            _autoInjectTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _autoInjectTimer.Tick += AutoInjectTimer_Tick;

            _validationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _validationTimer.Tick += (s, e) => ValidateGameConfiguration();
            _validationTimer.Start();

            if (SettingsManager.Settings.AutoInject)
            {
                _autoInjectTimer.Start();
                System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Timer iniciado");
            }

            ValidateGameConfiguration();
        }

        private void ValidateGameConfiguration()
        {
            try
            {
                _currentValidation = GameValidator.ValidateGameState();

                bool canAddDll = _currentValidation.CanInject && !_updateAvailable;

                if (AddDllButton != null)
                    AddDllButton.IsEnabled = canAddDll;

                if (_currentValidation.IsFSLInstalled && !_isLoadingSettings)
                {
                    int recommendedDelay = _currentValidation.RecommendedDelay;

                    if (_launchDelay < recommendedDelay)
                    {
                        System.Diagnostics.Debug.WriteLine($"[VALIDATOR] FSL detectado - Ajustando delay de {_launchDelay}s a {recommendedDelay}s");

                        _launchDelay = recommendedDelay;

                        if (LaunchDelaySlider != null)
                            LaunchDelaySlider.Value = recommendedDelay;

                        if (LaunchDelayTextBox != null)
                            LaunchDelayTextBox.Text = recommendedDelay.ToString();

                        SettingsManager.Settings.LaunchDelay = _launchDelay;
                        SettingsManager.SaveSettings();
                    }
                }

                UpdateValidationStatus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VALIDATOR] Error: {ex.Message}");
            }
        }

        private void UpdateValidationStatus()
        {
            if (_currentValidation == null || StatusText == null)
                return;

            if (_currentValidation.IsBattlEyeActive)
            {
                StatusText.Text = LocalizationManager.GetString("BattlEyeDetected");
                StatusText.Foreground = Brushes.Red;

                System.Diagnostics.Debug.WriteLine("[VALIDATOR] ⚠️ BattlEye activo - Inyección bloqueada");
            }
            else if (_currentValidation.IsFSLInstalled)
            {
                var lang = LocalizationManager.CurrentLanguage.ToLower();
                string fslMessage = lang == "es"
                    ? $"FSL detectado - Delay: {_launchDelay}s"
                    : $"FSL detected - Delay: {_launchDelay}s";

                if (!StatusText.Text.Contains("Inyectando") &&
                    !StatusText.Text.Contains("Injecting") &&
                    !StatusText.Text.Contains("Esperando") &&
                    !_updateAvailable)
                {
                    StatusText.Text = fslMessage;
                    StatusText.Foreground = Brushes.Orange;
                }

                System.Diagnostics.Debug.WriteLine($"[VALIDATOR] ℹ️ FSL detectado - Delay: {_launchDelay}s");
            }
        }

        private void UpdateGameStatus()
        {
            bool isRunning = InjectionManager.IsGameRunning();

            if (isRunning)
            {
                GameStatusText.Text = LocalizationManager.GetString("GameRunning");
                GameStatusText.Foreground = Brushes.LimeGreen;

                LaunchButton.IsEnabled = false;

                if (!_updateAvailable && (_currentValidation == null || _currentValidation.CanInject))
                {
                    InjectButton.IsEnabled = true;
                    KillButton.IsEnabled = true;
                }
                else
                {
                    InjectButton.IsEnabled = false;
                    KillButton.IsEnabled = true;
                }

                if (!_gameWasRunning)
                {
                    _autoInjectionCompleted = false;
                    _gameStartTime = DateTime.Now;
                    System.Diagnostics.Debug.WriteLine($"[AUTO-INJECT] Juego detectado - Delay: {_launchDelay}s");
                }
                _gameWasRunning = true;
            }
            else
            {
                GameStatusText.Text = LocalizationManager.GetString("GameNotRunning");
                GameStatusText.Foreground = Brushes.Red;

                InjectButton.IsEnabled = false;
                KillButton.IsEnabled = false;

                if (!_updateAvailable && (_currentValidation == null || _currentValidation.CanInject))
                {
                    LaunchButton.IsEnabled = true;
                }
                else
                {
                    LaunchButton.IsEnabled = false;
                }

                if (_gameWasRunning)
                {
                    _autoInjectionCompleted = false;
                    _gameWasRunning = false;
                    _gameStartTime = DateTime.MinValue;

                    foreach (var dll in DllEntries)
                    {
                        dll.Status = LocalizationManager.GetString("NotInjected");
                    }

                    if (StatusText != null && !_updateAvailable && (_currentValidation == null || !_currentValidation.IsBattlEyeActive))
                    {
                        var lang = LocalizationManager.CurrentLanguage.ToLower();
                        StatusText.Text = lang == "es" ? "Listo" : "Ready";
                        StatusText.Foreground = Brushes.White;
                    }

                    System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Juego cerrado");
                }
            }
        }

        private async void AutoInjectTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                bool gameRunning = InjectionManager.IsGameRunning();
                bool autoInjectEnabled = SettingsManager.Settings.AutoInject;

                System.Diagnostics.Debug.WriteLine($"[AUTO-INJECT] Tick - Enabled: {autoInjectEnabled}, Game: {gameRunning}, Done: {_autoInjectionCompleted}");

                if (!autoInjectEnabled || _autoInjectionCompleted)
                    return;

                if (!gameRunning)
                    return;

                var liveValidation = GameValidator.ValidateGameState();
                if (liveValidation.IsBattlEyeActive)
                {
                    System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] ⚠️ BattlEye activo - Auto-inyección cancelada");

                    StatusText.Text = LocalizationManager.GetString("BattlEyeDetected");
                    StatusText.Foreground = Brushes.Red;

                    _autoInjectionCompleted = true;

                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            LocalizationManager.GetString("BattlEyeWarning"),
                            LocalizationManager.GetString("InjectionBlocked"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });

                    return;
                }

                var enabledDlls = DllEntries.Where(d => d.Enabled).ToList();
                if (!enabledDlls.Any())
                {
                    _autoInjectionCompleted = true;
                    return;
                }

                if (_gameStartTime == DateTime.MinValue)
                {
                    _gameStartTime = DateTime.Now;
                }

                var elapsed = (DateTime.Now - _gameStartTime).TotalSeconds;
                if (elapsed < _launchDelay)
                {
                    var remaining = _launchDelay - (int)elapsed;
                    StatusText.Text = $"Esperando carga del juego... ({remaining}s)";
                    StatusText.Foreground = Brushes.Orange;
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] Iniciando inyección...");
                StatusText.Text = "Inyectando DLLs automáticamente...";
                StatusText.Foreground = Brushes.Yellow;

                await Task.Delay(1000);

                if (!InjectionManager.IsGameRunning()) return;

                await InjectDllsAsync();
                _autoInjectionCompleted = true;

                StatusText.Text = "✅ Auto-inyección completada";
                StatusText.Foreground = Brushes.LimeGreen;
                System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] ✅ Completado");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTO-INJECT] ❌ Error: {ex.Message}");
                StatusText.Text = $"Error auto-inject: {ex.Message}";
                StatusText.Foreground = Brushes.Red;
            }
        }

        private void UpdateUI()
        {
            try
            {
                if (DllListTitle != null) DllListTitle.Text = LocalizationManager.GetString("DllList");
                if (AddDllButton != null) AddDllButton.Content = LocalizationManager.GetString("AddDll");
                if (AutoInjectCheckbox != null) AutoInjectCheckbox.Content = LocalizationManager.GetString("AutoInject");
                if (LaunchButton != null) LaunchButton.Content = LocalizationManager.GetString("LaunchGame");
                if (InjectButton != null) InjectButton.Content = LocalizationManager.GetString("InjectDlls");
                if (KillButton != null) KillButton.Content = LocalizationManager.GetString("KillGame");
                if (GameTypeTitle != null) GameTypeTitle.Text = LocalizationManager.GetString("GameType");
                if (LauncherTitle != null) LauncherTitle.Text = LocalizationManager.GetString("Launcher");
                if (RequirementsTitle != null) RequirementsTitle.Text = LocalizationManager.GetString("Requirements");
                if (DevsTitle != null) DevsTitle.Text = LocalizationManager.GetString("Devs");
                if (VersionStatusTitle != null) VersionStatusTitle.Text = LocalizationManager.GetString("VersionStatus");
                if (UpdateButton != null) UpdateButton.Content = LocalizationManager.GetString("UpdateAvailable");
                if (ChangelogButton != null) ChangelogButton.Content = LocalizationManager.GetString("ViewChangelog");

                if (VcRequirementText != null) VcRequirementText.Text = LocalizationManager.GetString("VcRequirement");
                if (GtaRequirementText != null) GtaRequirementText.Text = LocalizationManager.GetString("GtaRequirement");
                if (AdminRequirementText != null) AdminRequirementText.Text = LocalizationManager.GetString("AdminRequirement");

                if (LanguageLabel != null) LanguageLabel.Text = LocalizationManager.GetString("Language");

                if (LaunchDelayTitle != null)
                    LaunchDelayTitle.Text = LocalizationManager.GetString("LaunchDelay");
                if (LaunchDelayDescription != null)
                    LaunchDelayDescription.Text = LocalizationManager.GetString("LaunchDelayDescription");
                if (LaunchDelayInfo != null)
                    LaunchDelayInfo.Text = LocalizationManager.GetString("LaunchDelayInfo");

                if (AppDataButton != null)
                    AppDataButton.Content = LocalizationManager.GetString("OpenAppData");

                UpdateRemoveButtonsText();

                var lang = LocalizationManager.CurrentLanguage.ToLower();
                if (StatusText != null && !_updateAvailable)
                    StatusText.Text = lang == "es" ? "Listo" : "Ready";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en UpdateUI: {ex.Message}");
            }
        }

        private void UpdateRemoveButtonsText()
        {
            try
            {
                string currentLang = "en";
                if (LanguageSelector?.SelectedItem is ComboBoxItem item)
                    currentLang = item.Tag?.ToString() ?? "en";

                string removeText = currentLang.ToLower() == "es" ? "Quitar" : "Remove";

                if (DllListView?.ItemsSource != null)
                {
                    var items = DllListView.ItemsSource;
                    DllListView.ItemsSource = null;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        DllListView.ItemsSource = items;
                        Dispatcher.BeginInvoke(new Action(() => UpdateVisualRemoveButtons(removeText)), DispatcherPriority.Loaded);
                    }), DispatcherPriority.Loaded);
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
                    var container = DllListView.ItemContainerGenerator.ContainerFromIndex(i) as ListViewItem;
                    if (container != null)
                    {
                        var tb = FindVisualChild<TextBlock>(container, "RemoveButtonText");
                        if (tb != null) tb.Text = text;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en UpdateVisualRemoveButtons: {ex.Message}");
            }
        }

        private T? FindVisualChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed && (child as FrameworkElement)?.Name == name)
                    return typed;
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void AddDll_Click(object sender, RoutedEventArgs e)
        {
            if (_currentValidation != null && _currentValidation.IsBattlEyeActive)
            {
                MessageBox.Show(
                    LocalizationManager.GetString("BattlEyeWarning"),
                    LocalizationManager.GetString("InjectionBlocked"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

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
                if (!_isLoadingSettings)
                    SettingsManager.SaveSettings();
            }
        }

        private void RemoveDll_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is DllEntry dll)
            {
                DllEntries.Remove(dll);
                SettingsManager.Settings.DllEntries = DllEntries.ToList();
                if (!_isLoadingSettings)
                    SettingsManager.SaveSettings();
            }
        }

        private async void LaunchGame_Click(object sender, RoutedEventArgs e)
        {
            if (_updateAvailable)
            {
                MessageBox.Show(
                    LocalizationManager.CurrentLanguage == "es"
                        ? "Por favor actualiza el launcher antes de continuar."
                        : "Please update the launcher before continuing.",
                    "Update Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

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
            if (_updateAvailable)
            {
                MessageBox.Show(
                    LocalizationManager.CurrentLanguage == "es"
                        ? "Por favor actualiza el launcher antes de continuar."
                        : "Please update the launcher before continuing.",
                    "Update Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var liveValidation = GameValidator.ValidateGameState();
            if (liveValidation.IsBattlEyeActive)
            {
                MessageBox.Show(
                    LocalizationManager.GetString("BattlEyeWarning"),
                    LocalizationManager.GetString("InjectionBlocked"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                System.Diagnostics.Debug.WriteLine("[INJECT] ❌ Inyección bloqueada - BattlEye detectado");
                return;
            }

            await InjectDllsAsync();
        }

        private async Task InjectDllsAsync()
        {
            try
            {
                var enabledDlls = DllEntries.Where(d => d.Enabled).ToList();
                if (!enabledDlls.Any())
                {
                    MessageBox.Show(LocalizationManager.GetString("NoDllsEnabled"), "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                            dll.Status = "Inyectado";
                            injected++;
                            break;
                        case InjectionResult.ERROR_OPEN_PROCESS:
                            dll.Status = "Error: No se pudo abrir GTA5";
                            break;
                        case InjectionResult.ERROR_DLL_NOTFOUND:
                            dll.Status = "Error: DLL no encontrada";
                            break;
                        case InjectionResult.ERROR_ALLOC:
                            dll.Status = "Error: Memoria no asignada";
                            break;
                        case InjectionResult.ERROR_WRITE:
                            dll.Status = "Error: Escritura fallida";
                            break;
                        case InjectionResult.ERROR_CREATE_THREAD:
                            dll.Status = "Error: Hilo remoto fallido";
                            break;
                        default:
                            dll.Status = "Error: Fallo desconocido";
                            break;
                    }
                }

                StatusText.Text = $"Inyección completada: ({injected}/{enabledDlls.Count})";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Inyección falló";
            }
        }

        private void KillGame_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InjectionManager.KillGame();
                StatusText.Text = LocalizationManager.GetString("GameKilled");
                Task.Delay(2000).ContinueWith(_ =>
                    Dispatcher.Invoke(() =>
                    {
                        if (!_updateAvailable && (_currentValidation == null || !_currentValidation.IsBattlEyeActive))
                        {
                            var lang = LocalizationManager.CurrentLanguage.ToLower();
                            StatusText.Text = lang == "es" ? "Listo" : "Ready";
                            StatusText.Foreground = Brushes.White;
                        }
                    }));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GameType_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            SettingsManager.Settings.GameType = LegacyRadio.IsChecked == true ? GameType.Legacy : GameType.Enhanced;
            SettingsManager.SaveSettings();
        }

        private void Launcher_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;
            SettingsManager.Settings.LauncherType = RockstarRadio.IsChecked == true ? LauncherType.Rockstar :
                                               EpicRadio.IsChecked == true ? LauncherType.EpicGames :
                                               LauncherType.Steam;
            SettingsManager.SaveSettings();
        }

        private void AutoInject_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingSettings) return;

            bool isEnabled = AutoInjectCheckbox.IsChecked == true;
            SettingsManager.Settings.AutoInject = isEnabled;
            SettingsManager.SaveSettings();

            if (isEnabled)
            {
                _autoInjectionCompleted = false;
                _gameWasRunning = false;
                _gameStartTime = DateTime.MinValue;
                _autoInjectTimer?.Start();
                System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] ✅ Activado");

                if (InjectionManager.IsGameRunning())
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        Dispatcher.Invoke(() => AutoInjectTimer_Tick(null, EventArgs.Empty));
                    });
                }
            }
            else
            {
                _autoInjectTimer?.Stop();
                _autoInjectionCompleted = false;
                System.Diagnostics.Debug.WriteLine("[AUTO-INJECT] ❌ Desactivado");
            }
        }

        private void LaunchDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (LaunchDelayTextBox == null || _isUpdatingDelayControls || _isLoadingSettings)
                return;

            _isUpdatingDelayControls = true;

            int newValue = (int)Math.Round(e.NewValue);
            _launchDelay = newValue;
            LaunchDelayTextBox.Text = newValue.ToString();

            if (SettingsManager.Settings != null)
            {
                SettingsManager.Settings.LaunchDelay = _launchDelay;
                SettingsManager.SaveSettings();
            }

            _isUpdatingDelayControls = false;

            System.Diagnostics.Debug.WriteLine($"[LAUNCH DELAY] Valor: {_launchDelay}s");
        }

        private void LaunchDelayTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (LaunchDelaySlider == null || _isUpdatingDelayControls || _isLoadingSettings)
                return;

            if (int.TryParse(LaunchDelayTextBox.Text, out int value))
            {
                if (value >= 0 && value <= 60)
                {
                    _isUpdatingDelayControls = true;

                    _launchDelay = value;
                    LaunchDelaySlider.Value = value;

                    if (SettingsManager.Settings != null)
                    {
                        SettingsManager.Settings.LaunchDelay = _launchDelay;
                        SettingsManager.SaveSettings();
                    }

                    _isUpdatingDelayControls = false;
                }
            }
        }

        private void LaunchDelayTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsTextNumeric(e.Text);
        }

        private static bool IsTextNumeric(string text)
        {
            return int.TryParse(text, out _);
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingSettings) return;
            if (LanguageSelector.SelectedItem is ComboBoxItem item)
            {
                var lang = item.Tag?.ToString() ?? "en";
                LocalizationManager.SetLanguage(lang);
                SettingsManager.Settings.Language = lang;
                SettingsManager.SaveSettings();
                UpdateUI();
                Dispatcher.BeginInvoke(new Action(() => UpdateRemoveButtonsText()), DispatcherPriority.Loaded);
                if (StatusText != null && !_updateAvailable)
                    StatusText.Text = lang.ToLower() == "es" ? "Listo" : "Ready";
            }
        }

        private void ComboBox_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (LanguageSelector != null && e.LeftButton == MouseButtonState.Pressed)
            {
                LanguageSelector.IsDropDownOpen = !LanguageSelector.IsDropDownOpen;
                e.Handled = true;
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
                {
                    FileName = TESSIO_DISCORD_URL,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir Discord: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Changelog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo
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

        private void OpenAppData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                Process.Start(new ProcessStartInfo
                {
                    FileName = appDataPath,
                    UseShellExecute = true
                });

                System.Diagnostics.Debug.WriteLine($"[APPDATA] Carpeta abierta: {appDataPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al abrir AppData: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();
        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void Discord_Click(object sender, RoutedEventArgs e) => OpenUrl(TESSIO_DISCORD_URL);
        private void TikTok_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.tiktok.com/@tessiogg");
        private void Twitch_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.twitch.tv/tessiogg");
        private void YouTube_Click(object sender, RoutedEventArgs e) => OpenUrl("https://www.youtube.com/@TessioScript");

        private void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al abrir: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StartParallaxAnimation()
        {
            try
            {
                var storyboard = (Storyboard)FindResource("BackgroundAnimation");
                storyboard?.Begin(this, true);
                if (BackgroundImage != null) BackgroundImage.Visibility = Visibility.Visible;
                if (ParallaxLayer1 != null) ParallaxLayer1.Visibility = Visibility.Visible;
                if (ParallaxLayer2 != null) ParallaxLayer2.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en animación: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _gameCheckTimer?.Stop();
            _autoInjectTimer?.Stop();
            _validationTimer?.Stop();
            base.OnClosed(e);
        }
    }
}