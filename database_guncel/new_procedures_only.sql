-- =============================================
-- SADECE YENİ EKLENEN PROCEDURE'LAR
-- Bu dosyayı SSMS'de çalıştırın
-- =============================================

USE ESC_GULEN_OPTIK;
GO

-- =============================================
-- PAYMENT PROCEDURES WITH SUBTYPE SUPPORT
-- =============================================

-- proc_AddPayment: Creates payment with Cash or CreditCard subtype
CREATE OR ALTER PROCEDURE proc_AddPayment
    @TransactionID INT,
    @AmountPaid MONEY,
    @PaymentType NVARCHAR(20),  -- 'Cash' or 'CreditCard'
    -- Cash specific
    @ReceivedBy NVARCHAR(50) = NULL,
    -- CreditCard specific
    @CardOwner NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @NewPaymentID INT;
    
    BEGIN TRANSACTION;
    BEGIN TRY
        -- 1. Insert into Payment (supertype)
        INSERT INTO Payment (TransactionID, PaymentDate, AmountPaid, PaymentType)
        VALUES (@TransactionID, GETDATE(), @AmountPaid, @PaymentType);
        
        SET @NewPaymentID = SCOPE_IDENTITY();
        
        -- 2. Insert into subtype table based on PaymentType
        IF @PaymentType = 'Cash'
        BEGIN
            INSERT INTO Cash (PaymentID, ReceivedBy)
            VALUES (@NewPaymentID, @ReceivedBy);
        END
        ELSE IF @PaymentType = 'CreditCard'
        BEGIN
            INSERT INTO CreditCard (PaymentID, CardOwner)
            VALUES (@NewPaymentID, @CardOwner);
        END
        
        COMMIT TRANSACTION;
        
        -- Return the new PaymentID
        SELECT @NewPaymentID AS PaymentID;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- =============================================
-- PRODUCT PROCEDURES WITH SUBTYPE SUPPORT
-- =============================================

-- proc_AddProduct: Creates product with subtype and materials
CREATE OR ALTER PROCEDURE proc_AddProduct
    @Brand NVARCHAR(50),
    @StockQuantity INT,
    @Price DECIMAL(18,2),
    @ProductTypeID INT,
    -- Subtype specific fields
    @ModelOrSerial NVARCHAR(100) = NULL,
    @ColourCode NVARCHAR(50) = NULL,
    @Size NVARCHAR(20) = NULL,
    @LensType NVARCHAR(50) = NULL,
    -- Eye measurements (for ContactLenses and Lenses)
    @Right_SPH DECIMAL(4,2) = NULL,
    @Right_CYL DECIMAL(4,2) = NULL,
    @Right_AX INT = NULL,
    @Left_SPH DECIMAL(4,2) = NULL,
    @Left_CYL DECIMAL(4,2) = NULL,
    @Left_AX INT = NULL,
    -- Material (comma-separated MaterialIDs or single MaterialID)
    @MaterialID INT = NULL,
    @MaterialPart NVARCHAR(50) = NULL,
    -- Output
    @NewProductID INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    BEGIN TRY
        -- 1. Insert into Product (supertype)
        INSERT INTO Product (Brand, StockQuantity, Price, ProductTypeID)
        VALUES (@Brand, @StockQuantity, @Price, @ProductTypeID);
        
        SET @NewProductID = SCOPE_IDENTITY();
        
        -- 2. Insert into subtype table based on ProductTypeID
        -- 1 = FRAME, 2 = SUNGLASSES, 3 = CONTACTLENS, 4 = LENS
        
        IF @ProductTypeID = 1 -- FRAME
        BEGIN
            INSERT INTO Frames (ProductID, FrameModel, ColourCode)
            VALUES (@NewProductID, @ModelOrSerial, @ColourCode);
        END
        ELSE IF @ProductTypeID = 2 -- SUNGLASSES
        BEGIN
            INSERT INTO Sunglasses (ProductID, SunGlassesSerialNo, Size, ColourCode)
            VALUES (@NewProductID, @ModelOrSerial, @Size, @ColourCode);
        END
        ELSE IF @ProductTypeID = 3 -- CONTACTLENS
        BEGIN
            INSERT INTO ContactLenses (ProductID, ContactLensSerialNo, Colour, Type,
                Right_SPH, Right_CYL, Right_AX, Left_SPH, Left_CYL, Left_AX)
            VALUES (@NewProductID, @ModelOrSerial, @ColourCode, @LensType,
                @Right_SPH, @Right_CYL, @Right_AX, @Left_SPH, @Left_CYL, @Left_AX);
        END
        ELSE IF @ProductTypeID = 4 -- LENS
        BEGIN
            INSERT INTO Lenses (ProductID, LensSerialNo, Type,
                Right_SPH, Right_CYL, Right_AX, Left_SPH, Left_CYL, Left_AX)
            VALUES (@NewProductID, @ModelOrSerial, @LensType,
                @Right_SPH, @Right_CYL, @Right_AX, @Left_SPH, @Left_CYL, @Left_AX);
        END
        
        -- 3. Insert material if provided
        IF @MaterialID IS NOT NULL
        BEGIN
            INSERT INTO ProductMaterials (ProductID, MaterialID, ComponentPart)
            VALUES (@NewProductID, @MaterialID, @MaterialPart);
        END
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SET @NewProductID = NULL;
        THROW;
    END CATCH
