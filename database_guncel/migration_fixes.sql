-- =============================================
-- MIGRATION SCRIPT: Sorun Düzeltmeleri
-- 1. Customer silme için CASCADE DELETE
-- 2. Cash/CreditCard için CASCADE DELETE
-- 3. proc_AddProduct güncelleme (Lens/ContactLens için material yok)
-- 4. Çoklu material ekleme procedure'ı
-- =============================================

USE ESC_GULEN_OPTIK;
GO

-- =============================================
-- 1. CUSTOMER SİLME SORUNU - CASCADE DELETE EKLE
-- =============================================

-- Önce mevcut FK'ları kaldır
ALTER TABLE Transactions DROP CONSTRAINT IF EXISTS FK_Transactions_Customer;
ALTER TABLE Prescription DROP CONSTRAINT IF EXISTS FK_Prescription_Customer;

-- Yeni FK'ları CASCADE DELETE ile ekle
ALTER TABLE Transactions
ADD CONSTRAINT FK_Transactions_Customer 
    FOREIGN KEY (CustomerID) REFERENCES Customer(CustomerID)
    ON DELETE CASCADE;

ALTER TABLE Prescription
ADD CONSTRAINT FK_Prescription_Customer 
    FOREIGN KEY (CustomerID) REFERENCES Customer(CustomerID)
    ON DELETE CASCADE;

PRINT '✅ Customer CASCADE DELETE eklendi';
GO

-- =============================================
-- 2. CASH/CREDITCARD İÇİN CASCADE DELETE
-- =============================================

-- Payment silindiğinde Cash/CreditCard da silinsin
ALTER TABLE Cash DROP CONSTRAINT IF EXISTS FK_Cash_Payment;
ALTER TABLE CreditCard DROP CONSTRAINT IF EXISTS FK_CC_Payment;

ALTER TABLE Cash
ADD CONSTRAINT FK_Cash_Payment 
    FOREIGN KEY (PaymentID) REFERENCES Payment(PaymentID)
    ON DELETE CASCADE;

ALTER TABLE CreditCard
ADD CONSTRAINT FK_CC_Payment 
    FOREIGN KEY (PaymentID) REFERENCES Payment(PaymentID)
    ON DELETE CASCADE;

PRINT '✅ Cash/CreditCard CASCADE DELETE eklendi';
GO

-- =============================================
-- 3. PRODUCTMATERIALS İÇİN CASCADE DELETE
-- =============================================

-- Product silindiğinde materials da silinsin
ALTER TABLE ProductMaterials DROP CONSTRAINT IF EXISTS FK_ProductMaterials_Product;

ALTER TABLE ProductMaterials
ADD CONSTRAINT FK_ProductMaterials_Product 
    FOREIGN KEY (ProductID) REFERENCES Product(ProductID)
    ON DELETE CASCADE;

PRINT '✅ ProductMaterials CASCADE DELETE eklendi';
GO

-- =============================================
-- 4. proc_AddProduct GÜNCELLEME 
-- Lens ve ContactLens için material eklenmez
-- =============================================

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
    -- Material (sadece FRAME ve SUNGLASSES için)
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
        
        -- 3. Insert material ONLY for FRAME (1) and SUNGLASSES (2)
        -- Lens ve ContactLens için material EKLENMİYOR
        IF @MaterialID IS NOT NULL AND @ProductTypeID IN (1, 2)
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

PRINT '✅ proc_AddProduct güncellendi (Lens/ContactLens için material yok)';
GO

-- =============================================
-- 5. ÇOKLU MATERIAL EKLEME PROCEDURE'I
-- =============================================

CREATE OR ALTER PROCEDURE proc_AddMultipleProductMaterials
    @ProductID INT,
    @MaterialIDs NVARCHAR(MAX),  -- Comma-separated: '1,2,3'
    @ComponentParts NVARCHAR(MAX) = NULL  -- Comma-separated: 'Frame,Temple,Bridge'
