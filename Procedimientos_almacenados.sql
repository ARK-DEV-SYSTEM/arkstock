USE [arkdbsisventas]
GO

CREATE OR ALTER PROCEDURE sp_ModificarCompra
    @CompraId INT,
    @ProveedorId INT,
    @UsuarioId INT,
    @Productos NVARCHAR(MAX),
    @Resultado BIT OUTPUT,
    @Mensaje NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;
    SET @Mensaje = '';

    BEGIN TRY
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM Compras WHERE Id = @CompraId)
        BEGIN
            THROW 51000, 'La compra no existe', 1;
        END
        DECLARE @ProductosAnteriores TABLE (ProductoId INT);
        INSERT INTO @ProductosAnteriores (ProductoId)
        SELECT ProductoId
        FROM ComprasDetalle
        WHERE CompraId = @CompraId;
        UPDATE p
        SET p.Stock = p.Stock - cd.Cantidad
        FROM Productos p
        INNER JOIN ComprasDetalle cd ON p.Id = cd.ProductoId
        WHERE cd.CompraId = @CompraId;
        DELETE FROM ComprasDetalle WHERE CompraId = @CompraId;
        DECLARE @TempProductos TABLE (
            Codigo NVARCHAR(50),
            Nombre NVARCHAR(150),
            CategoriaId INT,
            Talla NVARCHAR(20),
            Color NVARCHAR(30),
            PrecioCompra DECIMAL(10,2),
            PrecioVenta DECIMAL(10,2),
            Cantidad DECIMAL(10,2),
            UnidadMedida NVARCHAR(20),
            StockMinimo DECIMAL(10,2),
            ProductoId INT NULL,
            ProductoExiste BIT DEFAULT 0
        );
        INSERT INTO @TempProductos (Codigo, Nombre, CategoriaId, Talla, Color, PrecioCompra, PrecioVenta, Cantidad, UnidadMedida, StockMinimo)
        SELECT Codigo, Nombre, CategoriaId, Talla, Color, PrecioCompra, PrecioVenta, Cantidad,
               ISNULL(UnidadMedida, 'Unidad'), ISNULL(StockMinimo, 5)
        FROM OPENJSON(@Productos)
        WITH (
            Codigo NVARCHAR(50),
            Nombre NVARCHAR(150),
            CategoriaId INT,
            Talla NVARCHAR(20),
            Color NVARCHAR(30),
            PrecioCompra DECIMAL(10,2),
            PrecioVenta DECIMAL(10,2),
            Cantidad DECIMAL(10,2),
            UnidadMedida NVARCHAR(20),
            StockMinimo DECIMAL(10,2)
        );
        UPDATE t
        SET t.ProductoExiste = 1,
            t.ProductoId = p.Id
        FROM @TempProductos t
        INNER JOIN Productos p ON t.Codigo = p.Codigo;
        INSERT INTO Productos (Codigo, Nombre, CategoriaId, Talla, Color, PrecioCompra, PrecioVenta, Stock, UnidadMedida, StockMinimo, Activo)
        SELECT Codigo, Nombre, CategoriaId, Talla, Color, PrecioCompra, PrecioVenta, 0, UnidadMedida, StockMinimo, 1
        FROM @TempProductos
        WHERE ProductoExiste = 0;
        UPDATE t
        SET t.ProductoId = p.Id
        FROM @TempProductos t
        INNER JOIN Productos p ON t.Codigo = p.Codigo
        WHERE t.ProductoId IS NULL;
        UPDATE p
        SET p.Activo = 0
        FROM Productos p
        INNER JOIN @ProductosAnteriores pa ON p.Id = pa.ProductoId
        WHERE pa.ProductoId NOT IN (SELECT ProductoId FROM @TempProductos);
        DECLARE @Total DECIMAL(10,2) = (SELECT SUM(Cantidad * PrecioCompra) FROM @TempProductos);
        UPDATE Compras
        SET ProveedorId = @ProveedorId,
            Total = @Total,
            UsuarioId = @UsuarioId,
            Fecha = GETDATE()
        WHERE Id = @CompraId;
        INSERT INTO ComprasDetalle (CompraId, ProductoId, Cantidad, PrecioUnitario, Subtotal)
        SELECT @CompraId, ProductoId, Cantidad, PrecioCompra, (Cantidad * PrecioCompra)
        FROM @TempProductos;
        UPDATE p
        SET p.Stock = p.Stock + t.Cantidad
        FROM Productos p
        INNER JOIN @TempProductos t ON p.Id = t.ProductoId;
        COMMIT TRANSACTION;
        SET @Resultado = 1;
        SET @Mensaje = 'Compra modificada correctamente.';

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;

        SET @Resultado = 0;
        SET @Mensaje = 'Error al modificar: ' + ERROR_MESSAGE();
    END CATCH