END;
GO

-- proc_UpdateProduct: Updates product with subtype
CREATE OR ALTER PROCEDURE proc_UpdateProduct
    @ProductID INT,
    @Brand NVARCHAR(50),
    @StockQuantity INT,
    @Price DECIMAL(18,2),
    @ProductTypeID INT,
    -- Subtype specific fields
    @ModelOrSerial NVARCHAR(100) = NULL,
    @ColourCode NVARCHAR(50) = NULL,
    @Size NVARCHAR(20) = NULL,
    @LensType NVARCHAR(50) = NULL,
    -- Eye measurements (for ContactLenses and Lenses)
    @Right_SPH DECIMAL(4,2) = NULL,
    @Right_CYL DECIMAL(4,2) = NULL,
    @Right_AX INT = NULL,
    @Left_SPH DECIMAL(4,2) = NULL,
    @Left_CYL DECIMAL(4,2) = NULL,
    @Left_AX INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION;
    BEGIN TRY
        -- 1. Update Product (supertype)
        UPDATE Product 
        SET Brand = @Brand, 
            StockQuantity = @StockQuantity, 
            Price = @Price,
            ProductTypeID = @ProductTypeID
        WHERE ProductID = @ProductID;
        
        -- 2. Update subtype table based on ProductTypeID
        IF @ProductTypeID = 1 -- FRAME
        BEGIN
            IF EXISTS (SELECT 1 FROM Frames WHERE ProductID = @ProductID)
                UPDATE Frames SET FrameModel = @ModelOrSerial, ColourCode = @ColourCode
                WHERE ProductID = @ProductID;
            ELSE
                INSERT INTO Frames (ProductID, FrameModel, ColourCode)
                VALUES (@ProductID, @ModelOrSerial, @ColourCode);
        END
        ELSE IF @ProductTypeID = 2 -- SUNGLASSES
        BEGIN
            IF EXISTS (SELECT 1 FROM Sunglasses WHERE ProductID = @ProductID)
                UPDATE Sunglasses SET SunGlassesSerialNo = @ModelOrSerial, Size = @Size, ColourCode = @ColourCode
                WHERE ProductID = @ProductID;
            ELSE
                INSERT INTO Sunglasses (ProductID, SunGlassesSerialNo, Size, ColourCode)
                VALUES (@ProductID, @ModelOrSerial, @Size, @ColourCode);
        END
        ELSE IF @ProductTypeID = 3 -- CONTACTLENS
        BEGIN
            IF EXISTS (SELECT 1 FROM ContactLenses WHERE ProductID = @ProductID)
                UPDATE ContactLenses SET ContactLensSerialNo = @ModelOrSerial, Colour = @ColourCode, Type = @LensType,
                    Right_SPH = @Right_SPH, Right_CYL = @Right_CYL, Right_AX = @Right_AX,
                    Left_SPH = @Left_SPH, Left_CYL = @Left_CYL, Left_AX = @Left_AX
                WHERE ProductID = @ProductID;
            ELSE
                INSERT INTO ContactLenses (ProductID, ContactLensSerialNo, Colour, Type,
                    Right_SPH, Right_CYL, Right_AX, Left_SPH, Left_CYL, Left_AX)
                VALUES (@ProductID, @ModelOrSerial, @ColourCode, @LensType,
                    @Right_SPH, @Right_CYL, @Right_AX, @Left_SPH, @Left_CYL, @Left_AX);
        END
        ELSE IF @ProductTypeID = 4 -- LENS
        BEGIN
            IF EXISTS (SELECT 1 FROM Lenses WHERE ProductID = @ProductID)
                UPDATE Lenses SET LensSerialNo = @ModelOrSerial, Type = @LensType,
                    Right_SPH = @Right_SPH, Right_CYL = @Right_CYL, Right_AX = @Right_AX,
                    Left_SPH = @Left_SPH, Left_CYL = @Left_CYL, Left_AX = @Left_AX
                WHERE ProductID = @ProductID;
            ELSE
                INSERT INTO Lenses (ProductID, LensSerialNo, Type,
                    Right_SPH, Right_CYL, Right_AX, Left_SPH, Left_CYL, Left_AX)
                VALUES (@ProductID, @ModelOrSerial, @LensType,
                    @Right_SPH, @Right_CYL, @Right_AX, @Left_SPH, @Left_CYL, @Left_AX);
        END
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- proc_AddProductMaterial: Adds material to a product
CREATE OR ALTER PROCEDURE proc_AddProductMaterial
    @ProductID INT,
    @MaterialID INT,
    @ComponentPart NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Check if material already exists for this product
    IF EXISTS (SELECT 1 FROM ProductMaterials WHERE ProductID = @ProductID AND MaterialID = @MaterialID)
    BEGIN
        UPDATE ProductMaterials 
        SET ComponentPart = @ComponentPart
        WHERE ProductID = @ProductID AND MaterialID = @MaterialID;
    END
    ELSE
    BEGIN
        INSERT INTO ProductMaterials (ProductID, MaterialID, ComponentPart)
        VALUES (@ProductID, @MaterialID, @ComponentPart);
    END
