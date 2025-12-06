using System;

namespace ark_app1
{
    public class Producto
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public int? CategoriaId { get; set; }
        public string? Talla { get; set; }
        public string? Color { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public decimal Stock { get; set; }
        public string UnidadMedida { get; set; } = "Unidad";
        public decimal StockMinimo { get; set; }
        public DateTime FechaRegistro { get; set; }
        public string? CategoriaNombre { get; set; }

        // Propiedades para la ventana de compras
        public decimal Cantidad { get; set; }
        public decimal Subtotal => Cantidad * PrecioCompra;

        public void CopyFrom(Producto other)
        {
            this.Id = other.Id;
            this.Codigo = other.Codigo;
            this.Nombre = other.Nombre;
            this.CategoriaId = other.CategoriaId;
            this.Talla = other.Talla;
            this.Color = other.Color;
            this.PrecioCompra = other.PrecioCompra;
            this.PrecioVenta = other.PrecioVenta;
            this.Stock = other.Stock;
            this.UnidadMedida = other.UnidadMedida;
            this.StockMinimo = other.StockMinimo;
            this.FechaRegistro = other.FechaRegistro;
            this.CategoriaNombre = other.CategoriaNombre;
        }
    }
}
