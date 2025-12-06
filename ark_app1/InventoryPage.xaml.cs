using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace ark_app1
{
    public sealed partial class InventoryPage : Page
    {
        private readonly ObservableCollection<Producto> _productos = new();
        private readonly ObservableCollection<Compra> _compras = new();
        private int _currentPage = 1;
        private int _pageSize = 20;

        public InventoryPage()
        {
            this.InitializeComponent();
            Loaded += async (s, e) => await LoadInitialData();
            ProductsDataGrid.ItemsSource = _productos;
            ComprasDataGrid.ItemsSource = _compras;
        }

        private async Task LoadInitialData()
        {
            await LoadProductos();
            await LoadCompras();
        }

        private async Task LoadProductos(string filter = null)
        {
            _productos.Clear();
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT p.Id, p.Codigo, p.Nombre, p.CategoriaId, c.Nombre as CategoriaNombre, p.Talla, p.Color,
                                          p.PrecioCompra, p.PrecioVenta, p.Stock, p.UnidadMedida, p.StockMinimo, p.FechaRegistro
                                   FROM Productos p LEFT JOIN Categorias c ON p.CategoriaId = c.Id
                                   WHERE p.Activo = 1";
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    cmd.CommandText += " AND (p.Nombre LIKE @f OR p.Codigo LIKE @f)";
                    cmd.Parameters.AddWithValue("@f", $"%{filter}%");
                }
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    _productos.Add(new Producto
                    {
                        Id = r.GetInt32(0),
                        Codigo = r.GetString(1),
                        Nombre = r.GetString(2),
                        CategoriaId = r.IsDBNull(3) ? null : (int?)r.GetInt32(3),
                        CategoriaNombre = r.IsDBNull(4) ? "Sin Categoría" : r.GetString(4),
                        Talla = r.IsDBNull(5) ? "" : r.GetString(5),
                        Color = r.IsDBNull(6) ? "" : r.GetString(6),
                        PrecioCompra = r.GetDecimal(7),
                        PrecioVenta = r.GetDecimal(8),
                        Stock = r.GetDecimal(9),
                        UnidadMedida = r.GetString(10),
                        StockMinimo = r.GetDecimal(11),
                        FechaRegistro = r.GetDateTime(12)
                    });
                }
            }
            catch (Exception ex)
            {
                ShowInfoBar("Error al cargar productos", ex.Message, InfoBarSeverity.Error);
            }
        }

        private async Task LoadCompras()
        {
            _compras.Clear();
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var offset = (_currentPage - 1) * _pageSize;
                var cmd = new SqlCommand(@"SELECT c.Id, c.Fecha, p.Nombre as Proveedor, c.Total, u.NombreCompleto as Usuario, c.Estado
                                           FROM Compras c LEFT JOIN Proveedores p ON c.ProveedorId = p.Id
                                           LEFT JOIN Usuarios u ON c.UsuarioId = u.Id
                                           ORDER BY c.Fecha DESC
                                           OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY", conn);
                cmd.Parameters.AddWithValue("@Offset", offset);
                cmd.Parameters.AddWithValue("@Limit", _pageSize);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    _compras.Add(new Compra
                    {
                        Id = r.GetInt32(0),
                        Fecha = r.GetDateTime(1),
                        Proveedor = r.IsDBNull(2) ? "Sin Proveedor" : r.GetString(2),
                        Total = r.GetDecimal(3),
                        Usuario = r.GetString(4),
                        Estado = r.GetString(5)
                    });
                }
                PageInfoText.Text = $"Página {_currentPage}";
            }
            catch (Exception ex)
            {
                ShowInfoBar("Error al cargar compras", ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                await LoadCompras();
            }
        }

        private async void NextPage_Click(object sender, RoutedEventArgs e)
        {
            _currentPage++;
            await LoadCompras();
        }

        private async void ViewDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Compra compra })
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = this.Content.XamlRoot,
                    Title = $"Detalle Compra #{compra.Id}",
                    CloseButtonText = "Cerrar",
                    DefaultButton = ContentDialogButton.Close
                };

                var detailsList = new System.Text.StringBuilder();
                try
                {
                    using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                    await conn.OpenAsync();
                    var cmd = new SqlCommand(@"SELECT p.Nombre, cd.Cantidad, cd.PrecioUnitario, cd.Subtotal
                                               FROM ComprasDetalle cd JOIN Productos p ON cd.ProductoId = p.Id
                                               WHERE cd.CompraId = @Id", conn);
                    cmd.Parameters.AddWithValue("@Id", compra.Id);
                    using var r = await cmd.ExecuteReaderAsync();
                    while(await r.ReadAsync())
                    {
                        detailsList.AppendLine($"- {r.GetString(0)}: {r.GetDecimal(1)} x {r.GetDecimal(2):C2} = {r.GetDecimal(3):C2}");
                    }
                }
                catch { detailsList.AppendLine("Error al cargar detalles."); }

                dialog.Content = new ScrollViewer { Content = new TextBlock { Text = detailsList.ToString(), TextWrapping = TextWrapping.Wrap } };
                await dialog.ShowAsync();
            }
        }

        private bool _isDialogOpen = false;

        private void RegistrarCompraButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDialogOpen) return;
            _isDialogOpen = true;
            var dialog = new AddCompraDialog();
            dialog.Activate();
            dialog.Closed += async (s, args) =>
            {
                _isDialogOpen = false;
                await LoadInitialData();
            };
        }

        private void AjusteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDialogOpen) return;
            _isDialogOpen = true;
            var dialog = new AdjustmentDialog();
            dialog.Activate();
            dialog.Closed += async (s, args) =>
            {
                _isDialogOpen = false;
                await LoadProductos();
            };
        }

        private void CategoriesButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(CategoriesPage));
        }

        private void EditCompraButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDialogOpen) return;
            if (sender is not Button { Tag: Compra compra }) return;

            _isDialogOpen = true;
            var dialog = new AddCompraDialog(compra.Id);
            dialog.Activate();
            dialog.Closed += async (s, args) =>
            {
                _isDialogOpen = false;
                await LoadCompras();
            };
        }

        private async void EditProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isDialogOpen) return;
            if (sender is not Button { Tag: Producto producto }) return;

            _isDialogOpen = true;

            var dialogContent = new AddProductDialogContent();
            await dialogContent.LoadProductAsync(producto, isInventoryEdit: true);

            var dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = "Editar Producto",
                PrimaryButtonText = "Guardar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                Content = dialogContent
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var updatedProducto = dialogContent.GetProducto();
                if (updatedProducto != null)
                {
                    await UpdateProductAsync(updatedProducto);
                    await LoadProductos();
                }
            }
            _isDialogOpen = false;
        }

        private async Task UpdateProductAsync(Producto p)
        {
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandText = "sp_GestionarProducto";

                cmd.Parameters.AddWithValue("@Id", p.Id);
                cmd.Parameters.AddWithValue("@Codigo", p.Codigo);
                cmd.Parameters.AddWithValue("@Nombre", p.Nombre);
                cmd.Parameters.AddWithValue("@CategoriaId", (object)p.CategoriaId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Talla", (object)p.Talla ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Color", (object)p.Color ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PrecioCompra", p.PrecioCompra);
                cmd.Parameters.AddWithValue("@PrecioVenta", p.PrecioVenta);
                cmd.Parameters.AddWithValue("@UnidadMedida", (object)p.UnidadMedida ?? "Unidad");
                cmd.Parameters.AddWithValue("@StockMinimo", p.StockMinimo);

                var pResultado = new SqlParameter("@Resultado", System.Data.SqlDbType.Bit) { Direction = System.Data.ParameterDirection.Output };
                var pMensaje = new SqlParameter("@Mensaje", System.Data.SqlDbType.NVarChar, 500) { Direction = System.Data.ParameterDirection.Output };

                cmd.Parameters.Add(pResultado);
                cmd.Parameters.Add(pMensaje);

                await cmd.ExecuteNonQueryAsync();

                if (!(bool)pResultado.Value)
                {
                    ShowInfoBar("Error al actualizar", pMensaje.Value.ToString(), InfoBarSeverity.Error);
                }
                else
                {
                    ShowInfoBar("Éxito", "Producto actualizado.", InfoBarSeverity.Success);
                }
            }
            catch (Exception ex)
            {
                ShowInfoBar("Error", ex.Message, InfoBarSeverity.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _ = LoadProductos(SearchTextBox.Text);
        }

        private void ShowInfoBar(string title, string? message, InfoBarSeverity severity)
        {
            InfoBar.Title = title;
            InfoBar.Message = message ?? string.Empty;
            InfoBar.Severity = severity;
            InfoBar.IsOpen = true;
        }
    }

    public class Compra
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string Proveedor { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }
}