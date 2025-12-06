using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using System;
using Microsoft.Data.SqlClient;

namespace ark_app1
{
    public class TicketData
    {
        public int SaleId { get; set; }
        public string ClientName { get; set; } = "Cliente Ocasional";
        public string UserName { get; set; } = "Cajero";
        public DateTime Date { get; set; }
        public List<TicketItem> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal DiscountTotal { get; set; }
        public decimal Total { get; set; }
        public decimal Cash { get; set; }
        public decimal Change { get; set; }
        public string PaymentMethod { get; set; } = "Efectivo";
    }

    public class TicketItem
    {
        public string Name { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Subtotal { get; set; }
    }

    public static class TicketGenerator
    {
        public static async Task GenerateAndOpenAsync(TicketData data)
        {
            // Configure License (Community is free for individuals/small companies)
            QuestPDF.Settings.License = LicenseType.Community;

            string companyName = "Ticket Venta";
            string? companyAddress = null;
            string? companyPhone = null;

            try
            {
                using var conn = new SqlConnection(DatabaseManager.ConnectionString);
                await conn.OpenAsync();
                var cmd = new SqlCommand("SELECT TOP 1 Nombre, Direccion, Telefono FROM Empresa", conn);
                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    if (!r.IsDBNull(0)) companyName = r.GetString(0);
                    if (!r.IsDBNull(1)) companyAddress = r.GetString(1);
                    if (!r.IsDBNull(2)) companyPhone = r.GetString(2);
                }
            }
            catch { /* Use defaults */ }

            // Generate Document
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Thermal printer width 80mm
                    page.ContinuousSize(226.77f);   // 80 mm en puntos
                    page.Margin(14.17f);            // 5 mm en puntos
                    page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8));

                    // Header
                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text(companyName).Bold().FontSize(10);
                        if (!string.IsNullOrEmpty(companyAddress))
                            col.Item().AlignCenter().Text(companyAddress);
                        if (!string.IsNullOrEmpty(companyPhone))
                            col.Item().AlignCenter().Text($"Tel: {companyPhone}");

                        col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Black);

                        col.Item().AlignCenter().Text($"Venta #{data.SaleId}").Bold();
                        col.Item().AlignCenter().Text($"{data.Date:dd/MM/yyyy HH:mm:ss}");

                        col.Item().Text($"Cliente: {data.ClientName}");
                        col.Item().Text($"Atendido: {data.UserName}");

                        col.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Black);
                    });

                    // Content (Items)
                    page.Content().PaddingVertical(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Product Name
                            columns.RelativeColumn(2); // Qty x Price
                            columns.RelativeColumn(2); // Total
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("PROD").Bold();
                            header.Cell().Text("CANT").Bold();
                            header.Cell().AlignRight().Text("TOT").Bold();
                        });

                        foreach (var item in data.Items)
                        {
                            table.Cell().Text(item.Name);
                            table.Cell().Text($"{item.Quantity:0.##}x{item.Price:0.00}");
                            table.Cell().AlignRight().Text($"{item.Subtotal:N2}");
                        }
                    });

                    // Footer
                    page.Footer().Column(col =>
                    {
                        col.Item().PaddingVertical(2).LineHorizontal(1).LineColor(Colors.Black);

                        col.Item().AlignRight().Text($"Subtotal: {data.Subtotal:N2}");
                        if (data.DiscountTotal > 0)
                            col.Item().AlignRight().Text($"Descuento: -{data.DiscountTotal:N2}");

                        col.Item().AlignRight().Text($"TOTAL: {data.Total:N2}").Bold().FontSize(10);

                        col.Item().AlignRight().Text($"Pago ({data.PaymentMethod}): {data.Cash:N2}");
                        col.Item().AlignRight().Text($"Cambio: {data.Change:N2}");

                        col.Item().PaddingTop(10).AlignCenter().Text("¡Gracias por su compra!").Bold();
                    });
                });
            });

            // Save to Temp
            string fileName = $"Ticket_{data.SaleId}_{DateTime.Now.Ticks}.pdf";
            StorageFolder folder = ApplicationData.Current.TemporaryFolder;
            StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            // Write
            using (var stream = await file.OpenStreamForWriteAsync())
            {
                document.GeneratePdf(stream);
            }

            // Open
            await Launcher.LaunchFileAsync(file);
        }
    }
}