END
GO

-- 2. SP para Registrar Ajuste
CREATE OR ALTER PROCEDURE sp_RegistrarAjuste
    @UsuarioId INT,
    @ProductoId INT,
    @Cantidad DECIMAL(10,2),
    @Motivo NVARCHAR(300),
    @Resultado BIT OUTPUT,
    @Mensaje NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM Productos WHERE Id = @ProductoId) THROW 51000, 'Producto no encontrado', 1;
            IF @Cantidad = 0 THROW 51000, 'La cantidad no puede ser 0', 1;
            INSERT INTO InventarioAjustes (UsuarioId, ProductoId, Cantidad, Motivo)
            VALUES (@UsuarioId, @ProductoId, @Cantidad, @Motivo);
        COMMIT TRANSACTION;
        SET @Resultado = 1;
        SET @Mensaje = 'Ajuste registrado correctamente.';
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        SET @Resultado = 0;
        SET @Mensaje = ERROR_MESSAGE();
    END CATCH
END
GO

-- 3. SP Gestionar Cliente
CREATE OR ALTER PROCEDURE sp_GestionarCliente
    @Id INT = 0,
    @Nombre NVARCHAR(100),
    @Telefono NVARCHAR(20) = NULL,
    @CI NVARCHAR(20) = NULL,
    @Direccion NVARCHAR(200) = NULL,
    @Notas NVARCHAR(300) = NULL,
    @Accion NVARCHAR(10) = 'GUARDAR',
    @Resultado BIT OUTPUT,
    @Mensaje NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        IF @Accion = 'ELIMINAR'
        BEGIN
            DELETE FROM Clientes WHERE Id = @Id;
            SET @Mensaje = 'Cliente eliminado.';
        END
        ELSE
        BEGIN
            IF @Id = 0
            BEGIN
                INSERT INTO Clientes (Nombre, Telefono, CI, Direccion, Notas)
                VALUES (@Nombre, @Telefono, @CI, @Direccion, @Notas);
                SET @Mensaje = 'Cliente registrado.';
            END
            ELSE
            BEGIN
                UPDATE Clientes
                SET Nombre = @Nombre, Telefono = @Telefono, CI = @CI, Direccion = @Direccion, Notas = @Notas
                WHERE Id = @Id;
                SET @Mensaje = 'Cliente actualizado.';
            END
        END
        SET @Resultado = 1;
    END TRY
    BEGIN CATCH
        SET @Resultado = 0;
        SET @Mensaje = ERROR_MESSAGE();
    END CATCH
END
GO

-- 4. SP Gestionar Categoria
CREATE OR ALTER PROCEDURE sp_GestionarCategoria
    @Id INT = 0,
    @Nombre NVARCHAR(50),
    @Accion NVARCHAR(10) = 'GUARDAR',
    @Resultado BIT OUTPUT,
    @Mensaje NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        IF @Accion = 'ELIMINAR'
        BEGIN
            DELETE FROM Categorias WHERE Id = @Id;
            SET @Mensaje = 'Categoría eliminada.';
        END
        ELSE
        BEGIN
            IF EXISTS (SELECT 1 FROM Categorias WHERE Nombre = @Nombre AND Id <> @Id)
                THROW 51000, 'Ya existe una categoría con ese nombre', 1;

            IF @Id = 0
            BEGIN
                INSERT INTO Categorias (Nombre) VALUES (@Nombre);
                SET @Mensaje = 'Categoría creada.';
            END
            ELSE
            BEGIN
                UPDATE Categorias SET Nombre = @Nombre WHERE Id = @Id;
                SET @Mensaje = 'Categoría actualizada.';
            END
        END
        SET @Resultado = 1;
    END TRY
    BEGIN CATCH
        SET @Resultado = 0;
        SET @Mensaje = ERROR_MESSAGE();
    END CATCH
END
GO

