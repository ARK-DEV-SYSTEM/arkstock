using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using Microsoft.UI.Xaml;

namespace ark_app1
{
    public sealed partial class AddProductDialogContent : Page
    {
        private int? _existingProductId = null;
        private Task _loadCategoriasTask;

        public AddProductDialogContent()
        {
            this.InitializeComponent();
            _loadCategoriasTask = LoadCategorias();
        }

        private async Task LoadCategorias()
        {
            try
            {
                var cats = new ObservableCollection<object>();
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("SELECT Id, Nombre FROM Categorias ORDER BY Nombre", conn);
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    cats.Add(new { Id = r.GetInt32(0), Nombre = r.GetString(1) });
                }
                CategoriaComboBox.ItemsSource = cats;
            }
            catch { /* Ignore for now */ }
        }

        public async Task LoadProductAsync(Producto p, bool isInventoryEdit = false)
        {
            await _loadCategoriasTask;

            _existingProductId = p.Id;

            CodigoTextBox.Text = p.Codigo;
            NombreTextBox.Text = p.Nombre;
            if (p.CategoriaId.HasValue)
            {
                CategoriaComboBox.SelectedValue = p.CategoriaId.Value;
            }
            TallaTextBox.Text = p.Talla;
            ColorTextBox.Text = p.Color;
            PrecioCompraBox.Value = (double)p.PrecioCompra;
            PrecioVentaBox.Value = (double)p.PrecioVenta;
            UnidadTextBox.Text = p.UnidadMedida;
            StockMinimoBox.Value = (double)p.StockMinimo;

            // En modo edición de inventario, el Código NO SE PUEDE EDITAR.
            CodigoTextBox.IsReadOnly = true;
            CodigoTextBox.IsEnabled = false; // Visual indication

            if (isInventoryEdit)
            {
                // En edición de inventario, mostramos el stock actual pero lo deshabilitamos
                // para que usen "Ajustar Stock"
                CantidadBox.Header = "Stock Actual";
                CantidadBox.Value = (double)p.Stock;
                CantidadBox.IsEnabled = false;
            }
            else
            {
                // Si es edición dentro de una compra (si alguna vez se usa así),
                // la cantidad es la cantidad a comprar.
                CantidadBox.Value = (double)p.Stock; // Stock property holds quantity in some contexts
            }
        }

        public Producto? GetProducto()
        {
            ResultInfoBar.IsOpen = false;

            if (string.IsNullOrWhiteSpace(CodigoTextBox.Text) || string.IsNullOrWhiteSpace(NombreTextBox.Text))
            {
                ShowError("Código y Nombre son obligatorios.");
                return null;
            }

            if (PrecioVentaBox.Value < 0)
            {
                ShowError("El Precio de Venta no puede ser negativo.");
                return null;
            }

            if (PrecioCompraBox.Value < 0)
            {
                ShowError("El Precio de Compra no puede ser negativo.");
                return null;
            }

            if (CantidadBox.Value < 0)
            {
                ShowError("La Cantidad/Stock no puede ser negativa.");
                return null;
            }

            return new Producto
            {
                Id = _existingProductId ?? 0,
                Codigo = CodigoTextBox.Text,
                Nombre = NombreTextBox.Text,
                CategoriaId = (int?)CategoriaComboBox.SelectedValue,
                Talla = TallaTextBox.Text,
                Color = ColorTextBox.Text,
                PrecioCompra = (decimal)PrecioCompraBox.Value,
                PrecioVenta = (decimal)PrecioVentaBox.Value,
                Stock = (decimal)CantidadBox.Value,
                UnidadMedida = UnidadTextBox.Text,
                StockMinimo = (decimal)StockMinimoBox.Value
            };
        }

        private void ShowError(string message)
        {
            ResultInfoBar.Message = message;
            ResultInfoBar.Severity = InfoBarSeverity.Warning;
            ResultInfoBar.IsOpen = true;
        }
    }
}
