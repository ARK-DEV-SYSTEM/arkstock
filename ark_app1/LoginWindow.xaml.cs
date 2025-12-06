using BCrypt.Net;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT;
using ark_app1.Helpers;

namespace ark_app1;

public sealed partial class LoginWindow : Window
{
    private readonly Regex _emailRegex = new(@"^[\w-\.]+@([\w-]+\.)+[\w-]{2,4}$", RegexOptions.Compiled);
    private readonly Regex _numericRegex = new(@"^\d+$", RegexOptions.Compiled);
    MicaController? micaController;
    SystemBackdropConfiguration? configurationSource;

    public LoginWindow()
    {
        this.InitializeComponent();
        WindowHelper.SetDefaultIcon(this);
        // Relying on Package.appxmanifest for application icon.
        var presenter = AppWindow.Presenter as OverlappedPresenter;
        if (presenter != null)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
        }
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
        AppWindow.Resize(new SizeInt32(750, 1004));
        CenterWindow();
        EnsureCorrectDatabase();
        TrySetMicaBackdrop();
    }

    private void EnsureCorrectDatabase()
    {
        if (DatabaseManager.ConnectionString != null && DatabaseManager.ConnectionString.Contains("Database=master"))
            DatabaseManager.ConnectionString = DatabaseManager.ConnectionString.Replace("Database=master", "Database=arkdbsisventas");
    }

    private void CenterWindow()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
        if (area == null) return;
        AppWindow.Move(new PointInt32(
            (area.Value.Width - AppWindow.Size.Width) / 2,
            (area.Value.Height - AppWindow.Size.Height) / 2));
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        string cred = LoginCredentialTextBox.Text.Trim();
        string pass = LoginPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(cred) || string.IsNullOrWhiteSpace(pass))
        {
            ShowInfoBar("Error", "Completa ambos campos para iniciar sesión.", InfoBarSeverity.Error);
            return;
        }

        await Task.Run(async () =>
        {
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, PasswordHash, NombreCompleto, CI, Email, Telefono FROM Usuarios WHERE Email = @cred OR CI = @cred";
                cmd.Parameters.AddWithValue("@cred", cred);

                using var reader = await cmd.ExecuteReaderAsync();
                if (reader.Read())
                {
                    string hash = reader.GetString(1);

                    if (BCrypt.Net.BCrypt.Verify(pass, hash))
                    {
                         var user = new User
                        {
                            Id = reader.GetInt32(0),
                            NombreCompleto = reader.GetString(2),
                            CI = reader.GetString(3),
                            Email = reader.GetString(4),
                            Telefono = reader.IsDBNull(5) ? string.Empty : reader.GetString(5)
                        };

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            var app = Application.Current as App;
                            if (app != null) app.CurrentUser = user;
                            var mainPage = new MainPage(user.NombreCompleto);
                            mainPage.Activate();
                            this.Close();
                        });
                    }
                    else
                    {
                        DispatcherQueue.TryEnqueue(() => ShowInfoBar("Error", "Contraseña incorrecta.", InfoBarSeverity.Error));
                    }
                }
                else
                {
                    DispatcherQueue.TryEnqueue(() => ShowInfoBar("Error", "Usuario no encontrado.", InfoBarSeverity.Error));
                }
            }
            catch
            {
                DispatcherQueue.TryEnqueue(() => ShowInfoBar("Error", "No se pudo conectar a la base de datos.", InfoBarSeverity.Error));
            }
        });
    }
    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        string nombre = FullNameTextBox.Text.Trim();
        string telefono = PhoneTextBox.Text.Trim();
        string ci = CiTextBox.Text.Trim();
        string email = EmailTextBox.Text.Trim();
        string password = RegisterPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(nombre) ||
            string.IsNullOrWhiteSpace(ci) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password))
        {
            ShowInfoBar("Error", "Todos los campos son obligatorios.", InfoBarSeverity.Error);
            return;
        }

        if (!_emailRegex.IsMatch(email))
        {
            ShowInfoBar("Error", "Ingresa un correo electrónico válido.", InfoBarSeverity.Warning);
            return;
        }

        if (!_numericRegex.IsMatch(ci) || ci.Length < 7 || ci.Length > 8)
        {
            ShowInfoBar("Error", "La CI debe contener solo números (7 u 8 dígitos).", InfoBarSeverity.Warning);
            return;
        }

        if (!string.IsNullOrEmpty(telefono) && !_numericRegex.IsMatch(telefono))
        {
            ShowInfoBar("Error", "El teléfono solo debe contener números.", InfoBarSeverity.Warning);
            return;
        }

        if (password.Length < 6)
        {
            ShowInfoBar("Error", "La contraseña debe tener al menos 6 caracteres.", InfoBarSeverity.Warning);
            return;
        }

        string hash = BCrypt.Net.BCrypt.HashPassword(password);

        await Task.Run(async () =>
        {
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();

                var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Usuarios (NombreCompleto, CI, Email, Telefono, PasswordHash)
                    VALUES (@nombre, @ci, @email, @tel, @hash)";

                cmd.Parameters.AddWithValue("@nombre", nombre);
                cmd.Parameters.AddWithValue("@ci", ci);
                cmd.Parameters.AddWithValue("@email", email);
                cmd.Parameters.AddWithValue("@tel", telefono ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@hash", hash);

                await cmd.ExecuteNonQueryAsync();

                DispatcherQueue.TryEnqueue(() =>
                {
                    ShowInfoBar("¡Perfecto!", "Cuenta creada correctamente. Ya puedes iniciar sesión.", InfoBarSeverity.Success);
                    ShowLogin();
                });
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                DispatcherQueue.TryEnqueue(() => ShowInfoBar("Error", "El CI o Email ya está registrado.", InfoBarSeverity.Error));
            }
            catch (Exception)
            {
                DispatcherQueue.TryEnqueue(() => ShowInfoBar("Error", "Error al crear la cuenta.", InfoBarSeverity.Error));
            }
        });
    }
    private void ShowRegisterButton_Click(object sender, RoutedEventArgs e) => ShowRegister();
    private void ShowLoginButton_Click(object sender, RoutedEventArgs e) => ShowLogin();

    private void ShowInfoBar(string title, string message, InfoBarSeverity severity)
    {
        LoginInfoBar.Title = title;
        LoginInfoBar.Message = message;
        LoginInfoBar.Severity = severity;
        LoginInfoBar.IsOpen = true;
    }

    private void ShowRegister()
    {
        LoginStackPanel.Visibility = Visibility.Collapsed;
        RegisterStackPanel.Visibility = Visibility.Visible;
        TitleTextBlock.Text = "Crear Cuenta";
        LoginInfoBar.IsOpen = false;
    }

    private void ShowLogin()
    {
        RegisterStackPanel.Visibility = Visibility.Collapsed;
        LoginStackPanel.Visibility = Visibility.Visible;
        TitleTextBlock.Text = "Iniciar Sesión";
        LoginInfoBar.IsOpen = false;
    }

    bool TrySetMicaBackdrop()
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
            micaController.Kind = MicaKind.BaseAlt;
            micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
            micaController.SetSystemBackdropConfiguration(configurationSource);

            return true;
        }
        return false;
    }
    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (configurationSource != null)
            configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        micaController?.Dispose();
        micaController = null;
        configurationSource = null;
        this.Activated -= Window_Activated;
    }

    private void Window_ThemeChanged(FrameworkElement sender, object args)
    {
        if (configurationSource != null)
            SetConfigurationSourceTheme();
    }

    private void SetConfigurationSourceTheme()
    {
        if (configurationSource == null) return;

        switch (((FrameworkElement)this.Content).ActualTheme)
        {
            case ElementTheme.Dark:
                configurationSource.Theme = SystemBackdropTheme.Dark;
                break;
            case ElementTheme.Light:
                configurationSource.Theme = SystemBackdropTheme.Light;
                break;
            case ElementTheme.Default:
                configurationSource.Theme = SystemBackdropTheme.Default;
                break;
        }
    }
}