-- 5 registrar compra:
CREATE OR ALTER PROCEDURE sp_RegistrarVenta_v2
    @UsuarioId INT,
    @ClienteId INT = NULL,
    @Productos NVARCHAR(MAX),
    @EfectivoRecibido DECIMAL(10,2) = NULL,
    @TipoPago NVARCHAR(20) = 'Efectivo',
    @DescuentoGlobalPorcentaje DECIMAL(5,2) = 0,
    @DescuentoGlobalMonto DECIMAL(10,2) = 0,
    @Resultado BIT OUTPUT,
    @Mensaje NVARCHAR(500) OUTPUT,
    @VentaId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;
    SET @Mensaje = '';
    SET @VentaId = NULL;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @UsuarioId)
        BEGIN
            THROW 51000, 'El usuario no existe', 1;
        END

        IF @ClienteId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Clientes WHERE Id = @ClienteId)
        BEGIN
            THROW 51000, 'El cliente no existe', 1;
        END
        DECLARE @TempVenta TABLE (
            ProductoId INT,
            Cantidad DECIMAL(10,2),
            PrecioUnitario DECIMAL(10,2),
            DescuentoPorcentaje DECIMAL(5,2),
            DescuentoMonto DECIMAL(10,2),
            Subtotal DECIMAL(10,2),
            StockActual DECIMAL(10,2)
        );

        INSERT INTO @TempVenta (ProductoId, Cantidad, PrecioUnitario, DescuentoPorcentaje, DescuentoMonto)
        SELECT
            ProductoId,
            Cantidad,
            PrecioUnitario,
            ISNULL(DescuentoPorcentaje, 0),
            ISNULL(DescuentoMonto, 0)
        FROM OPENJSON(@Productos)
        WITH (
            ProductoId INT '$.ProductoId',
            Cantidad DECIMAL(10,2) '$.Cantidad',
            PrecioUnitario DECIMAL(10,2) '$.PrecioUnitario',
            DescuentoPorcentaje DECIMAL(5,2) '$.DescuentoPorcentaje',
            DescuentoMonto DECIMAL(10,2) '$.DescuentoMonto'
        );

        UPDATE t
        SET t.StockActual = p.Stock
        FROM @TempVenta t
        INNER JOIN Productos p ON t.ProductoId = p.Id;

        IF EXISTS (SELECT 1 FROM @TempVenta WHERE StockActual IS NULL)
        BEGIN
            THROW 51000, 'Uno o más productos no existen', 1;
        END

        IF EXISTS (SELECT 1 FROM @TempVenta WHERE Cantidad > StockActual)
        BEGIN
            DECLARE @ProdSinStock NVARCHAR(200);
            SELECT TOP 1 @ProdSinStock = p.Nombre
            FROM @TempVenta t
            JOIN Productos p ON t.ProductoId = p.Id
            WHERE t.Cantidad > t.StockActual;

            DECLARE @MsgStock NVARCHAR(250) = 'Stock insuficiente para: ' + @ProdSinStock;
            THROW 51000, @MsgStock, 1;
        END

        UPDATE @TempVenta
        SET Subtotal = (Cantidad * PrecioUnitario) - DescuentoMonto - ((Cantidad * PrecioUnitario) * (DescuentoPorcentaje / 100.0));

        DECLARE @TotalVenta DECIMAL(10,2);
        SELECT @TotalVenta = SUM(Subtotal) FROM @TempVenta;

        IF @DescuentoGlobalMonto > 0
        BEGIN
            SET @TotalVenta = @TotalVenta - @DescuentoGlobalMonto;
        END

        IF @DescuentoGlobalPorcentaje > 0
        BEGIN
            SET @TotalVenta = @TotalVenta - (@TotalVenta * @DescuentoGlobalPorcentaje / 100.0);
        END

        IF @TotalVenta < 0 SET @TotalVenta = 0;

        INSERT INTO Ventas (Fecha, UsuarioId, ClienteId, Total, DescuentoPorcentaje, DescuentoMonto, EfectivoRecibido, Cambio, Estado, TipoPago)
        VALUES (GETDATE(), @UsuarioId, @ClienteId, @TotalVenta, @DescuentoGlobalPorcentaje, @DescuentoGlobalMonto, @EfectivoRecibido,
                CASE WHEN @EfectivoRecibido IS NOT NULL THEN @EfectivoRecibido - @TotalVenta ELSE 0 END,
                'Completada', @TipoPago);

        SET @VentaId = SCOPE_IDENTITY();

        INSERT INTO VentasDetalle (VentaId, ProductoId, Cantidad, PrecioUnitario, DescuentoPorcentaje, DescuentoMonto, Subtotal)
        SELECT @VentaId, ProductoId, Cantidad, PrecioUnitario, DescuentoPorcentaje, DescuentoMonto, Subtotal
        FROM @TempVenta;

        UPDATE p
        SET p.Stock = p.Stock - t.Cantidad
        FROM Productos p
        INNER JOIN @TempVenta t ON p.Id = t.ProductoId;

        COMMIT TRANSACTION;
        SET @Resultado = 1;
        SET @Mensaje = 'Venta registrada exitosamente. ID: ' + CAST(@VentaId AS NVARCHAR(20));

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @Resultado = 0;
        SET @Mensaje = 'Error al registrar venta: ' + ERROR_MESSAGE();
        SET @VentaId = NULL;
    END CATCH
