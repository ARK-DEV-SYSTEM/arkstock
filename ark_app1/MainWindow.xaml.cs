using ark_app1.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT;

namespace ark_app1
{
    public sealed partial class MainWindow : Window, INotifyPropertyChanged
    {
        private bool _isInfoBarOpen;
        private string _infoBarTitle = string.Empty;
        private string _infoBarMessage = string.Empty;
        private InfoBarSeverity _infoBarSeverity = InfoBarSeverity.Informational;
        MicaController? micaController;
        SystemBackdropConfiguration? configurationSource;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsInfoBarOpen
        {
            get => _isInfoBarOpen;
            set => SetProperty(ref _isInfoBarOpen, value);
        }

        public string InfoBarTitle
        {
            get => _infoBarTitle;
            set => SetProperty(ref _infoBarTitle, value);
        }

        public string InfoBarMessage
        {
            get => _infoBarMessage;
            set => SetProperty(ref _infoBarMessage, value);
        }

        public InfoBarSeverity InfoBarSeverity
        {
            get => _infoBarSeverity;
            set => SetProperty(ref _infoBarSeverity, value);
        }

        public MainWindow()
        {
            this.InitializeComponent();
            WindowHelper.SetDefaultIcon(this);

            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            AppWindow.Resize(new SizeInt32(900, 900));
            CenterWindow();
            TrySetMicaBackdrop();
        }

        private void CenterWindow()
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
            if (area == null) return;
            AppWindow.Move(new PointInt32((area.Value.Width - AppWindow.Size.Width) / 2, (area.Value.Height - AppWindow.Size.Height) / 2));
        }

        private void AuthenticationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SqlAuthenticationStackPanel != null)
            {
                var selectedItem = (ComboBoxItem)AuthenticationComboBox.SelectedItem;
                SqlAuthenticationStackPanel.Visibility = (selectedItem.Content.ToString() == "SQL Server Authentication")
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            IsInfoBarOpen = false;
            string serverName = ServerNameTextBox.Text;
            string connectionString;

            var selectedItem = (ComboBoxItem)AuthenticationComboBox.SelectedItem;
            if (selectedItem.Content.ToString() == "Windows Authentication")
            {
                connectionString = $"Server={serverName};Database=master;Integrated Security=True;TrustServerCertificate=True;";
            }
            else
            {
                string username = UsernameTextBox.Text;
                string password = PasswordBox.Password;
                connectionString = $"Server={serverName};Database=master;User ID={username};Password={password};TrustServerCertificate=True;";
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    await connection.OpenAsync();
                    DatabaseManager.ConnectionString = connectionString;
                    this.AppWindow.Hide();
                    var loginWindow = new LoginWindow();
                    loginWindow.Activate();
                }
                catch (Exception ex)
                {
                    InfoBarTitle = "Error de conexión";
                    InfoBarMessage = $"No se pudo establecer la conexión con el servidor SQL. Error: {ex.Message}";
                    InfoBarSeverity = InfoBarSeverity.Error;
                    IsInfoBarOpen = true;
                    if (!this.AppWindow.IsVisible)
                    {
                        this.AppWindow.Show();
                    }
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        bool TrySetMicaBackdrop(bool useBaseAlt = true)
        {
            if (MicaController.IsSupported())
            {
                configurationSource = new SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                micaController = new MicaController();
                micaController.Kind = useBaseAlt ? MicaKind.BaseAlt : MicaKind.Base;
                micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
                micaController.SetSystemBackdropConfiguration(configurationSource);

                return true;
            }
            return false;
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (configurationSource is not null)
            {
                configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            micaController?.Dispose();
            micaController = null;
            this.Activated -= Window_Activated;
            configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            if (configurationSource is not null)
            {
                switch (((FrameworkElement)this.Content).ActualTheme)
                {
                    case ElementTheme.Dark: configurationSource.Theme = SystemBackdropTheme.Dark; break;
                    case ElementTheme.Light: configurationSource.Theme = SystemBackdropTheme.Light; break;
                    case ElementTheme.Default: configurationSource.Theme = SystemBackdropTheme.Default; break;
                }
            }
        }
    }
}
