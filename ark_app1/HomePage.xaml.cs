using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage.Pickers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Windows.Storage;
using Windows.System;
using System.IO;
using WinRT.Interop;

namespace ark_app1
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            this.InitializeComponent();
            Loaded += async (s, e) => await LoadDashboardData();
        }

        private async Task LoadDashboardData()
        {
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("sp_ObtenerDatosReporte", conn);
                using var r = await cmd.ExecuteReaderAsync();

                var salesBars = new List<ChartBar>();
                var profitBars = new List<ChartBar>();
                decimal maxSale = 1, maxProfit = 1;

                // 1. Ventas últimos 7 días
                while (await r.ReadAsync())
                {
                    var date = r.GetDateTime(0);
                    var sale = r.GetDecimal(1);
                    var profit = r.GetDecimal(2);

                    if (sale > maxSale) maxSale = sale;
                    if (profit > maxProfit) maxProfit = profit;

                    string label = date.ToString("dd/MM");
                    salesBars.Add(new ChartBar { Label = label, RawValue = sale, Tooltip = $"Ventas {label}: Bs. {sale:N2}" });
                    profitBars.Add(new ChartBar { Label = label, RawValue = profit, Tooltip = $"Ganancia {label}: Bs. {profit:N2}" });
                }

                // Normalizar barras
                foreach (var b in salesBars) b.Height = maxSale > 0 ? (double)(b.RawValue / maxSale) * 150 : 0;
                foreach (var b in profitBars) b.Height = maxProfit > 0 ? (double)(b.RawValue / maxProfit) * 150 : 0;

                SalesChart.ItemsSource = salesBars;
                ProfitChart.ItemsSource = profitBars;

                await r.NextResultAsync();
                await r.NextResultAsync(); 
                await r.NextResultAsync(); 
                await r.NextResultAsync(); 
                await r.NextResultAsync(); 

                if (await r.ReadAsync())
                {
                    TxtTotalProducts.Text = r.GetInt32(0).ToString("N0");
                    TxtTotalSales.Text = $"Bs. {r.GetDecimal(1):N2}";
                    TxtTotalClients.Text = r.GetInt32(2).ToString("N0");
                }
            }
            catch (Exception ex)
            {
                ShowInfo("Error", ex.Message, InfoBarSeverity.Error);
            }
        }

        private async void EditCompany_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = this.Content.XamlRoot,
                Title = "Configurar Empresa",
                PrimaryButtonText = "Guardar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary
            };

            var stack = new StackPanel { Spacing = 10, Width = 350 };
            var txtNombre = new TextBox { Header = "Nombre Empresa", PlaceholderText = "Requerido" };
            var txtTel = new TextBox { Header = "Teléfono" };
            var txtTel2 = new TextBox { Header = "Teléfono Secundario" };
            var txtDir = new TextBox { Header = "Dirección" };
            var txtEmail = new TextBox { Header = "Email" };
            var txtWeb = new TextBox { Header = "Sitio Web" };

            // Load existing
            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("SELECT TOP 1 Nombre, Telefono, TelefonoSecundario, Direccion, Email, SitioWeb FROM Empresa", conn);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    txtNombre.Text = r.GetString(0);
                    if(!r.IsDBNull(1)) txtTel.Text = r.GetString(1);
                    if(!r.IsDBNull(2)) txtTel2.Text = r.GetString(2);
                    if(!r.IsDBNull(3)) txtDir.Text = r.GetString(3);
                    if(!r.IsDBNull(4)) txtEmail.Text = r.GetString(4);
                    if(!r.IsDBNull(5)) txtWeb.Text = r.GetString(5);
                }
            }
            catch { }

            stack.Children.Add(txtNombre);
            stack.Children.Add(txtTel);
            stack.Children.Add(txtTel2);
            stack.Children.Add(txtDir);
            stack.Children.Add(txtEmail);
            stack.Children.Add(txtWeb);
            dialog.Content = stack;

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (string.IsNullOrWhiteSpace(txtNombre.Text))
                {
                    ShowInfo("Error", "El nombre es obligatorio", InfoBarSeverity.Error);
                    return;
                }

                try
                {
                    using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                    await conn.OpenAsync();
                    var cmd = new SqlCommand("sp_GestionarEmpresa", conn);
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Nombre", txtNombre.Text);
                    cmd.Parameters.AddWithValue("@Telefono", txtTel.Text);
                    cmd.Parameters.AddWithValue("@TelefonoSecundario", txtTel2.Text);
                    cmd.Parameters.AddWithValue("@Direccion", txtDir.Text);
                    cmd.Parameters.AddWithValue("@Email", txtEmail.Text);
                    cmd.Parameters.AddWithValue("@SitioWeb", txtWeb.Text);

                    var pRes = new SqlParameter("@Resultado", System.Data.SqlDbType.Bit) { Direction = System.Data.ParameterDirection.Output };
                    var pMsg = new SqlParameter("@Mensaje", System.Data.SqlDbType.NVarChar, 500) { Direction = System.Data.ParameterDirection.Output };
                    cmd.Parameters.Add(pRes);
                    cmd.Parameters.Add(pMsg);

                    await cmd.ExecuteNonQueryAsync();
                    ShowInfo("Éxito", pMsg.Value.ToString(), InfoBarSeverity.Success);
                }
                catch (Exception ex)
                {
                    ShowInfo("Error", ex.Message, InfoBarSeverity.Error);
                }
            }
        }

        private async void GenerateReport_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("PDF Document", new List<string> { ".pdf" });
            picker.SuggestedFileName = $"Reporte_General_{DateTime.Now:yyyyMMdd_HHmm}";
            var hwnd = WindowNative.GetWindowHandle(App.Window);
            InitializeWithWindow.Initialize(picker, hwnd);
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;

            try
            {
                var ventasDiarias = new List<(DateTime Fecha, decimal Total, decimal Ganancia)>();
                decimal totalSemana = 0;
                var bajoStock = new List<(string Codigo, string Nombre, decimal Stock, decimal Min)>();
                var ultimasVentas = new List<(int Id, DateTime Fecha, string Cliente, decimal Total, string Estado)>();
                var detalleVentas = new List<(int VentaId, string Codigo, string Producto, decimal Cant, decimal Precio, decimal Subtotal)>();
                var totales = (Productos: 0, VentasHist: 0m, Clientes: 0, StockValor: 0m);
                var topProductos = new List<(string Nombre, decimal Cantidad, decimal Total)>();

                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("sp_ObtenerDatosReporte", conn);
                using var r = await cmd.ExecuteReaderAsync();

                while (await r.ReadAsync()) ventasDiarias.Add((r.GetDateTime(0), r.GetDecimal(1), r.GetDecimal(2)));
                await r.NextResultAsync();
                if (await r.ReadAsync()) totalSemana = r.GetDecimal(0);
                await r.NextResultAsync();
                while (await r.ReadAsync()) bajoStock.Add((r.GetString(0), r.GetString(1), r.GetDecimal(2), r.GetDecimal(3)));
                await r.NextResultAsync();
                while (await r.ReadAsync()) ultimasVentas.Add((r.GetInt32(0), r.GetDateTime(1), r.GetString(2), r.GetDecimal(3), r.GetString(4)));
                await r.NextResultAsync();
                while (await r.ReadAsync()) detalleVentas.Add((r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4), r.GetDecimal(5)));
                await r.NextResultAsync();
                if (await r.ReadAsync())
                {
                    totales.Productos = r.GetInt32(0);
                    totales.VentasHist = r.GetDecimal(1);
                    totales.Clientes = r.GetInt32(2);
                    totales.StockValor = r.GetDecimal(3);
                }
                await r.NextResultAsync();
                while (await r.ReadAsync()) topProductos.Add((r.GetString(0), r.GetDecimal(1), r.GetDecimal(2)));

                QuestPDF.Settings.License = LicenseType.Community;
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(2, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header().Column(col =>
                        {
                            col.Item().Text("REPORTE GENERAL DEL SISTEMA").FontSize(22).Bold();
                            col.Item().AlignRight().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");
                        });

                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Background(Colors.Grey.Lighten2).Padding(8).Text($"Productos: {totales.Productos:N0}").Bold();
                                row.RelativeItem().Background(Colors.Grey.Lighten2).Padding(8).Text($"Ventas Totales: Bs. {totales.VentasHist:N2}").Bold();
                                row.RelativeItem().Background(Colors.Grey.Lighten2).Padding(8).Text($"Clientes: {totales.Clientes:N0}").Bold();
                                row.RelativeItem().Background(Colors.Grey.Lighten2).Padding(8).Text($"Valor Stock: Bs. {totales.StockValor:N2}").Bold();
                            });

                            col.Item().PaddingTop(15).Text("Ventas Últimos 7 Días").FontSize(16).Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Header(h => { h.Cell().Text("Fecha"); h.Cell().Text("Total"); h.Cell().Text("Ganancia"); });
                                foreach (var v in ventasDiarias)
                                {
                                    table.Cell().Text(v.Fecha.ToString("dd/MM"));
                                    table.Cell().Text($"Bs. {v.Total:N2}");
                                    table.Cell().Text($"Bs. {v.Ganancia:N2}");
                                }
                                table.Cell().ColumnSpan(3).AlignRight().Text($"TOTAL SEMANA: Bs. {totalSemana:N2}").Bold();
                            });

                            col.Item().PaddingTop(20).Text("Bajo Stock").FontSize(16).Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(3); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Header(h => { h.Cell().Text("Cód"); h.Cell().Text("Producto"); h.Cell().Text("Stock"); h.Cell().Text("Mín"); });
                                foreach (var p in bajoStock)
                                {
                                    table.Cell().Text(p.Codigo);
                                    table.Cell().Text(p.Nombre);
                                    table.Cell().Text(p.Stock.ToString("N0"));
                                    table.Cell().Text(p.Min.ToString("N0"));
                                }
                            });

                            col.Item().PaddingTop(20).Text("Últimas 20 Ventas").FontSize(16).Bold();
                            foreach (var venta in ultimasVentas)
                            {
                                col.Item().Background(Colors.Grey.Lighten3).Padding(8).Column(c =>
                                {
                                    c.Item().Text($"Venta #{venta.Id} • {venta.Fecha:dd/MM/yyyy HH:mm} • {venta.Cliente} • Total: Bs. {venta.Total:N2} • {venta.Estado}").Bold();
                                    var det = detalleVentas.Where(x => x.VentaId == venta.Id).ToList();
                                    if (det.Any())
                                    {
                                        c.Item().Table(t =>
                                        {
                                            t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(3); cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn(); });
                                            t.Header(h => { h.Cell().Text("Cód"); h.Cell().Text("Producto"); h.Cell().Text("Cant"); h.Cell().Text("P.U."); h.Cell().Text("Subtotal"); });
                                            foreach (var d in det)
                                            {
                                                t.Cell().Text(d.Codigo);
                                                t.Cell().Text(d.Producto);
                                                t.Cell().Text(d.Cant.ToString("N2"));
                                                t.Cell().Text($"Bs. {d.Precio:N2}");
                                                t.Cell().Text($"Bs. {d.Subtotal:N2}");
                                            }
                                        });
                                    }
                                });
                            }

                            col.Item().PaddingTop(20).Text("Top 10 Más Vendidos (30 días)").FontSize(16).Bold();
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(); c.RelativeColumn(); });
                                table.Header(h => { h.Cell().Text("Producto"); h.Cell().Text("Cantidad"); h.Cell().Text("Total"); });
                                foreach (var t in topProductos)
                                {
                                    table.Cell().Text(t.Nombre);
                                    table.Cell().Text(t.Cantidad.ToString("N0"));
                                    table.Cell().Text($"Bs. {t.Total:N2}");
                                }
                            });
                        });

                        page.Footer().AlignCenter().Text(x => x.CurrentPageNumber());
                    });
                }).GeneratePdf(file.Path);

                await Launcher.LaunchFileAsync(file);
                ShowInfo("Éxito", "Reporte generado correctamente", InfoBarSeverity.Success);
            }
            catch (Exception ex)
            {
                ShowInfo("Error", ex.Message, InfoBarSeverity.Error);
            }
        }

        class LowStockItem
        {
            public string Codigo { get; set; } = string.Empty;
            public string Nombre { get; set; } = string.Empty;
            public decimal Stock { get; set; }
            public decimal Min { get; set; }
        }

        class RecentSaleItem
        {
            public int Id { get; set; }
            public DateTime Fecha { get; set; }
            public string Cliente { get; set; } = string.Empty;
            public decimal Total { get; set; }
            public string Estado { get; set; } = string.Empty;
        }

        private void ShowInfo(string title, string? msg, InfoBarSeverity severity)
        {
            ResultInfoBar.Title = title;
            ResultInfoBar.Message = msg ?? string.Empty;
            ResultInfoBar.Severity = severity;
            ResultInfoBar.IsOpen = true;
        }
    }

    public class ChartBar
    {
        public string Label { get; set; } = string.Empty;
        public decimal RawValue { get; set; }
        public double Height { get; set; }
        public string Tooltip { get; set; } = string.Empty;
    }
}