END
GO

-- 6 gestionar producto
CREATE OR ALTER PROCEDURE sp_GestionarProducto
    @Id INT = 0,
    @Codigo NVARCHAR(50),
    @Nombre NVARCHAR(150),
    @CategoriaId INT = NULL,
    @Talla NVARCHAR(20) = NULL,
    @Color NVARCHAR(30) = NULL,
    @PrecioCompra DECIMAL(10,2) = 0,
    @PrecioVenta DECIMAL(10,2),
    @UnidadMedida NVARCHAR(20) = 'Unidad',
    @StockMinimo DECIMAL(10,2) = 5,
    @Resultado BIT OUTPUT,
    @Mensaje NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;
    SET @Mensaje = '';

    BEGIN TRY
        IF @Id = 0
        BEGIN
             IF EXISTS (SELECT 1 FROM Productos WHERE Codigo = @Codigo)
             BEGIN
                SET @Mensaje = 'El código ya existe';
                RETURN;
             END

             INSERT INTO Productos (Codigo, Nombre, CategoriaId, Talla, Color, PrecioCompra, PrecioVenta, Stock, UnidadMedida, StockMinimo, Activo, FechaRegistro)
             VALUES (@Codigo, @Nombre, @CategoriaId, @Talla, @Color, @PrecioCompra, @PrecioVenta, 0, @UnidadMedida, @StockMinimo, 1, GETDATE());

             SET @Mensaje = 'Producto creado correctamente';
             SET @Resultado = 1;
        END
        ELSE
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM Productos WHERE Id = @Id)
            BEGIN
                SET @Mensaje = 'El producto no existe';
                RETURN;
            END

            IF EXISTS (SELECT 1 FROM Productos WHERE Codigo = @Codigo AND Id <> @Id)
            BEGIN
                SET @Mensaje = 'El código ya está en uso por otro producto';
                RETURN;
            END

            UPDATE Productos
            SET
                Codigo = @Codigo,
                Nombre = @Nombre,
                CategoriaId = @CategoriaId,
                Talla = @Talla,
                Color = @Color,
                PrecioCompra = @PrecioCompra,
                PrecioVenta = @PrecioVenta,
                UnidadMedida = @UnidadMedida,
                StockMinimo = @StockMinimo
            WHERE Id = @Id;

            SET @Mensaje = 'Producto actualizado correctamente';
            SET @Resultado = 1;
        END
    END TRY
    BEGIN CATCH
        SET @Resultado = 0;
        SET @Mensaje = ERROR_MESSAGE();
    END CATCH
END
GO

-- 7 editar compra:

