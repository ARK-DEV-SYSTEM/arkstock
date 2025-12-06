using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Data;

namespace ark_app1
{
    public sealed partial class CategoriesPage : Page
    {
        private ObservableCollection<CategoriaEntity> _cats = new();

        public CategoriesPage()
        {
            this.InitializeComponent();
            CatsList.ItemsSource = _cats;
            Loaded += async (s, e) => await LoadCats();
        }

        private async Task LoadCats(string filter = "")
        {
            _cats.Clear();
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, Nombre FROM Categorias";
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    cmd.CommandText += " WHERE Nombre LIKE @f";
                    cmd.Parameters.AddWithValue("@f", $"%{filter}%");
                }
                cmd.CommandText += " ORDER BY Nombre";

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    _cats.Add(new CategoriaEntity { Id = r.GetInt32(0), Nombre = r.GetString(1) });
                }
            }
            catch (Exception ex) { ShowInfo("Error", ex.Message, InfoBarSeverity.Error); }
        }

        private async void AddButton_Click(object sender, RoutedEventArgs e) => await ShowDialog();
        private async void EditButton_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: CategoriaEntity c }) await ShowDialog(c); }

        private async Task ShowDialog(CategoriaEntity? c = null)
        {
            var txt = new TextBox { Header = "Nombre de la Categoría", Text = c?.Nombre ?? "" };
            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = c == null ? "Nueva Categoría" : "Editar Categoría",
                Content = txt,
                PrimaryButtonText = "Guardar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(txt.Text))
                {
                    ShowInfo("Validación", "El nombre es obligatorio", InfoBarSeverity.Warning);
                    return;
                }
                await SaveCat(c?.Id ?? 0, txt.Text, "GUARDAR");
            }
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: CategoriaEntity c })
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "¿Eliminar?",
                    Content = $"Se eliminará la categoría '{c.Nombre}'.",
                    PrimaryButtonText = "Eliminar",
                    CloseButtonText = "Cancelar"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    await SaveCat(c.Id, "", "ELIMINAR");
            }
        }

        private async Task SaveCat(int id, string nombre, string accion)
        {
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("sp_GestionarCategoria", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Id", id);
                cmd.Parameters.AddWithValue("@Nombre", nombre);
                cmd.Parameters.AddWithValue("@Accion", accion);

                var pRes = cmd.Parameters.Add("@Resultado", SqlDbType.Bit); pRes.Direction = ParameterDirection.Output;
                var pMsg = cmd.Parameters.Add("@Mensaje", SqlDbType.NVarChar, 500); pMsg.Direction = ParameterDirection.Output;

                await cmd.ExecuteNonQueryAsync();

                if ((bool)pRes.Value)
                {
                    ShowInfo("Éxito", pMsg.Value.ToString(), InfoBarSeverity.Success);
                    await LoadCats();
                }
                else ShowInfo("Error", pMsg.Value.ToString(), InfoBarSeverity.Error);
            }
            catch (Exception ex) { ShowInfo("Error", ex.Message, InfoBarSeverity.Error); }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => _ = LoadCats(SearchBox.Text);
        private void ShowInfo(string title, string? msg, InfoBarSeverity severity) { InfoBar.Title = title; InfoBar.Message = msg ?? string.Empty; InfoBar.Severity = severity; InfoBar.IsOpen = true; }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.Frame.CanGoBack) this.Frame.GoBack();
            else this.Frame.Navigate(typeof(InventoryPage));
        }
    }

    public class CategoriaEntity { public int Id { get; set; } public string Nombre { get; set; } = string.Empty; }
}
