using ark_app1.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT;

namespace ark_app1;

public sealed partial class MainPage : Window
{
    public new AppWindow AppWindow { get; private set; }
    MicaController? micaController;
    SystemBackdropConfiguration? configurationSource;

    public MainPage(string userFullName)
    {
        this.InitializeComponent();
        WindowHelper.SetDefaultIcon(this);
        var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
        AppWindow = AppWindow.GetFromWindowId(windowId);

        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
        AppWindow.Resize(new SizeInt32(1800, 1000));
        CenterWindow();
        TrySetMicaBackdrop();

        UserPicture.DisplayName = userFullName;
        AppTitleBar.Subtitle = $"Bienvenido, {userFullName}";
        ContentFrame.Navigate(typeof(HomePage));

        // this.Closed += MainPage_Closed; // Handled by Window_Closed in Mica setup or explicitly below
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

        // Ensure the entire application closes when the main dashboard is closed.
        // This handles cases where MainWindow is hidden but still running.
        Application.Current.Exit();
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

    private void MainPage_Closed_Legacy(object sender, WindowEventArgs args)
    {
       // Handled in Window_Closed
    }
     private void CenterWindow()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
        if (area == null) return;
        AppWindow.Move(new PointInt32(
            (area.Value.Width - AppWindow.Size.Width) / 2,
            (area.Value.Height - AppWindow.Size.Height) / 2));
    }
    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var selectedItem = (NavigationViewItem)args.SelectedItem;
        if (selectedItem != null && selectedItem.Tag != null)
        {
            string selectedItemTag = selectedItem.Tag.ToString()!;

            // Navigate only if a valid page exists
            if (selectedItemTag == "Home" || selectedItemTag == "TermsAndConditions" || selectedItemTag == "AboutArkDev" || selectedItemTag == "Inventory" || selectedItemTag == "Sales" || selectedItemTag == "Providers" || selectedItemTag == "Clients")
            {
                NavView.Header = selectedItem.Content?.ToString() ?? string.Empty;
                string pageName = "ark_app1." + selectedItemTag + "Page";
                Type pageType = Type.GetType(pageName);
                ContentFrame.Navigate(pageType);
            }
        }
    }

    private void ProfileFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        NavView.Header = "Mi Perfil";
        ContentFrame.Navigate(typeof(ProfilePage));
    }

    private void LogoutFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        // You would typically navigate back to the login window and close this one.
        var mainWindow = new MainWindow();
        mainWindow.Activate();
        this.Close();
    }

    // --- Global Search Logic ---
    private async void AppSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            if (string.IsNullOrWhiteSpace(sender.Text) || sender.Text.Length < 2)
            {
                sender.ItemsSource = null;
                return;
            }

            var results = new ObservableCollection<GlobalSearchResult>();
            string query = sender.Text;

            await Task.Run(async () =>
            {
                try
                {
                    using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                    await conn.OpenAsync();

                    // Search Products
                    var cmdProd = new SqlCommand("SELECT TOP 5 Id, Nombre, Codigo FROM Productos WHERE (Nombre LIKE @q OR Codigo LIKE @q) AND Activo = 1", conn);
                    cmdProd.Parameters.AddWithValue("@q", $"%{query}%");
                    using (var r = await cmdProd.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            string name = r.GetString(1);
                            string code = r.GetString(2);
                            results.Add(new GlobalSearchResult { Type = "Producto", Title = name, Subtitle = $"Cód: {code}", Id = r.GetInt32(0) });
                        }
                    }

                    // Search Clients
                    var cmdCli = new SqlCommand("SELECT TOP 5 Id, Nombre, CI FROM Clientes WHERE Nombre LIKE @q OR CI LIKE @q", conn);
                    cmdCli.Parameters.AddWithValue("@q", $"%{query}%");
                    using (var r = await cmdCli.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            string name = r.GetString(1);
                            string ci = r.IsDBNull(2) ? "S/N" : r.GetString(2);
                            results.Add(new GlobalSearchResult { Type = "Cliente", Title = name, Subtitle = $"CI: {ci}", Id = r.GetInt32(0) });
                        }
                    }
                }
                catch { /* Ignore errors during search */ }
            });

            // Dispatch to UI thread
            this.DispatcherQueue.TryEnqueue(() =>
            {
                sender.ItemsSource = results;
            });
        }
    }

    private void AppSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is GlobalSearchResult result)
        {
            if (result.Type == "Producto")
            {
                // Navigate to InventoryPage
                NavView.Header = "Inventario";
                ContentFrame.Navigate(typeof(InventoryPage));
                // Note: To filter automatically in InventoryPage, we would need to pass parameters or use a mediator.
                // For now, navigating to the relevant section is the most robust action without major refactoring.
            }
            else if (result.Type == "Cliente")
            {
                // Navigate to ClientsPage
                NavView.Header = "Clientes";
                ContentFrame.Navigate(typeof(ClientsPage));
            }
        }
    }

    private void AppSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion != null)
        {
            AppSearchBox_SuggestionChosen(sender, new AutoSuggestBoxSuggestionChosenEventArgs());
        }
    }
}

public class GlobalSearchResult
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public int Id { get; set; }

    public override string ToString() => $"{Type}: {Title}";
}