CREATE OR ALTER PROCEDURE sp_EditarCompra
    @CompraId INT,
    @ProveedorId INT = NULL,
    @Productos NVARCHAR(MAX),
    @Resultado BIT OUTPUT,
    @Mensaje NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;
    SET @Mensaje = '';
    
    BEGIN TRY
        BEGIN TRANSACTION;
        
        IF NOT EXISTS (SELECT 1 FROM Compras WHERE Id = @CompraId)
        BEGIN
            SET @Mensaje = 'La compra no existe';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        IF @ProveedorId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Proveedores WHERE Id = @ProveedorId)
        BEGIN
            SET @Mensaje = 'El proveedor no existe';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        IF @Productos IS NULL OR @Productos = ''
        BEGIN
            SET @Mensaje = 'Debe enviar productos';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        DECLARE @TempProductos TABLE (
            ProductoId INT,
            Nombre NVARCHAR(150),
            CategoriaId INT,
            Talla NVARCHAR(20),
            Color NVARCHAR(30),
            PrecioCompra DECIMAL(10,2),
            PrecioVenta DECIMAL(10,2),
            Cantidad DECIMAL(10,2),
            UnidadMedida NVARCHAR(20),
            StockMinimo DECIMAL(10,2),
            CantidadAnterior DECIMAL(10,2) NULL
        );
        
        INSERT INTO @TempProductos (ProductoId, Nombre, CategoriaId, Talla, Color, PrecioCompra, PrecioVenta, Cantidad, UnidadMedida, StockMinimo)
        SELECT 
            ProductoId,
            Nombre,
            CategoriaId,
            Talla,
            Color,
            ISNULL(PrecioCompra, 0.00),
            PrecioVenta,
            Cantidad,
            ISNULL(UnidadMedida, 'Unidad'),
            ISNULL(StockMinimo, 5.00)
        FROM OPENJSON(@Productos)
        WITH (
            ProductoId INT '$.ProductoId',
            Nombre NVARCHAR(150) '$.Nombre',
            CategoriaId INT '$.CategoriaId',
            Talla NVARCHAR(20) '$.Talla',
            Color NVARCHAR(30) '$.Color',
            PrecioCompra DECIMAL(10,2) '$.PrecioCompra',
            PrecioVenta DECIMAL(10,2) '$.PrecioVenta',
            Cantidad DECIMAL(10,2) '$.Cantidad',
            UnidadMedida NVARCHAR(20) '$.UnidadMedida',
            StockMinimo DECIMAL(10,2) '$.StockMinimo'
        );
        
        IF NOT EXISTS (SELECT 1 FROM @TempProductos)
        BEGIN
            SET @Mensaje = 'No se procesaron productos del JSON';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        IF EXISTS (SELECT 1 FROM @TempProductos WHERE ProductoId IS NULL OR ProductoId <= 0)
        BEGIN
            SET @Mensaje = 'El ProductoId es obligatorio';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        IF EXISTS (SELECT 1 FROM @TempProductos WHERE Nombre IS NULL OR Nombre = '')
        BEGIN
            SET @Mensaje = 'El Nombre es obligatorio';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        IF EXISTS (SELECT 1 FROM @TempProductos WHERE PrecioVenta IS NULL OR PrecioVenta <= 0)
        BEGIN
            SET @Mensaje = 'El PrecioVenta debe ser mayor a 0';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        IF EXISTS (SELECT 1 FROM @TempProductos WHERE Cantidad IS NULL OR Cantidad <= 0)
        BEGIN
            SET @Mensaje = 'La Cantidad debe ser mayor a 0';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        IF EXISTS (
            SELECT 1 FROM @TempProductos t
            WHERE NOT EXISTS (SELECT 1 FROM Productos WHERE Id = t.ProductoId)
        )
        BEGIN
            SET @Mensaje = 'Uno o mas productos no existen';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        IF EXISTS (
            SELECT 1 FROM @TempProductos t
            WHERE t.CategoriaId IS NOT NULL 
            AND NOT EXISTS (SELECT 1 FROM Categorias WHERE Id = t.CategoriaId)
        )
        BEGIN
            SET @Mensaje = 'Una categoria no existe';
            ROLLBACK TRANSACTION;
            RETURN;
        END
        
        UPDATE t
        SET t.CantidadAnterior = cd.Cantidad
        FROM @TempProductos t
        INNER JOIN ComprasDetalle cd ON cd.ProductoId = t.ProductoId AND cd.CompraId = @CompraId;
        
        UPDATE p
        SET p.Stock = p.Stock - t.CantidadAnterior
        FROM Productos p
        INNER JOIN @TempProductos t ON p.Id = t.ProductoId
        WHERE t.CantidadAnterior IS NOT NULL;
        
        UPDATE p
        SET 
            p.Nombre = t.Nombre,
            p.CategoriaId = t.CategoriaId,
            p.Talla = t.Talla,
            p.Color = t.Color,
            p.PrecioCompra = t.PrecioCompra,
            p.PrecioVenta = t.PrecioVenta,
            p.UnidadMedida = t.UnidadMedida,
            p.StockMinimo = t.StockMinimo
        FROM Productos p
        INNER JOIN @TempProductos t ON p.Id = t.ProductoId;
        
        UPDATE p
        SET p.Stock = p.Stock + t.Cantidad
        FROM Productos p
        INNER JOIN @TempProductos t ON p.Id = t.ProductoId;
        
        UPDATE c
        SET c.ProveedorId = @ProveedorId
        FROM Compras c
        WHERE c.Id = @CompraId;
        
        DECLARE @NuevoTotal DECIMAL(10,2);
        SELECT @NuevoTotal = SUM(Cantidad * PrecioCompra)
        FROM @TempProductos;
        
        UPDATE c
        SET c.Total = @NuevoTotal
        FROM Compras c
        WHERE c.Id = @CompraId;
        
        UPDATE cd
        SET 
            cd.Cantidad = t.Cantidad,
            cd.PrecioUnitario = t.PrecioCompra,
            cd.Subtotal = t.Cantidad * t.PrecioCompra
        FROM ComprasDetalle cd
        INNER JOIN @TempProductos t ON cd.ProductoId = t.ProductoId
        WHERE cd.CompraId = @CompraId;
        
        COMMIT TRANSACTION;
        
        SET @Resultado = 1;
        SET @Mensaje = 'Compra editada correctamente ID: ' + CAST(@CompraId AS NVARCHAR(10));
        
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        
        SET @Resultado = 0;
        SET @Mensaje = 'Error: ' + ERROR_MESSAGE();
    END CATCH
