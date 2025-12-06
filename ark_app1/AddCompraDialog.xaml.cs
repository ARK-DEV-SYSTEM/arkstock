using ark_app1.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics;

namespace ark_app1
{
    public sealed partial class AddCompraDialog : Window
    {
        private readonly ObservableCollection<ProductoCompra> _productosCompra = new();
        private readonly ObservableCollection<Proveedor> _proveedores = new();
        private int? _compraIdToEdit = null;

        public AddCompraDialog(int? compraId = null)
        {
            this.InitializeComponent();
            WindowHelper.SetDefaultIcon(this);
            _compraIdToEdit = compraId;
            ProductosDataGrid.ItemsSource = _productosCompra;

            var presenter = AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }
            AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
            AppWindow.Resize(new SizeInt32(1100, 900));
            CenterWindow();

            if (_compraIdToEdit.HasValue)
            {
                this.Title = "Editar Compra";
                HeaderTextBlock.Text = "Editar Compra";
                _ = InitializeEditAsync();
            }
            else
            {
                _ = LoadProveedores();
            }
        }

        private async Task InitializeEditAsync()
        {
            await LoadProveedores();
            await LoadCompraData();
        }

        private async Task LoadCompraData()
        {
            try
            {
                if (_compraIdToEdit == null) return;

                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();

                // 1. Get Purchase Header
                var cmd = new SqlCommand("SELECT ProveedorId FROM Compras WHERE Id = @Id", conn);
                cmd.Parameters.AddWithValue("@Id", _compraIdToEdit.Value);
                var provId = await cmd.ExecuteScalarAsync();
                if (provId != DBNull.Value && provId != null)
                {
                    ProveedorComboBox.SelectedValue = (int)provId;
                }

                // 2. Get Details
                var cmdDet = new SqlCommand(@"
                    SELECT p.Codigo, p.Nombre, p.CategoriaId, p.Talla, p.Color,
                           cd.PrecioUnitario, p.PrecioVenta, cd.Cantidad, p.UnidadMedida, p.StockMinimo, p.Id
                    FROM ComprasDetalle cd
                    INNER JOIN Productos p ON cd.ProductoId = p.Id
                    WHERE cd.CompraId = @Id", conn);
                cmdDet.Parameters.AddWithValue("@Id", _compraIdToEdit.Value);

                using var r = await cmdDet.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    _productosCompra.Add(new ProductoCompra
                    {
                        Codigo = r.GetString(0),
                        Nombre = r.GetString(1),
                        CategoriaId = r.IsDBNull(2) ? null : r.GetInt32(2),
                        Talla = r.IsDBNull(3) ? "" : r.GetString(3),
                        Color = r.IsDBNull(4) ? "" : r.GetString(4),
                        PrecioCompra = r.GetDecimal(5), // Price at moment of purchase
                        PrecioVenta = r.GetDecimal(6),
                        Cantidad = r.GetDecimal(7),
                        UnidadMedida = r.IsDBNull(8) ? "Unidad" : r.GetString(8),
                        StockMinimo = r.GetDecimal(9),
                        ProductoId = r.GetInt32(10)
                    });
                }
                UpdateTotal();
            }
            catch (Exception ex)
            {
                ShowInfoBar("Error al cargar compra", ex.Message, InfoBarSeverity.Error);
            }
        }

