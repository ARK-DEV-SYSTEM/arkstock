using ark_app1.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading.Tasks;
using Windows.Graphics;

namespace ark_app1
{
    public sealed partial class AdjustmentDialog : Window
    {
        private ObservableCollection<Producto> _products = new();

        public AdjustmentDialog()
        {
            this.InitializeComponent();
            WindowHelper.SetDefaultIcon(this);
            var presenter = AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            AppWindow.Resize(new SizeInt32(700, 700));
            CenterWindow();
            _ = LoadProducts();
        }

        private void CenterWindow()
        {
            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
            if (displayArea != null)
            {
                var workArea = displayArea.WorkArea;
                AppWindow.Move(new PointInt32((workArea.Width - 500) / 2, (workArea.Height - 500) / 2));
            }
        }

        private async Task LoadProducts()
        {
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("SELECT Id, Nombre, Stock, Codigo FROM Productos WHERE Activo = 1 ORDER BY Nombre", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    _products.Add(new Producto
                    {
                        Id = r.GetInt32(0),
                        Nombre = $"{r.GetString(3)} - {r.GetString(1)}", // Codigo - Nombre
                        Stock = r.GetDecimal(2)
                    });
                }
                ProductComboBox.ItemsSource = _products;
            }
            catch (Exception ex) { ShowInfo("Error", ex.Message, InfoBarSeverity.Error); }
        }

        private void ProductComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProductComboBox.SelectedItem is Producto p)
                StockActualText.Text = $"Stock Actual: {p.Stock}";
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProductComboBox.SelectedValue == null) { ShowInfo("Validación", "Seleccione un producto", InfoBarSeverity.Warning); return; }
            if (CantidadBox.Value == 0) { ShowInfo("Validación", "La cantidad no puede ser 0", InfoBarSeverity.Warning); return; }
            if (string.IsNullOrWhiteSpace(MotivoTextBox.Text)) { ShowInfo("Validación", "Ingrese un motivo", InfoBarSeverity.Warning); return; }

            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("sp_RegistrarAjuste", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                var currentUser = (Application.Current as App)?.CurrentUser;
                cmd.Parameters.AddWithValue("@UsuarioId", currentUser?.Id ?? 1);
                cmd.Parameters.AddWithValue("@ProductoId", (int)ProductComboBox.SelectedValue);
                cmd.Parameters.AddWithValue("@Cantidad", (decimal)CantidadBox.Value);
                cmd.Parameters.AddWithValue("@Motivo", MotivoTextBox.Text);

                var pRes = cmd.Parameters.Add("@Resultado", SqlDbType.Bit); pRes.Direction = ParameterDirection.Output;
                var pMsg = cmd.Parameters.Add("@Mensaje", SqlDbType.NVarChar, 500); pMsg.Direction = ParameterDirection.Output;

                await cmd.ExecuteNonQueryAsync();

                if ((bool)pRes.Value)
                {
                    ShowInfo("Éxito", pMsg.Value.ToString(), InfoBarSeverity.Success);
                    await Task.Delay(1000);
                    this.Close();
                }
                else ShowInfo("Error", pMsg.Value.ToString(), InfoBarSeverity.Error);
            }
            catch (Exception ex) { ShowInfo("Error", ex.Message, InfoBarSeverity.Error); }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => this.Close();
        private void ShowInfo(string title, string? msg, InfoBarSeverity severity) { InfoBar.Title = title; InfoBar.Message = msg ?? string.Empty; InfoBar.Severity = severity; InfoBar.IsOpen = true; }
    }
}