END
GO

-- 8 actualizar ajuste:

CREATE TRIGGER TR_ActualizarStock_Ajuste
ON InventarioAjustes
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM Productos p
        INNER JOIN inserted i ON p.Id = i.ProductoId
        WHERE p.Stock + i.Cantidad < 0
    )
    BEGIN
        RAISERROR ('El ajuste generaría stock negativo. Operación cancelada.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;
    UPDATE p
    SET p.Stock = p.Stock + i.Cantidad
    FROM Productos p
    INNER JOIN inserted i ON p.Id = i.ProductoId;

END;
GO

-- 9 registarr compra:
CREATE OR ALTER PROCEDURE sp_RegistrarCompra
    @UsuarioId INT,
    @ProveedorId INT = NULL,
    @Productos NVARCHAR(MAX),
    @Resultado BIT OUTPUT,
    @Mensaje NVARCHAR(500) OUTPUT,
    @CompraId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Resultado = 0;
    SET @Mensaje = '';
    SET @CompraId = NULL;  
    BEGIN TRY
        BEGIN TRANSACTION;    
        IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @UsuarioId)
        BEGIN
            SET @Mensaje = 'El usuario no existe';
            ROLLBACK TRANSACTION;
            RETURN;
        END      
        IF @ProveedorId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Proveedores WHERE Id = @ProveedorId)
        BEGIN
            SET @Mensaje = 'El proveedor no existe';
            ROLLBACK TRANSACTION;
            RETURN;
        END     
        IF @Productos IS NULL OR @Productos = ''
        BEGIN
            SET @Mensaje = 'Debe enviar productos';
            ROLLBACK TRANSACTION;
            RETURN;
        END       
        DECLARE @TempProductos TABLE (
            Codigo NVARCHAR(50),
            Nombre NVARCHAR(150),
            CategoriaId INT,
            Talla NVARCHAR(20),
            Color NVARCHAR(30),
            PrecioCompra DECIMAL(10,2),
            PrecioVenta DECIMAL(10,2),
            Cantidad DECIMAL(10,2),
            UnidadMedida NVARCHAR(20),
            StockMinimo DECIMAL(10,2),
            ProductoExiste BIT DEFAULT 0,
            ProductoId INT NULL
        );    
        INSERT INTO @TempProductos (Codigo, Nombre, CategoriaId, Talla, Color, PrecioCompra, PrecioVenta, Cantidad, UnidadMedida, StockMinimo)
        SELECT 
            Codigo,
            Nombre,
            CategoriaId,
            Talla,
            Color,
            ISNULL(PrecioCompra, 0.00),
            PrecioVenta,
            Cantidad,
            ISNULL(UnidadMedida, 'Unidad'),
            ISNULL(StockMinimo, 5.00)
        FROM OPENJSON(@Productos)
        WITH (
            Codigo NVARCHAR(50) '$.Codigo',
            Nombre NVARCHAR(150) '$.Nombre',
            CategoriaId INT '$.CategoriaId',
            Talla NVARCHAR(20) '$.Talla',
            Color NVARCHAR(30) '$.Color',
            PrecioCompra DECIMAL(10,2) '$.PrecioCompra',
            PrecioVenta DECIMAL(10,2) '$.PrecioVenta',
            Cantidad DECIMAL(10,2) '$.Cantidad',
            UnidadMedida NVARCHAR(20) '$.UnidadMedida',
            StockMinimo DECIMAL(10,2) '$.StockMinimo'
        );      
        IF NOT EXISTS (SELECT 1 FROM @TempProductos)
        BEGIN
            SET @Mensaje = 'No se procesaron productos del JSON';
            ROLLBACK TRANSACTION;
            RETURN;
        END      
        IF EXISTS (SELECT 1 FROM @TempProductos WHERE Codigo IS NULL OR Codigo = '')
        BEGIN
            SET @Mensaje = 'El Codigo es obligatorio';
            ROLLBACK TRANSACTION;
            RETURN;
        END     
        IF EXISTS (SELECT 1 FROM @TempProductos WHERE Nombre IS NULL OR Nombre = '')
        BEGIN
            SET @Mensaje = 'El Nombre es obligatorio';
            ROLLBACK TRANSACTION;
            RETURN;
        END      
        IF EXISTS (SELECT 1 FROM @TempProductos WHERE PrecioVenta IS NULL OR PrecioVenta <= 0)
        BEGIN
            SET @Mensaje = 'El PrecioVenta debe ser mayor a 0';
            ROLLBACK TRANSACTION;
            RETURN;
        END        
        IF EXISTS (SELECT 1 FROM @TempProductos WHERE Cantidad IS NULL OR Cantidad <= 0)
        BEGIN
            SET @Mensaje = 'La Cantidad debe ser mayor a 0';
            ROLLBACK TRANSACTION;
            RETURN;
        END    
        IF EXISTS (
            SELECT 1 FROM @TempProductos t
            WHERE t.CategoriaId IS NOT NULL 
            AND NOT EXISTS (SELECT 1 FROM Categorias WHERE Id = t.CategoriaId)
        )
        BEGIN
            SET @Mensaje = 'Una categoria no existe';
            ROLLBACK TRANSACTION;
            RETURN;
        END  
        UPDATE t
        SET t.ProductoExiste = 1,
            t.ProductoId = p.Id
        FROM @TempProductos t
        INNER JOIN Productos p ON t.Codigo = p.Codigo; 
        IF EXISTS (SELECT 1 FROM @TempProductos WHERE ProductoExiste = 1)
        BEGIN
            DECLARE @CodigosExistentes NVARCHAR(500);
            SELECT @CodigosExistentes = STRING_AGG(Codigo, ', ')
            FROM @TempProductos
            WHERE ProductoExiste = 1;
            SET @Mensaje = 'Productos ya existen: ' + @CodigosExistentes;
            ROLLBACK TRANSACTION;
            RETURN;
        END
        INSERT INTO Productos (Codigo, Nombre, CategoriaId, Talla, Color, PrecioCompra, PrecioVenta, Stock, UnidadMedida, StockMinimo, Activo, FechaRegistro)
        SELECT 
            t.Codigo,
            t.Nombre,
            t.CategoriaId,
            t.Talla,
            t.Color,
            t.PrecioCompra,
            t.PrecioVenta,
            0.00,
            t.UnidadMedida,
            t.StockMinimo,
            1,
            GETDATE()
        FROM @TempProductos t;
        UPDATE t
        SET t.ProductoId = p.Id
        FROM @TempProductos t
        INNER JOIN Productos p ON t.Codigo = p.Codigo; 
        DECLARE @TotalCompra DECIMAL(10,2);
        SELECT @TotalCompra = SUM(Cantidad * PrecioCompra)
        FROM @TempProductos;
        INSERT INTO Compras (Fecha, ProveedorId, Total, UsuarioId, Estado)
        VALUES (GETDATE(), @ProveedorId, @TotalCompra, @UsuarioId, 'Completada');
        SET @CompraId = SCOPE_IDENTITY();
        INSERT INTO ComprasDetalle (CompraId, ProductoId, Cantidad, PrecioUnitario, Subtotal)
        SELECT 
            @CompraId,
            ProductoId,
            Cantidad,
            PrecioCompra,
            Cantidad * PrecioCompra
        FROM @TempProductos;
        UPDATE p
        SET p.Stock = p.Stock + t.Cantidad
        FROM Productos p
        INNER JOIN @TempProductos t ON p.Id = t.ProductoId;
        COMMIT TRANSACTION;
        SET @Resultado = 1;
        SET @Mensaje = 'Compra registrada ID: ' + CAST(@CompraId AS NVARCHAR(10));
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;      
        SET @Resultado = 0;
        SET @Mensaje = 'Error: ' + ERROR_MESSAGE();
        SET @CompraId = NULL;
    END CATCH