        private void CenterWindow()
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest)?.WorkArea;
            if (area == null) return;
            AppWindow.Move(new PointInt32(
                (area.Value.Width - AppWindow.Size.Width) / 2,
                (area.Value.Height - AppWindow.Size.Height) / 2));
        }

        private async Task LoadProveedores()
        {
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("SELECT Id, Nombre FROM Proveedores", conn);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    _proveedores.Add(new Proveedor
                    {
                        Id = reader.GetInt32(0),
                        Nombre = reader.GetString(1)
                    });
                }
                ProveedorComboBox.ItemsSource = _proveedores;
            }
            catch (Exception ex)
            {
                ShowInfoBar("Error al cargar proveedores", ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void AddProductoButton_Click(object sender, RoutedEventArgs e)
        {
            // Show a dialog to select or create a product to add to the list
            var dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = "Agregar Producto",
                PrimaryButtonText = "Agregar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                Content = new AddProductDialogContent()
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var content = dialog.Content as AddProductDialogContent;
                var producto = content?.GetProducto();

                if (producto != null)
                {
                    _productosCompra.Add(new ProductoCompra
                    {
                        Codigo = producto.Codigo,
                        Nombre = producto.Nombre,
                        CategoriaId = producto.CategoriaId,
                        Talla = producto.Talla,
                        Color = producto.Color,
                        PrecioCompra = producto.PrecioCompra,
                        PrecioVenta = producto.PrecioVenta,
                        Cantidad = producto.Stock, // Using Stock property to hold quantity for the purchase
                        UnidadMedida = producto.UnidadMedida,
                        StockMinimo = producto.StockMinimo
                    });
                    UpdateTotal();
                }
            }
        }

        private void RemoveProduct_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProductoCompra p)
            {
                _productosCompra.Remove(p);
                UpdateTotal();
            }
        }

        private void UpdateTotal()
        {
            decimal total = _productosCompra.Sum(p => p.Subtotal);
            TotalTextBlock.Text = $"Total: Bs. {total:N2}";
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProveedorComboBox.SelectedValue == null)
            {
                ShowInfoBar("Error", "Seleccione un proveedor", InfoBarSeverity.Warning);
                return;
            }

            if (_productosCompra.Count == 0)
            {
                ShowInfoBar("Error", "Agregue al menos un producto", InfoBarSeverity.Warning);
                return;
            }

            var jsonProductos = JsonConvert.SerializeObject(_productosCompra);
            int usuarioId = (Application.Current as App).CurrentUser?.Id ?? 0;
            int proveedorId = (int)ProveedorComboBox.SelectedValue;

            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();

                using var cmd = conn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;

                if (_compraIdToEdit.HasValue)
                {
                    cmd.CommandText = "sp_ModificarCompra";
                    cmd.Parameters.AddWithValue("@CompraId", _compraIdToEdit.Value);
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                }
                else
                {
                    cmd.CommandText = "sp_RegistrarCompra";
                    cmd.Parameters.AddWithValue("@UsuarioId", usuarioId);
                    var pCompraId = new SqlParameter("@CompraId", SqlDbType.Int) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(pCompraId);
                }

                cmd.Parameters.AddWithValue("@ProveedorId", proveedorId);
                cmd.Parameters.AddWithValue("@Productos", jsonProductos);

                var pResultado = new SqlParameter("@Resultado", SqlDbType.Bit) { Direction = ParameterDirection.Output };
                var pMensaje = new SqlParameter("@Mensaje", SqlDbType.NVarChar, 500) { Direction = ParameterDirection.Output };

                cmd.Parameters.Add(pResultado);
                cmd.Parameters.Add(pMensaje);

                await cmd.ExecuteNonQueryAsync();

                bool resultado = (bool)pResultado.Value;
                string mensaje = pMensaje.Value.ToString();

                if (resultado)
                {
                    ShowInfoBar("Éxito", mensaje, InfoBarSeverity.Success);
                    await Task.Delay(1500);
                    this.Close();
                }
                else
                {
                    ShowInfoBar("Error", mensaje, InfoBarSeverity.Error);
                }
            }
            catch (Exception ex)
            {
                ShowInfoBar("Error de Base de Datos", ex.Message, InfoBarSeverity.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => this.Close();

        private void ShowInfoBar(string title, string message, InfoBarSeverity severity)
        {
            ResultInfoBar.Title = title;
            ResultInfoBar.Message = message ?? string.Empty;
            ResultInfoBar.Severity = severity;
            ResultInfoBar.IsOpen = true;
        }
    }

    public class ProductoCompra
    {
        public int? ProductoId { get; set; } // Needed for edits
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int? CategoriaId { get; set; }
        public string? Talla { get; set; }
        public string? Color { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal Cantidad { get; set; }
        public string UnidadMedida { get; set; } = "Unidad";
        public decimal StockMinimo { get; set; }

        [JsonIgnore]
        public decimal Subtotal => Cantidad * PrecioCompra;
    }

    public class Proveedor
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
    }
}
