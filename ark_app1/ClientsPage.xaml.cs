using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Data;

namespace ark_app1
{
    public sealed partial class ClientsPage : Page
    {
        private ObservableCollection<ClienteEntity> _clients = new();

        public ClientsPage()
        {
            this.InitializeComponent();
            ClientsGrid.ItemsSource = _clients;
            Loaded += async (s, e) => await LoadClients();
        }

        private async Task LoadClients(string filter = "")
        {
            _clients.Clear();
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, Nombre, Telefono, CI, Direccion, Notas FROM Clientes";

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    cmd.CommandText += " WHERE Nombre LIKE @f OR CI LIKE @f";
                    cmd.Parameters.AddWithValue("@f", $"%{filter}%");
                }

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    _clients.Add(new ClienteEntity
                    {
                        Id = r.GetInt32(0),
                        Nombre = r.GetString(1),
                        Telefono = r.IsDBNull(2) ? "" : r.GetString(2),
                        CI = r.IsDBNull(3) ? "" : r.GetString(3),
                        Direccion = r.IsDBNull(4) ? "" : r.GetString(4),
                        Notas = r.IsDBNull(5) ? "" : r.GetString(5)
                    });
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Error", ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e) => await ShowClientDialog();

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: ClienteEntity c }) await ShowClientDialog(c);
        }

        private async Task ShowClientDialog(ClienteEntity? c = null)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = c == null ? "Nuevo Cliente" : "Editar Cliente",
                PrimaryButtonText = "Guardar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            var stack = new StackPanel { Spacing = 10, Width = 350 };
            var txtNombre = new TextBox { Header = "Nombre", Text = c?.Nombre ?? "" };
            var txtCi = new TextBox { Header = "CI/DNI", Text = c?.CI ?? "" };
            var txtTel = new TextBox { Header = "Teléfono", Text = c?.Telefono ?? "" };
            var txtDir = new TextBox { Header = "Dirección", Text = c?.Direccion ?? "" };

            stack.Children.Add(txtNombre);
            stack.Children.Add(txtCi);
            stack.Children.Add(txtTel);
            stack.Children.Add(txtDir);

            dialog.Content = stack;

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(txtNombre.Text))
                {
                    ShowInfo("Validación", "El nombre es obligatorio", InfoBarSeverity.Warning);
                    return;
                }

                await SaveClient(new ClienteEntity
                {
                    Id = c?.Id ?? 0,
                    Nombre = txtNombre.Text,
                    CI = txtCi.Text,
                    Telefono = txtTel.Text,
                    Direccion = txtDir.Text,
                    Notas = c?.Notas
                });
            }
        }

        private async Task SaveClient(ClienteEntity c)
        {
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("sp_GestionarCliente", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@Id", c.Id);
                cmd.Parameters.AddWithValue("@Nombre", c.Nombre);
                cmd.Parameters.AddWithValue("@CI", (object)c.CI ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Telefono", (object)c.Telefono ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Direccion", (object)c.Direccion ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Notas", (object)c.Notas ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Accion", "GUARDAR");

                var pRes = cmd.Parameters.Add("@Resultado", SqlDbType.Bit); pRes.Direction = ParameterDirection.Output;
                var pMsg = cmd.Parameters.Add("@Mensaje", SqlDbType.NVarChar, 500); pMsg.Direction = ParameterDirection.Output;

                await cmd.ExecuteNonQueryAsync();

                if ((bool)pRes.Value)
                {
                    ShowInfo("Éxito", pMsg.Value.ToString(), InfoBarSeverity.Success);
                    await LoadClients();
                }
                else
                {
                    ShowInfo("Error", pMsg.Value.ToString(), InfoBarSeverity.Error);
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Error", ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
             if (sender is Button { Tag: ClienteEntity c })
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Confirmar eliminación",
                    Content = $"¿Eliminar a {c.Nombre}?",
                    PrimaryButtonText = "Eliminar",
                    CloseButtonText = "Cancelar",
                    DefaultButton = ContentDialogButton.Close
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    try
                    {
                        using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                        await conn.OpenAsync();
                        var cmd = new SqlCommand("sp_GestionarCliente", conn);
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Id", c.Id);
                        cmd.Parameters.AddWithValue("@Nombre", ""); // No needed
                        cmd.Parameters.AddWithValue("@Accion", "ELIMINAR");

                        var pRes = cmd.Parameters.Add("@Resultado", SqlDbType.Bit); pRes.Direction = ParameterDirection.Output;
                        var pMsg = cmd.Parameters.Add("@Mensaje", SqlDbType.NVarChar, 500); pMsg.Direction = ParameterDirection.Output;

                        await cmd.ExecuteNonQueryAsync();

                        if ((bool)pRes.Value)
                        {
                            ShowInfo("Eliminado", pMsg.Value.ToString(), InfoBarSeverity.Success);
                            await LoadClients();
                        }
                        else
                        {
                             ShowInfo("Error", pMsg.Value.ToString(), InfoBarSeverity.Error);
                        }
                    }
                    catch (Exception ex)
                    {
                         ShowInfo("Error", ex.Message, InfoBarSeverity.Error);
                    }
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => _ = LoadClients(SearchBox.Text);
        private void RefreshButton_Click(object sender, RoutedEventArgs e) => _ = LoadClients();

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack) this.Frame.GoBack();
            else this.Frame.Navigate(typeof(SalesPage));
        }

        private void ShowInfo(string title, string? msg, InfoBarSeverity severity)
        {
            InfoBar.Title = title;
            InfoBar.Message = msg ?? string.Empty;
            InfoBar.Severity = severity;
            InfoBar.IsOpen = true;
        }
    }

    public class ClienteEntity
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string CI { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Notas { get; set; } = string.Empty;
    }
}