END
GO

--- 10 gestionar empresa
CREATE OR ALTER PROCEDURE sp_GestionarEmpresa
    @Nombre NVARCHAR(200),
    @Telefono NVARCHAR(50) = NULL,
    @TelefonoSecundario NVARCHAR(50) = NULL,
    @Direccion NVARCHAR(300) = NULL,
    @Email NVARCHAR(150) = NULL,
    @SitioWeb NVARCHAR(150) = NULL,
    @Resultado BIT OUTPUT,
    @Mensaje NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        IF EXISTS (SELECT 1 FROM Empresa)
        BEGIN
            UPDATE Empresa
            SET Nombre = @Nombre,
                Telefono = @Telefono,
                TelefonoSecundario = @TelefonoSecundario,
                Direccion = @Direccion,
                Email = @Email,
                SitioWeb = @SitioWeb;
            SET @Mensaje = 'Datos de empresa actualizados.';
        END
        ELSE
        BEGIN
            INSERT INTO Empresa (Nombre, Telefono, TelefonoSecundario, Direccion, Email, SitioWeb)
            VALUES (@Nombre, @Telefono, @TelefonoSecundario, @Direccion, @Email, @SitioWeb);
            SET @Mensaje = 'Empresa registrada.';
        END
        SET @Resultado = 1;
    END TRY
    BEGIN CATCH
        SET @Resultado = 0;
        SET @Mensaje = ERROR_MESSAGE();
    END CATCH
