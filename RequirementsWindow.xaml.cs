using GTAVInjector.Core;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace GTAVInjector
{
    public partial class RequirementsWindow : Window
    {
        private readonly SystemRequirementsResult _result;
        private string _language;

        public RequirementsWindow(SystemRequirementsResult result, string language = "es")
        {
            InitializeComponent();
            _result = result;
            _language = language.ToLower();

            // Configurar idioma inicial
            SetLanguageInComboBox(_language);

            LoadRequirements();
            UpdateUI();
        }

        private void SetLanguageInComboBox(string lang)
        {
            foreach (ComboBoxItem item in LanguageSelector.Items)
            {
                if (item.Tag?.ToString() == lang)
                {
                    LanguageSelector.SelectedItem = item;
                    break;
                }
            }
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageSelector.SelectedItem is ComboBoxItem item)
            {
                _language = item.Tag?.ToString() ?? "es";
                UpdateUI();
                LoadRequirements(); // Recargar con nuevo idioma
            }
        }

        private void LoadRequirements()
        {
            RequirementsPanel.Children.Clear();

            // 🔥 SECCIÓN: REQUISITOS CRÍTICOS
            AddSectionHeader(
                _language == "es" ? "🔒 REQUISITOS CRÍTICOS (Obligatorios)" : "🔒 CRITICAL REQUIREMENTS (Mandatory)",
                Brushes.Red);
            AddRequirement("Administrator Rights", _result.IsAdministrator, true);

            // Separador
            AddSeparator();

            // 🔥 SECCIÓN: REQUISITOS RECOMENDADOS
            AddSectionHeader(
                _language == "es" ? "⚠️ REQUISITOS RECOMENDADOS (Opcionales)" : "⚠️ RECOMMENDED REQUIREMENTS (Optional)",
                Brushes.Orange);
            AddRequirement("VC++ Redistributable x86", _result.HasVCRedist2015_2022_x86, false);
            AddRequirement("VC++ Redistributable x64", _result.HasVCRedist2015_2022_x64, false);
            AddRequirement("GTA V Installed", _result.HasGTAVInstalled, false);
            AddRequirement("Windows 10 or Newer", _result.IsWindows10OrNewer, false);

            // Separador
            AddSeparator();

            // 🔥 SECCIÓN: INFORMACIÓN
            AddSectionHeader(
                _language == "es" ? "ℹ️ INFORMACIÓN DEL SISTEMA" : "ℹ️ SYSTEM INFORMATION",
                new SolidColorBrush(Color.FromRgb(169, 70, 255)));
            AddRequirement(".NET 8 Runtime", _result.HasDotNet8Runtime, false);
        }

        private void AddSectionHeader(string text, Brush color)
        {
            var headerBlock = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = color,
                Margin = new Thickness(0, 10, 0, 10)
            };
            RequirementsPanel.Children.Add(headerBlock);
        }

        private void AddSeparator()
        {
            var separator = new Border
            {
                Height = 2,
                Background = new SolidColorBrush(Color.FromRgb(169, 70, 255)),
                Opacity = 0.3,
                Margin = new Thickness(0, 15, 0, 15)
            };
            RequirementsPanel.Children.Add(separator);
        }

        private void AddRequirement(string name, bool met, bool isCritical)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 31, 1, 47)),
                BorderBrush = met ? Brushes.LimeGreen : (isCritical ? Brushes.Red : Brushes.Orange),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 5, 0, 5),
                Padding = new Thickness(15, 12, 15, 12)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

            // Icono
            var icon = new TextBlock
            {
                Text = met ? "✅" : (isCritical ? "❌" : "⚠️"),
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(icon, 0);

            // Texto
            var textBlock = new TextBlock
            {
                Text = GetLocalizedRequirementName(name),
                FontSize = 14,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(textBlock, 1);

            // Etiqueta de estado
            var statusBorder = new Border
            {
                Background = isCritical ? new SolidColorBrush(Color.FromArgb(100, 255, 0, 0)) :
                                         new SolidColorBrush(Color.FromArgb(100, 255, 140, 0)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var statusLabel = new TextBlock
            {
                Text = isCritical ? (_language == "es" ? "CRÍTICO" : "CRITICAL") :
                                   (_language == "es" ? "Opcional" : "Optional"),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };

            statusBorder.Child = statusLabel;
            Grid.SetColumn(statusBorder, 2);

            grid.Children.Add(icon);
            grid.Children.Add(textBlock);
            grid.Children.Add(statusBorder);
            border.Child = grid;

            RequirementsPanel.Children.Add(border);
        }

        private string GetLocalizedRequirementName(string name)
        {
            if (_language == "es")
            {
                return name switch
                {
                    "Administrator Rights" => "Permisos de Administrador",
                    "VC++ Redistributable x86" => "Visual C++ Redistributable x86",
                    "VC++ Redistributable x64" => "Visual C++ Redistributable x64",
                    "GTA V Installed" => "GTA V Instalado",
                    "Windows 10 or Newer" => "Windows 10 o Superior",
                    ".NET 8 Runtime" => "Runtime .NET 8",
                    _ => name
                };
            }
            return name;
        }

        private void UpdateUI()
        {
            // Actualizar textos principales
            if (_language == "es")
            {
                Title = "Verificación de Requisitos del Sistema";
                if (TitleText != null) TitleText.Text = "Requisitos del Sistema";
                if (SubtitleText != null) SubtitleText.Text = "Verificando configuración...";
                if (StatusSummaryText != null) StatusSummaryText.Text = _result.GetDetailedSummary(_language);
                if (StatusCountText != null) StatusCountText.Text = _result.GetSummary();
                if (DownloadButton != null) DownloadButton.Content = "📥 Descargar VC++ Redist";
                if (ContinueButton != null) ContinueButton.Content = "✔️ Continuar";
            }
            else
            {
                Title = "System Requirements Check";
                if (TitleText != null) TitleText.Text = "System Requirements";
                if (SubtitleText != null) SubtitleText.Text = "Checking configuration...";
                if (StatusSummaryText != null) StatusSummaryText.Text = _result.GetDetailedSummary(_language);
                if (StatusCountText != null) StatusCountText.Text = _result.GetSummary();
                if (DownloadButton != null) DownloadButton.Content = "📥 Download VC++ Redist";
                if (ContinueButton != null) ContinueButton.Content = "✔️ Continue";
            }

            // Actualizar color del resumen
            if (StatusSummaryText != null)
            {
                if (_result.AllRequirementsMet)
                    StatusSummaryText.Foreground = Brushes.LimeGreen;
                else if (_result.HasCriticalRequirements)
                    StatusSummaryText.Foreground = Brushes.Orange;
                else
                    StatusSummaryText.Foreground = Brushes.Red;
            }

            // 🔥 Solo ocultar botón de continuar si falta ADMINISTRADOR
            if (!_result.IsAdministrator)
            {
                if (ContinueButton != null) ContinueButton.Visibility = Visibility.Collapsed;

                if (DownloadButton != null)
                {
                    if (_language == "es")
                    {
                        DownloadButton.Content = "❌ Cerrar Launcher";
                    }
                    else
                    {
                        DownloadButton.Content = "❌ Close Launcher";
                    }
                }
            }
            else
            {
                if (ContinueButton != null) ContinueButton.Visibility = Visibility.Visible;

                // Ocultar botón de descarga si VC++ ya está instalado
                if (_result.HasVCRedist2015_2022_x86 && _result.HasVCRedist2015_2022_x64)
                {
                    if (DownloadButton != null) DownloadButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    if (DownloadButton != null) DownloadButton.Visibility = Visibility.Visible;
                }
            }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            // Si no tiene admin, cerrar el launcher
            if (!_result.IsAdministrator)
            {
                Application.Current.Shutdown();
                return;
            }

            // Si tiene admin, abrir descarga de VC++
            SystemRequirementsValidator.OpenVCRedistDownload();
            MessageBox.Show(
                _language == "es"
                    ? "Se abrirá la página de descarga de Visual C++ Redistributable.\n\nInstala ambas versiones (x86 y x64) y reinicia el launcher."
                    : "The Visual C++ Redistributable download page will open.\n\nInstall both versions (x86 and x64) and restart the launcher.",
                _language == "es" ? "Descarga Iniciada" : "Download Started",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_result.IsAdministrator)
            {
                MessageBox.Show(
                    _language == "es"
                        ? "❌ ERROR CRÍTICO: Se requieren permisos de Administrador.\n\nPor favor, ejecuta el launcher como Administrador."
                        : "❌ CRITICAL ERROR: Administrator permissions required.\n\nPlease run the launcher as Administrator.",
                    _language == "es" ? "Error Crítico" : "Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            if (!_result.AllRequirementsMet)
            {
                string warningMessage = _language == "es"
                    ? "⚠️ ADVERTENCIA: Algunos requisitos recomendados no se cumplen:\n\n"
                    : "⚠️ WARNING: Some recommended requirements are not met:\n\n";

                var warnings = new System.Collections.Generic.List<string>();

                if (!_result.HasVCRedist2015_2022_x86)
                    warnings.Add(_language == "es" ? "• VC++ Redistributable x86 no instalado" : "• VC++ Redistributable x86 not installed");
                if (!_result.HasVCRedist2015_2022_x64)
                    warnings.Add(_language == "es" ? "• VC++ Redistributable x64 no instalado" : "• VC++ Redistributable x64 not installed");
                if (!_result.HasGTAVInstalled)
                    warnings.Add(_language == "es" ? "• GTA V no detectado en el sistema" : "• GTA V not detected on the system");
                if (!_result.IsWindows10OrNewer)
                    warnings.Add(_language == "es" ? "• Windows 10 o superior recomendado" : "• Windows 10 or newer recommended");

                warningMessage += string.Join("\n", warnings);
                warningMessage += _language == "es"
                    ? "\n\n⚠️ El launcher puede no funcionar correctamente.\n\n¿Deseas continuar de todos modos?"
                    : "\n\n⚠️ The launcher may not work correctly.\n\nDo you want to continue anyway?";

                var result = MessageBox.Show(
                    warningMessage,
                    _language == "es" ? "Advertencia" : "Warning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                    return;
            }

            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Si no tiene admin, cerrar aplicación completa
            if (!_result.IsAdministrator)
            {
                Application.Current.Shutdown();
                return;
            }

            // Si tiene admin, solo cerrar esta ventana con resultado negativo
            DialogResult = false;
            Close();
        }
    }
}