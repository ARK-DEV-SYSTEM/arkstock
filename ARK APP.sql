CREATE DATABASE [arkdbsisventas]
GO
USE [arkdbsisventas]
GO

CREATE TABLE Usuarios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    NombreCompleto NVARCHAR(100) NOT NULL,
    CI NVARCHAR(20) UNIQUE NULL,
    Email NVARCHAR(100) UNIQUE NULL,
    Telefono NVARCHAR(20) NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    FechaRegistro DATETIME2 DEFAULT GETDATE()
)
GO

CREATE TABLE Clientes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,
    Telefono NVARCHAR(20) NULL,
    CI NVARCHAR(20) NULL,
    Direccion NVARCHAR(200) NULL,
    Notas NVARCHAR(300) NULL,
    FechaRegistro DATETIME2 DEFAULT GETDATE()
)
GO

CREATE TABLE Categorias (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(50) NOT NULL UNIQUE
)
GO

CREATE TABLE Proveedores (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL,
    RUC NVARCHAR(20) NULL,
    Telefono NVARCHAR(20) NULL,
    Direccion NVARCHAR(200) NULL,
    Email NVARCHAR(100) NULL,
    Contacto NVARCHAR(100) NULL,
    Notas NVARCHAR(300) NULL,
    FechaRegistro DATETIME2 DEFAULT GETDATE()
)
GO

CREATE TABLE Productos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Codigo NVARCHAR(50) NOT NULL UNIQUE,
    Nombre NVARCHAR(150) NOT NULL,
    CategoriaId INT NULL,
    Talla NVARCHAR(20) NULL,
    Color NVARCHAR(30) NULL,
    PrecioCompra DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    PrecioVenta DECIMAL(10,2) NOT NULL,
    Stock DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    UnidadMedida NVARCHAR(20) DEFAULT 'Unidad',
    StockMinimo DECIMAL(10,2) NOT NULL DEFAULT 5.00,
    FechaRegistro DATETIME2 DEFAULT GETDATE(),
    Activo BIT DEFAULT 1,
    CONSTRAINT FK_Productos_Categoria FOREIGN KEY (CategoriaId)
        REFERENCES Categorias(Id) ON DELETE SET NULL
)
GO

CREATE TABLE Ventas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Fecha DATETIME2 DEFAULT GETDATE(),
    UsuarioId INT NOT NULL,
    ClienteId INT NULL,
    Total DECIMAL(10,2) NOT NULL,
    DescuentoPorcentaje DECIMAL(5,2) NULL DEFAULT 0.00,
    DescuentoMonto DECIMAL(10,2) NULL DEFAULT 0.00,
    EfectivoRecibido DECIMAL(10,2) NULL,
    Cambio DECIMAL(10,2) NULL,
    Estado NVARCHAR(20) DEFAULT 'Completada',
    TipoPago NVARCHAR(20) DEFAULT 'Efectivo',
    CONSTRAINT FK_Ventas_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id),
    CONSTRAINT FK_Ventas_Cliente FOREIGN KEY (ClienteId) REFERENCES Clientes(Id)
)
GO

CREATE TABLE VentasDetalle (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    VentaId INT NOT NULL,
    ProductoId INT NOT NULL,
    Cantidad DECIMAL(10,2) NOT NULL,
    PrecioUnitario DECIMAL(10,2) NOT NULL,
    DescuentoPorcentaje DECIMAL(5,2) NULL DEFAULT 0.00,
    DescuentoMonto DECIMAL(10,2) NULL DEFAULT 0.00,
    Subtotal DECIMAL(10,2) NOT NULL,
    CONSTRAINT FK_VentasDetalle_Venta FOREIGN KEY (VentaId)
        REFERENCES Ventas(Id) ON DELETE CASCADE,
    CONSTRAINT FK_VentasDetalle_Producto FOREIGN KEY (ProductoId)
        REFERENCES Productos(Id)
)
GO

CREATE TABLE Compras (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Fecha DATETIME2 DEFAULT GETDATE(),
    ProveedorId INT NULL,
    Total DECIMAL(10,2) NOT NULL,
    UsuarioId INT NOT NULL,
    Estado NVARCHAR(20) DEFAULT 'Completada',
    CONSTRAINT FK_Compras_Proveedor FOREIGN KEY (ProveedorId)
        REFERENCES Proveedores(Id) ON DELETE SET NULL,
    CONSTRAINT FK_Compras_Usuario FOREIGN KEY (UsuarioId) REFERENCES Usuarios(Id)
)
GO