AS
BEGIN
    SET NOCOUNT ON;
    
    -- ProductTypeID kontrolü - sadece FRAME ve SUNGLASSES için izin ver
    DECLARE @ProductTypeID INT;
    SELECT @ProductTypeID = ProductTypeID FROM Product WHERE ProductID = @ProductID;
    
    IF @ProductTypeID NOT IN (1, 2) -- FRAME veya SUNGLASSES değilse
    BEGIN
        RAISERROR('Material sadece Frame ve Sunglasses ürünleri için eklenebilir.', 16, 1);
        RETURN;
    END
    
    -- Önce mevcut materialleri sil
    DELETE FROM ProductMaterials WHERE ProductID = @ProductID;
    
    -- Material ID'leri parse et ve ekle
    DECLARE @MaterialTable TABLE (Idx INT IDENTITY(1,1), MaterialID INT);
    DECLARE @PartTable TABLE (Idx INT IDENTITY(1,1), PartName NVARCHAR(50));
    
    -- MaterialIDs'yi parse et
    INSERT INTO @MaterialTable (MaterialID)
    SELECT CAST(value AS INT) FROM STRING_SPLIT(@MaterialIDs, ',') WHERE LTRIM(RTRIM(value)) <> '';
    
    -- ComponentParts'ı parse et (varsa)
    IF @ComponentParts IS NOT NULL AND @ComponentParts <> ''
    BEGIN
        INSERT INTO @PartTable (PartName)
        SELECT LTRIM(RTRIM(value)) FROM STRING_SPLIT(@ComponentParts, ',');
    END
    
    -- Her material için ProductMaterials'a ekle
    INSERT INTO ProductMaterials (ProductID, MaterialID, ComponentPart)
    SELECT 
        @ProductID,
        m.MaterialID,
        ISNULL(p.PartName, NULL)
    FROM @MaterialTable m
    LEFT JOIN @PartTable p ON m.Idx = p.Idx;
    
    PRINT 'Materialler başarıyla eklendi.';
END;
GO

PRINT '✅ proc_AddMultipleProductMaterials oluşturuldu';
GO

-- =============================================
-- 6. proc_UpdateProduct GÜNCELLEME
-- Lens ve ContactLens için material yok
-- =============================================

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
            -- ContactLens için material sil (varsa)
            DELETE FROM ProductMaterials WHERE ProductID = @ProductID;
            
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
            -- Lens için material sil (varsa)
            DELETE FROM ProductMaterials WHERE ProductID = @ProductID;
            
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

PRINT '✅ proc_UpdateProduct güncellendi';
GO

-- =============================================
-- 7. CUSTOMER EMAIL UNIQUE CONSTRAINT
-- =============================================

-- Önce mevcut constraint varsa kaldır
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_Customer_MailAddress' AND object_id = OBJECT_ID('Customer'))
BEGIN
    ALTER TABLE Customer DROP CONSTRAINT UQ_Customer_MailAddress;
END

-- Unique constraint ekle (NULL değerler için birden fazla satıra izin verir)
-- SQL Server'da UNIQUE constraint NULL değerlere izin verir
ALTER TABLE Customer ADD CONSTRAINT UQ_Customer_MailAddress UNIQUE (MailAddress);

PRINT '✅ Customer MailAddress UNIQUE constraint eklendi';
GO

PRINT '';
PRINT '========================================';
PRINT '✅ TÜM MIGRATION İŞLEMLERİ TAMAMLANDI!';
PRINT '========================================';
PRINT '';
PRINT 'Değişiklikler:';
PRINT '1. Customer silindiğinde Transactions ve Prescriptions da silinir';
PRINT '2. Payment silindiğinde Cash/CreditCard da silinir';
PRINT '3. Product silindiğinde ProductMaterials da silinir';
PRINT '4. Lens/ContactLens için material eklenemez';
PRINT '5. Çoklu material ekleme desteği (proc_AddMultipleProductMaterials)';
PRINT '6. Customer MailAddress UNIQUE constraint';
GO