END;
GO

-- proc_GetProductDetails: Gets full product details including subtype
CREATE OR ALTER PROCEDURE proc_GetProductDetails
    @ProductID INT
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @ProductTypeID INT;
    
    SELECT @ProductTypeID = ProductTypeID FROM Product WHERE ProductID = @ProductID;
    
    -- Return main product info
    SELECT 
        P.ProductID, P.Brand, P.StockQuantity, P.Price, P.ProductTypeID,
        PT.TypeName AS ProductType,
        -- Subtype fields (COALESCE for different types)
        COALESCE(F.FrameModel, S.SunGlassesSerialNo, CL.ContactLensSerialNo, L.LensSerialNo) AS ModelOrSerial,
        COALESCE(F.ColourCode, S.ColourCode, CL.Colour) AS ColourCode,
        S.Size,
        COALESCE(CL.Type, L.Type) AS LensType,
        -- Eye measurements
        COALESCE(CL.Right_SPH, L.Right_SPH) AS Right_SPH,
        COALESCE(CL.Right_CYL, L.Right_CYL) AS Right_CYL,
        COALESCE(CL.Right_AX, L.Right_AX) AS Right_AX,
        COALESCE(CL.Left_SPH, L.Left_SPH) AS Left_SPH,
        COALESCE(CL.Left_CYL, L.Left_CYL) AS Left_CYL,
        COALESCE(CL.Left_AX, L.Left_AX) AS Left_AX
    FROM Product P
    INNER JOIN ProductTypes PT ON P.ProductTypeID = PT.ProductTypeID
    LEFT JOIN Frames F ON P.ProductID = F.ProductID
    LEFT JOIN Sunglasses S ON P.ProductID = S.ProductID
    LEFT JOIN ContactLenses CL ON P.ProductID = CL.ProductID
    LEFT JOIN Lenses L ON P.ProductID = L.ProductID
    WHERE P.ProductID = @ProductID;
    
    -- Return materials for this product
    SELECT M.MaterialID, M.MaterialName, PM.ComponentPart
    FROM ProductMaterials PM
    INNER JOIN Materials M ON PM.MaterialID = M.MaterialID
    WHERE PM.ProductID = @ProductID;
END;
GO

PRINT '✅ Yeni procedure''lar başarıyla oluşturuldu!';
PRINT '   - proc_AddPayment';
PRINT '   - proc_AddProduct';
PRINT '   - proc_UpdateProduct';
PRINT '   - proc_AddProductMaterial';
PRINT '   - proc_GetProductDetails';
GO