END
GO
--- obtner datso reporte
CREATE OR ALTER PROCEDURE sp_ObtenerDatosReporte
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 
        CAST(v.Fecha AS DATE) AS Fecha,
        SUM(v.Total) AS TotalVentas,
        SUM(v.Total * 0.30) AS EstimadoGanancia
    FROM Ventas v
    WHERE v.Fecha >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE))
    GROUP BY CAST(v.Fecha AS DATE)
    ORDER BY Fecha;
    SELECT SUM(Total) AS TotalVentasSemana 
    FROM Ventas 
    WHERE Fecha >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE));
    SELECT TOP 20 Codigo, Nombre, Stock, StockMinimo
    FROM Productos
    WHERE Stock <= StockMinimo AND Activo = 1
    ORDER BY Stock ASC;
    SELECT TOP 20 v.Id, v.Fecha, ISNULL(c.Nombre, 'Mostrador') AS Cliente, v.Total, v.Estado
    FROM Ventas v
    LEFT JOIN Clientes c ON v.ClienteId = c.Id
    ORDER BY v.Fecha DESC;
    SELECT vd.VentaId, p.Codigo, p.Nombre, vd.Cantidad, vd.PrecioUnitario, vd.Subtotal
    FROM VentasDetalle vd
    INNER JOIN Productos p ON vd.ProductoId = p.Id
    WHERE EXISTS (
        SELECT 1 FROM (
            SELECT TOP 20 Id FROM Ventas ORDER BY Fecha DESC
        ) top20 WHERE top20.Id = vd.VentaId
    )
    ORDER BY vd.VentaId DESC, p.Nombre;
    SELECT 
        (SELECT COUNT(*) FROM Productos WHERE Activo = 1)               AS TotalProductos,
        (SELECT ISNULL(SUM(Total), 0) FROM Ventas)                      AS TotalVentasHistorico,
        (SELECT COUNT(*) FROM Clientes)                                 AS TotalClientes,
        (SELECT ISNULL(SUM(Stock), 0) FROM Productos WHERE Activo = 1)  AS ValorStockActual;
    SELECT TOP 10 
        p.Nombre, 
        SUM(vd.Cantidad) AS CantidadVendida,
        SUM(vd.Subtotal) AS TotalVendido
    FROM VentasDetalle vd
    INNER JOIN Productos p ON vd.ProductoId = p.Id
    INNER JOIN Ventas v ON vd.VentaId = v.Id
    WHERE v.Fecha >= DATEADD(DAY, -30, CAST(GETDATE() AS DATE))
    GROUP BY p.Nombre
    ORDER BY SUM(vd.Cantidad) DESC;
END
GO