using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ark_app1
{
    public sealed partial class ProvidersPage : Page
    {
        private ObservableCollection<ProveedorEntity> _proveedores = new();

        public ProvidersPage()
        {
            this.InitializeComponent();
            ProvidersGrid.ItemsSource = _proveedores;
            Loaded += async (s, e) => await LoadProviders();
        }

        private async Task LoadProviders(string filter = "")
        {
            _proveedores.Clear();
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, Nombre, RUC, Telefono, Direccion, Email, Contacto, Notas FROM Proveedores";

                if (!string.IsNullOrWhiteSpace(filter))
                {
                    cmd.CommandText += " WHERE Nombre LIKE @f OR RUC LIKE @f OR Contacto LIKE @f";
                    cmd.Parameters.AddWithValue("@f", $"%{filter}%");
                }

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    _proveedores.Add(new ProveedorEntity
                    {
                        Id = r.GetInt32(0),
                        Nombre = r.GetString(1),
                        RUC = r.IsDBNull(2) ? "" : r.GetString(2),
                        Telefono = r.IsDBNull(3) ? "" : r.GetString(3),
                        Direccion = r.IsDBNull(4) ? "" : r.GetString(4),
                        Email = r.IsDBNull(5) ? "" : r.GetString(5),
                        Contacto = r.IsDBNull(6) ? "" : r.GetString(6),
                        Notas = r.IsDBNull(7) ? "" : r.GetString(7)
                    });
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Error al cargar", ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            await ShowProviderDialog();
        }

        private async void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: ProveedorEntity p })
            {
                await ShowProviderDialog(p);
            }
        }

        private async Task ShowProviderDialog(ProveedorEntity? p = null)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = p == null ? "Nuevo Proveedor" : "Editar Proveedor",
                PrimaryButtonText = "Guardar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            var stack = new StackPanel { Spacing = 10, Width = 350 };
            var txtNombre = new TextBox { Header = "Nombre *", PlaceholderText = "Obligatorio", Text = p?.Nombre ?? "", MaxLength = 100 };
            var txtRuc = new TextBox { Header = "RUC (Opcional)", Text = p?.RUC ?? "", MaxLength = 20 };
            var txtTel = new TextBox { Header = "Teléfono (Opcional)", Text = p?.Telefono ?? "", MaxLength = 20 };
            var txtEmail = new TextBox { Header = "Email (Opcional)", Text = p?.Email ?? "", MaxLength = 100 };
            var txtContacto = new TextBox { Header = "Contacto (Opcional)", Text = p?.Contacto ?? "", MaxLength = 100 };

            stack.Children.Add(txtNombre);
            stack.Children.Add(txtRuc);
            stack.Children.Add(txtTel);
            stack.Children.Add(txtEmail);
            stack.Children.Add(txtContacto);

            dialog.Content = stack;

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(txtNombre.Text))
                {
                    ShowInfo("Validación", "El nombre es obligatorio", InfoBarSeverity.Warning);
                    return;
                }

                await SaveProvider(new ProveedorEntity
                {
                    Id = p?.Id ?? 0,
                    Nombre = txtNombre.Text,
                    RUC = txtRuc.Text,
                    Telefono = txtTel.Text,
                    Email = txtEmail.Text,
                    Contacto = txtContacto.Text
                });
            }
        }

        private async Task SaveProvider(ProveedorEntity p)
        {
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();

                if (p.Id == 0)
                {
                    cmd.CommandText = "INSERT INTO Proveedores (Nombre, RUC, Telefono, Email, Contacto) VALUES (@n, @r, @t, @e, @c)";
                }
                else
                {
                    cmd.CommandText = "UPDATE Proveedores SET Nombre=@n, RUC=@r, Telefono=@t, Email=@e, Contacto=@c WHERE Id=@id";
                    cmd.Parameters.AddWithValue("@id", p.Id);
                }

                cmd.Parameters.AddWithValue("@n", p.Nombre);
                cmd.Parameters.AddWithValue("@r", (object)p.RUC ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@t", (object)p.Telefono ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@e", (object)p.Email ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@c", (object)p.Contacto ?? DBNull.Value);

                await cmd.ExecuteNonQueryAsync();
                ShowInfo("Éxito", "Proveedor guardado correctamente", InfoBarSeverity.Success);
                await LoadProviders();
            }
            catch (Exception ex)
            {
                ShowInfo("Error", ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
             if (sender is Button { Tag: ProveedorEntity p })
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Confirmar eliminación",
                    Content = $"¿Seguro que deseas eliminar a {p.Nombre}?",
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
                        var cmd = new SqlCommand("DELETE FROM Proveedores WHERE Id = @id", conn);
                        cmd.Parameters.AddWithValue("@id", p.Id);
                        await cmd.ExecuteNonQueryAsync();
                        ShowInfo("Eliminado", "Proveedor eliminado.", InfoBarSeverity.Success);
                        await LoadProviders();
                    }
                    catch (Exception)
                    {
                         ShowInfo("Error", "No se puede eliminar (probablemente tiene compras asociadas).", InfoBarSeverity.Error);
                    }
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _ = LoadProviders(SearchBox.Text);
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _ = LoadProviders();
        }

        private void ShowInfo(string title, string msg, InfoBarSeverity severity)
        {
            InfoBar.Title = title;
            InfoBar.Message = msg;
            InfoBar.Severity = severity;
            InfoBar.IsOpen = true;
        }
    }

    public class ProveedorEntity
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string RUC { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Contacto { get; set; } = string.Empty;
        public string Notas { get; set; } = string.Empty;
    }
}
