using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace ark_app1
{
    public sealed partial class SalesPage : Page
    {
        private ObservableCollection<Producto> _products = new();
        private ObservableCollection<CartItem> _cart = new();
        private int? _selectedClientId = null;

        public SalesPage()
        {
            this.InitializeComponent();
            ProductsGrid.ItemsSource = _products;
            CartGrid.ItemsSource = _cart;
            Loaded += SalesPage_Loaded;
            _cart.CollectionChanged += (s, e) => CalculateTotal();
        }

        private async void SalesPage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadProducts();
            ProductSearchBox.Focus(FocusState.Programmatic);
        }

        // --- Client Search Logic ---
        private async void ClientSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (string.IsNullOrWhiteSpace(sender.Text))
                {
                    sender.ItemsSource = null;
                    return;
                }

                try
                {
                    var suggestions = new ObservableCollection<ClientSearchResult>();
                    using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                    await conn.OpenAsync();
                    var cmd = new SqlCommand("SELECT TOP 10 Id, Nombre FROM Clientes WHERE Nombre LIKE @f ORDER BY Nombre", conn);
                    cmd.Parameters.AddWithValue("@f", $"%{sender.Text}%");
                    using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync())
                    {
                        suggestions.Add(new ClientSearchResult { Id = r.GetInt32(0), Nombre = r.GetString(1) });
                    }
                    sender.ItemsSource = suggestions;
                }
                catch { /* Ignore */ }
            }
        }

        private void ClientSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            if (args.SelectedItem is ClientSearchResult client)
            {
                _selectedClientId = client.Id;
                SelectedClientText.Text = $"Cliente: {client.Nombre}";
            }
        }

        private void ClientSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (args.ChosenSuggestion == null)
            {
                if (string.IsNullOrWhiteSpace(args.QueryText))
                {
                    _selectedClientId = null;
                    SelectedClientText.Text = "Cliente: Ocasional";
                }
                else
                {
                    _selectedClientId = null;
                    SelectedClientText.Text = "Cliente: Ocasional (No registrado)";
                }
            }
        }

        private void NewClientButton_Click(object sender, RoutedEventArgs e)
        {
             this.Frame.Navigate(typeof(ClientsPage));
        }

        // --- Product Logic ---
        private async Task LoadProducts(string filter = "")
        {
            _products.Clear();
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT Id, Codigo, Nombre, Stock, PrecioVenta FROM Productos WHERE Activo = 1 AND Stock > 0";
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    cmd.CommandText += " AND (Nombre LIKE @f OR Codigo LIKE @f)";
                    cmd.Parameters.AddWithValue("@f", $"%{filter}%");
                }

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    _products.Add(new Producto
                    {
                        Id = r.GetInt32(0),
                        Codigo = r.GetString(1),
                        Nombre = r.GetString(2),
                        Stock = r.GetDecimal(3),
                        PrecioVenta = r.GetDecimal(4)
                    });
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Error", "Error al cargar productos: " + ex.Message, InfoBarSeverity.Error);
            }
        }

        private void ProductSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _ = LoadProducts(ProductSearchBox.Text);
        }

        // --- Cart Logic ---
        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: Producto p })
            {
                if (p.Stock <= 0)
                {
                    ShowInfo("Stock", "Producto sin stock", InfoBarSeverity.Warning);
                    return;
                }

                var existing = _cart.FirstOrDefault(c => c.ProductoId == p.Id);
                if (existing != null)
                {
                    if ((decimal)existing.Cantidad + 1 > p.Stock)
                    {
                        ShowInfo("Stock", "No hay suficiente stock", InfoBarSeverity.Warning);
                        return;
                    }
                    existing.Cantidad++;
                }
                else
                {
                    _cart.Add(new CartItem
                    {
                        ProductoId = p.Id,
                        Nombre = p.Nombre,
                        PrecioUnitario = p.PrecioVenta,
                        Cantidad = 1,
                        StockMax = p.Stock
                    });
                }
                CalculateTotal();
            }
        }

        private void RemoveFromCart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: CartItem item })
            {
                _cart.Remove(item);
            }
        }

        private void CartQty_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (sender.DataContext is CartItem item)
            {
                double val = args.NewValue;
                if (double.IsNaN(val) || val < 1) val = 1;

                if (val > (double)item.StockMax)
                {
                    sender.Value = (double)item.StockMax;
                    ShowInfo("Stock", $"Stock máximo disponible: {item.StockMax}", InfoBarSeverity.Warning);
                    item.Cantidad = (double)item.StockMax;
                }
                else
                {
                    item.Cantidad = val;
                    CalculateTotal();
                }
            }
        }

        private void CalculateTotal()
        {
            if (TotalText == null) return;
            decimal subtotal = _cart.Sum(x => x.Subtotal);
            TotalText.Text = $"Bs. {subtotal:N2}"; // Just the subtotal on main screen
        }

        private void ClearCart_Click(object sender, RoutedEventArgs e)
        {
            _cart.Clear();
            CalculateTotal();
            ClientSearchBox.Text = "";
            _selectedClientId = null;
            SelectedClientText.Text = "Cliente: Ocasional";
        }

        // --- Checkout Logic ---
        private async void CheckoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_cart.Any())
            {
                ShowInfo("Carrito vacío", "Agregue productos para vender", InfoBarSeverity.Warning);
                return;
            }

            decimal cartSubtotal = _cart.Sum(x => x.Subtotal);

            // Create Dialog Controls
            var dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = "Finalizar Venta",
                PrimaryButtonText = "Confirmar Pago",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            var stack = new StackPanel { Spacing = 10 };

            // Subtotal Display
            stack.Children.Add(new TextBlock { Text = $"Subtotal: Bs. {cartSubtotal:N2}", Foreground = new SolidColorBrush(Colors.Gray) });

            // Payment Method
            var payMethodCombo = new ComboBox { Header = "Método de Pago", HorizontalAlignment = HorizontalAlignment.Stretch, SelectedIndex = 0 };
            payMethodCombo.Items.Add(new ComboBoxItem { Content = "Efectivo" });
            payMethodCombo.Items.Add(new ComboBoxItem { Content = "Tarjeta" });
            payMethodCombo.Items.Add(new ComboBoxItem { Content = "QR" });
            payMethodCombo.Items.Add(new ComboBoxItem { Content = "Transferencia" });
            stack.Children.Add(payMethodCombo);

            // Discounts
            var gridDisc = new Grid { ColumnSpacing = 10 };
            gridDisc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridDisc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var discPercentBox = new NumberBox { Header = "Desc. Global (%)", Minimum = 0, Maximum = 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
            var discAmountBox = new NumberBox { Header = "Desc. Global (Monto)", Minimum = 0, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };

            Grid.SetColumn(discPercentBox, 0);
            Grid.SetColumn(discAmountBox, 1);
            gridDisc.Children.Add(discPercentBox);
            gridDisc.Children.Add(discAmountBox);
            stack.Children.Add(gridDisc);

            // Separator
            stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Colors.LightGray), Margin = new Thickness(0, 5, 0, 5) });

            // Total to Pay
            var totalBlock = new TextBlock { Text = $"Total a Pagar: Bs. {cartSubtotal:N2}", FontWeight = Microsoft.UI.Text.FontWeights.Bold, FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(totalBlock);

            // Cash Received
            var cashBox = new NumberBox
            {
                Header = "Efectivo/Monto Recibido (Bs.)",
                PlaceholderText = "Ingrese monto",
                Minimum = 0,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                Value = (double)cartSubtotal
            };
            stack.Children.Add(cashBox);

            // Change
            var changeText = new TextBlock { Text = "Cambio: Bs. 0.00", FontSize = 18, Foreground = new SolidColorBrush(Colors.Gray), HorizontalAlignment = HorizontalAlignment.Right };
            stack.Children.Add(changeText);

            // Logic
            decimal currentTotalToPay = cartSubtotal;

            void Recalculate()
            {
                double dp = discPercentBox.Value;
                decimal p = double.IsNaN(dp) ? 0 : (decimal)dp;

                double da = discAmountBox.Value;
                decimal a = double.IsNaN(da) ? 0 : (decimal)da;

                decimal t = cartSubtotal;
                if (a > 0) t -= a;
                if (p > 0) t -= (t * p / 100);
                if (t < 0) t = 0;

                currentTotalToPay = t;
                totalBlock.Text = $"Total a Pagar: Bs. {t:N2}";

                double c = cashBox.Value;
                decimal cash = double.IsNaN(c) ? 0 : (decimal)c;

                if (cash >= t)
                {
                    changeText.Text = $"Cambio: Bs. {(cash - t):N2}";
                    changeText.Foreground = new SolidColorBrush(Colors.Green);
                    dialog.IsPrimaryButtonEnabled = true;
                }
                else
                {
                    changeText.Text = "Monto insuficiente";
                    changeText.Foreground = new SolidColorBrush(Colors.Red);
                    dialog.IsPrimaryButtonEnabled = false;
                }
            }

            discPercentBox.ValueChanged += (s, a) => Recalculate();
            discAmountBox.ValueChanged += (s, a) => Recalculate();
            cashBox.ValueChanged += (s, a) => Recalculate();

            dialog.Content = stack;

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                string method = (payMethodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Efectivo";
                double dp = discPercentBox.Value;
                double da = discAmountBox.Value;
                decimal p = double.IsNaN(dp) ? 0 : (decimal)dp;
                decimal a = double.IsNaN(da) ? 0 : (decimal)da;
                decimal cash = double.IsNaN(cashBox.Value) ? 0 : (decimal)cashBox.Value;

                await ProcessSale(cash, currentTotalToPay, method, p, a);
            }
        }

        private async Task ProcessSale(decimal efectivoRecibido, decimal totalEsperado, string tipoPago, decimal discP, decimal discA)
        {
            var json = JsonSerializer.Serialize(_cart.Select(c => new
            {
                c.ProductoId,
                Cantidad = (decimal)c.Cantidad,
                c.PrecioUnitario,
                DescuentoPorcentaje = 0,
                DescuentoMonto = 0
            }));

            try
            {
                int userId = (Application.Current as App)?.CurrentUser?.Id ?? 1;

                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("sp_RegistrarVenta_v2", conn);
                cmd.CommandType = CommandType.StoredProcedure;

                cmd.Parameters.AddWithValue("@UsuarioId", userId);
                cmd.Parameters.AddWithValue("@ClienteId", (object?)_selectedClientId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Productos", json);
                cmd.Parameters.AddWithValue("@EfectivoRecibido", efectivoRecibido);
                cmd.Parameters.AddWithValue("@TipoPago", tipoPago);
                cmd.Parameters.AddWithValue("@DescuentoGlobalPorcentaje", discP);
                cmd.Parameters.AddWithValue("@DescuentoGlobalMonto", discA);

                var pRes = cmd.Parameters.Add("@Resultado", SqlDbType.Bit); pRes.Direction = ParameterDirection.Output;
                var pMsg = cmd.Parameters.Add("@Mensaje", SqlDbType.NVarChar, 500); pMsg.Direction = ParameterDirection.Output;
                var pId = cmd.Parameters.Add("@VentaId", SqlDbType.Int); pId.Direction = ParameterDirection.Output;

                await cmd.ExecuteNonQueryAsync();

                bool success = (bool)pRes.Value;
                string msg = pMsg.Value.ToString();

                if (success)
                {
                    ShowInfo("Venta Exitosa", msg, InfoBarSeverity.Success);

                    // Generate Ticket
                    try
                    {
                        string clientName = SelectedClientText.Text.Replace("Cliente: ", "");
                        string userName = (Application.Current as App)?.CurrentUser?.NombreCompleto ?? "Usuario";

                        var ticket = new TicketData
                        {
                            SaleId = (int)pId.Value,
                            ClientName = clientName,
                            UserName = userName,
                            Date = DateTime.Now,
                            Items = _cart.Select(c => new TicketItem
                            {
                                Name = c.Nombre,
                                Quantity = (decimal)c.Cantidad,
                                Price = c.PrecioUnitario,
                                Subtotal = c.Subtotal
                            }).ToList(),
                            Subtotal = _cart.Sum(x => x.Subtotal),
                            DiscountTotal = _cart.Sum(x => x.Subtotal) - totalEsperado,
                            Total = totalEsperado,
                            Cash = efectivoRecibido,
                            Change = efectivoRecibido - totalEsperado,
                            PaymentMethod = tipoPago
                        };
                        _ = TicketGenerator.GenerateAndOpenAsync(ticket);
                    }
                    catch (Exception ex)
                    {
                        ShowInfo("Aviso", "Venta guardada, pero error al generar ticket: " + ex.Message, InfoBarSeverity.Warning);
                    }

                    ClearCart_Click(null, null);
                    await LoadProducts(ProductSearchBox.Text);
                }
                else
                {
                    ShowInfo("Error", msg, InfoBarSeverity.Error);
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Error Crítico", ex.Message, InfoBarSeverity.Error);
            }
        }

        private void ShowInfo(string title, string? msg, InfoBarSeverity severity)
        {
            CartInfoBar.Title = title;
            CartInfoBar.Message = msg ?? string.Empty;
            CartInfoBar.Severity = severity;
            CartInfoBar.IsOpen = true;
        }
    }

    public class CartItem : INotifyPropertyChanged
    {
        public int ProductoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal PrecioUnitario { get; set; }

        private double _cantidad;
        public double Cantidad
        {
            get => _cantidad;
            set
            {
                if (_cantidad != value)
                {
                    _cantidad = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Subtotal));
                    OnPropertyChanged(nameof(SubtotalDisplay));
                }
            }
        }

        public decimal StockMax { get; set; }

        public decimal Subtotal => PrecioUnitario * (decimal)Cantidad;
        public string SubtotalDisplay => $"Bs. {Subtotal:N2}";

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class ClientSearchResult
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public override string ToString() => Nombre;
    }
}