CREATE TABLE ComprasDetalle (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    CompraId INT NOT NULL,
    ProductoId INT NOT NULL,
    Cantidad DECIMAL(10,2) NOT NULL,
    PrecioUnitario DECIMAL(10,2) NOT NULL,
    Subtotal DECIMAL(10,2) NOT NULL,
    CONSTRAINT FK_ComprasDetalle_Compra FOREIGN KEY (CompraId)
        REFERENCES Compras(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ComprasDetalle_Producto FOREIGN KEY (ProductoId)
        REFERENCES Productos(Id)
)
GO

CREATE TABLE InventarioAjustes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Fecha DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    UsuarioId INT NOT NULL,
    ProductoId INT NOT NULL,
    Cantidad DECIMAL(10,2) NOT NULL,
    Motivo NVARCHAR(300) NOT NULL,

    CONSTRAINT CK_Ajustes_Cantidad_NoCero CHECK (Cantidad <> 0),

    CONSTRAINT FK_Ajustes_Usuario FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios(Id),

    CONSTRAINT FK_Ajustes_Producto FOREIGN KEY (ProductoId)
        REFERENCES Productos(Id)
);
GO

CREATE TABLE Empresa (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(200) NOT NULL,
    Telefono NVARCHAR(50) NULL,
    TelefonoSecundario NVARCHAR(50) NULL,
    Direccion NVARCHAR(300) NULL,
    Email NVARCHAR(150) NULL,
    SitioWeb NVARCHAR(150) NULL,
    FechaRegistro DATETIME2 DEFAULT SYSDATETIME()
);
GO

INSERT INTO Categorias (Nombre) VALUES
('Bebidas'),('Gaseosas'),('Jugos y Néctares'),('Aguas Minerales'),('Energizantes'),
('Cervezas'),('Licores'),('Vinos'),('Dulces y Chocolates'),('Galletas'),
('Snacks'),('Papas Fritas'),('Chifles y Plátanos'),('Chicles y Caramelos'),('Helados'),
('Lácteos'),('Leche'),('Yogures'),('Quesos'),('Mantequilla y Margarina'),
('Huevos'),('Panadería'),('Panes'),('Tortillas'),('Pasteles y Tortas'),
('Abarrotes'),('Arroz'),('Fideos y Pastas'),('Aceites'),('Azúcar y Endulzantes'),
('Sal'),('Café'),('Té e Infusiones'),('Conservas'),('Enlatados'),
('Sopas Instantáneas'),('Salsas y Condimentos'),('Especias'),('Harinas'),('Cereales'),
('Limpieza'),('Detergentes'),('Jabones'),('Suavizantes'),('Desinfectantes'),
('Papel Higiénico'),('Toallas de Cocina'),('Servilletas'),('Higiene Personal'),
('Shampoo'),('Jabón de Baño'),('Pasta Dental'),('Desodorantes'),('Toallas Higiénicas'),
('Pañales'),('Medicinas Básicas'),('Analgésicos'),('Vitaminas'),
('Cuidado del Cabello'),('Cuidado de la Piel'),('Mascotas'),('Alimento para Perros'),
('Alimento para Gatos'),('Accesorios Mascotas'),('Ferretería Básica'),('Pilas y Baterías'),
('Bombillos'),('Cintas Adhesivas'),('Útiles Escolares'),('Cuadernos'),('Lapiceros'),
('Borradores'),('Juguetes'),('Ropa Interior'),('Medias'),('Gorras'),
('Celulares y Accesorios'),('Cargadores'),('Audífonos'),('Fundas de Celular'),
('Recargas'),('Otros'),('Sin Categoría')
GO

CREATE INDEX IX_Productos_Codigo ON Productos(Codigo ASC)
CREATE INDEX IX_Productos_Nombre ON Productos(Nombre ASC)
CREATE INDEX IX_Productos_Categoria ON Productos(CategoriaId)
CREATE INDEX IX_Productos_Talla ON Productos(Talla) WHERE Talla IS NOT NULL
CREATE INDEX IX_Productos_Color ON Productos(Color) WHERE Color IS NOT NULL

CREATE INDEX IX_Ventas_Fecha ON Ventas(Fecha DESC)
CREATE INDEX IX_Ventas_Usuario ON Ventas(UsuarioId)
CREATE INDEX IX_Ventas_Cliente ON Ventas(ClienteId)
CREATE INDEX IX_VentasDetalle_Venta ON VentasDetalle(VentaId)
CREATE INDEX IX_VentasDetalle_Producto ON VentasDetalle(ProductoId)
CREATE INDEX IX_Compras_Fecha ON Compras(Fecha DESC)
CREATE INDEX IX_Compras_Proveedor ON Compras(ProveedorId)
CREATE INDEX IX_Clientes_Nombre ON Clientes(Nombre)
CREATE INDEX IX_Clientes_Telefono ON Clientes(Telefono)
GO

CREATE INDEX IX_Ajustes_Producto ON InventarioAjustes(ProductoId);
CREATE INDEX IX_Ajustes_Usuario ON InventarioAjustes(UsuarioId);
GO
