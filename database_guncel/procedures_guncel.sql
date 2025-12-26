/* GÜNCELLENMİŞ PROCEDURE DOSYASI
   Değişiklikler: Supertype/Subtype yapısı entegre edildi.
   SaleID -> TransactionID olarak güncellendi.
   TransactionType -> TransactionTypeID (INT) olarak güncellendi.
   Tüm CREATE ifadeleri CREATE OR ALTER olarak güncellendi.
*/

GO
USE ESC_GULEN_OPTIK; -- Hangi DB'de çalışıyorsan onu seçmeyi unutma
GO


CREATE OR ALTER VIEW view_FullInvoiceDetails AS
SELECT 
    I.InvoiceID,
    I.InvoiceNumber,
    T.TransactionDate AS InvoiceDate, -- Transaction'dan geliyor
    
    -- Müşteri Bilgileri
    C.FirstName + ' ' + C.LastName AS CustomerName,
    C.NationalID,
    
    -- Hesaplamalar (Veritabanında tutmaya gerek yok, işlemci hesaplasın)
    ISNULL((SELECT SUM(TaxAmount) FROM SaleItem WHERE TransactionID = T.TransactionID), 0) AS TotalTaxAmount,
    ISNULL((SELECT SUM(SubTotal) FROM SaleItem WHERE TransactionID = T.TransactionID), 0) AS SubTotal,
    I.Status,
    I.IsEInvoice

FROM Invoice I
INNER JOIN Transactions T ON I.TransactionID = T.TransactionID
INNER JOIN Customer C ON T.CustomerID = C.CustomerID;

GO
CREATE OR ALTER VIEW view_ProductCatalog AS
SELECT 
    P.ProductID,
    P.Brand,
    PT.TypeName AS ProductType,
    P.Price,
    P.StockQuantity,
    
    -- Karmaşık Subtype mantığını tek sütuna indirgiyoruz
    COALESCE(F.FrameModel, S.SunGlassesSerialNo, CL.ContactLensSerialNo, L.LensSerialNo, 'Tanımsız') AS ModelOrSerial,
    
    -- Renk Bilgisini de tek sütunda topluyoruz
    COALESCE(F.ColourCode, S.ColourCode, CL.Colour, 'Standart') AS ColorInfo

FROM Product P
JOIN ProductTypes PT ON PT.ProductTypeID = P.ProductTypeID 
LEFT JOIN Frames F ON P.ProductID = F.ProductID
LEFT JOIN Sunglasses S ON P.ProductID = S.ProductID
LEFT JOIN ContactLenses CL ON P.ProductID = CL.ProductID
LEFT JOIN Lenses L ON P.ProductID = L.ProductID;

GO

CREATE OR ALTER VIEW view_DailyFinancialSummary AS
SELECT 
    ReportDate,
    
    -- Ciro: Hem Satıştan hem Tamirden gelen toplam fatura tutarı
    SUM(TotalAmount) AS TotalRevenue,
    
    -- Kasa: O gün tahsil edilen nakit (Ödemeler tablosundan)
    SUM(CollectionAmount) AS CashCollected,
    
    -- Alacak: O gün yapılan işlemlerden kalan bakiye
    SUM(RemainingBalance) AS TotalRemainingBalance,
    
    -- İşlem Adedi: Satış + Tamir toplam sayısı
    SUM(TransactionCount) AS TotalTransactionCount

FROM (
    -- 1. AYAK: İŞLEMLER (Satış + Tamir)
    SELECT 
        CAST(TransactionDate AS DATE) AS ReportDate,
        TotalAmount,
        0 AS CollectionAmount,
        RemainingBalance,
        1 AS TransactionCount
    FROM Transactions
    -- TransactionTypeID kullanılıyor (1 = SALE, 2 = REPAIR)
    WHERE TransactionTypeID IN (1, 2)  

    UNION ALL

    -- 2. AYAK: ÖDEMELER (Tahsilat)
    SELECT 
        CAST(PaymentDate AS DATE) AS ReportDate,
        0 AS TotalAmount,
        AmountPaid AS CollectionAmount,
        0 AS RemainingBalance,
        0 AS TransactionCount
    FROM Payment
) AS CombinedData

GROUP BY ReportDate;
GO


-- 2. SATIŞ OLUŞTURMA 
-- Artık önce Transactions, sonra SaleTransaction dolduruluyor.
CREATE OR ALTER PROCEDURE proc_CreateSale 
    @CustomerID INT,
    @StaffID INT,
    @NewTransactionID INT OUTPUT -- Dışarıya ID fırlatır
AS
BEGIN
    SET NOCOUNT ON;
    
    BEGIN TRANSACTION; -- İki tabloya yazacağımız için Transaction bloğu güvenlidir
    
    BEGIN TRY  -- DEĞİŞTİRİLDİ: TRY -> BEGIN TRY
        -- A. Önce Üst Tablo (Transactions)
        INSERT INTO Transactions (CustomerID, StaffID, TransactionDate, TotalAmount, RemainingBalance, TransactionTypeID)
        VALUES (@CustomerID, @StaffID, GETDATE(), 0, 0, 1);  -- DEĞİŞTİRİLDİ: TransactionType -> TransactionTypeID

        -- Oluşan ID'yi yakala
        SET @NewTransactionID = SCOPE_IDENTITY();

        -- B. Sonra Alt Tablo (SaleTransaction)
        INSERT INTO SaleTransaction (TransactionID)
        VALUES (@NewTransactionID);

        COMMIT TRANSACTION;
    END TRY  -- DEĞİŞTİRİLDİ: CATCH -> END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        SET @NewTransactionID = NULL;
        THROW; -- Hatayı ekrana bas
    END CATCH
END;
GO

-- 3. SEPETE ÜRÜN EKLEME (SaleItem) alteration versiyonu, saklamak için değiştirdim
CREATE OR ALTER PROCEDURE proc_AddSaleItem
    @TransactionID INT,
    @ProductID INT,
    @Quantity INT,
    @PrescriptionID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UnitPrice MONEY;
    DECLARE @ProductTypeID INT;
    DECLARE @CalculatedTaxRate DECIMAL(5,2);
    DECLARE @CustomerID INT;

    -- Fetch transaction and customer
    SELECT 
        @CustomerID = CustomerID
    FROM Transactions
    WHERE TransactionID = @TransactionID;

    IF @CustomerID IS NULL
    BEGIN
        RAISERROR('Hata: İşlem (Transaction) bulunamadı.', 16, 1);
        RETURN;
    END

    -- 2. Prescription ownership check (unchanged)
    IF @PrescriptionID IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM Prescription
            WHERE PrescriptionID = @PrescriptionID
              AND CustomerID = @CustomerID
        )
        BEGIN
            RAISERROR('Hata: Girilen reçete bu müşteriye ait değil!', 16, 1);
            RETURN;
        END
    END

    -- Price & product type
    SELECT @UnitPrice = Price, @ProductTypeID = ProductTypeID
    FROM Product
    WHERE ProductID = @ProductID;

    SET @UnitPrice = ISNULL(@UnitPrice, 0);

    -- Tax rate
    IF @ProductTypeID IN (1, 2) SET @CalculatedTaxRate = 10.00;
    ELSE IF @ProductTypeID = 3 SET @CalculatedTaxRate = 20.00;
    ELSE SET @CalculatedTaxRate = 20.00;

    BEGIN TRAN;
    BEGIN TRY
        -- Upsert logic
        IF EXISTS (SELECT 1 FROM SaleItem WHERE TransactionID = @TransactionID AND ProductID = @ProductID)
        BEGIN
            UPDATE SaleItem
            SET Quantity = Quantity + @Quantity
            WHERE TransactionID = @TransactionID AND ProductID = @ProductID;
        END
        ELSE
        BEGIN
            INSERT INTO SaleItem (TransactionID, ProductID, Quantity, UnitPrice, TaxRate, PrescriptionID)
            VALUES (@TransactionID, @ProductID, @Quantity, @UnitPrice, @CalculatedTaxRate, @PrescriptionID);
        END

        -- Recalculate transaction totals
        DECLARE @LineTotalWithTax MONEY = @UnitPrice * @Quantity * (1.00 + (@CalculatedTaxRate / 100.00));

        UPDATE Transactions
        SET TotalAmount     = TotalAmount + @LineTotalWithTax,
            RemainingBalance = RemainingBalance + @LineTotalWithTax
        WHERE TransactionID = @TransactionID;

        COMMIT;
    END TRY
    BEGIN CATCH
        ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- 4. MÜŞTERİ GEÇMİŞİ
CREATE OR ALTER PROCEDURE proc_GetCustomerTransactionHistory
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        T.TransactionID,
        T.TransactionDate,
        
        -- ID olan Tipi ekranda okunabilir hale getiriyoruz
        CASE T.TransactionTypeID  -- DEĞİŞTİRİLDİ: TransactionType -> TransactionTypeID
            WHEN 1 THEN 'Satış'
            WHEN 2 THEN 'Tamir'
            ELSE 'Bilinmiyor'
        END AS ProcessType,

        -- Finansal Bilgiler
        T.TotalAmount,
        T.RemainingBalance,
        
        -- Personel Bilgisi
        St.FirstName + ' ' + St.LastName AS ServedBy

    FROM Transactions T
    INNER JOIN Staff St ON T.StaffID = St.StaffID

    WHERE T.CustomerID = @CustomerID
    ORDER BY T.TransactionDate DESC;
END;
GO

-- 5. SATIŞ DETAYLARI
CREATE OR ALTER PROCEDURE proc_GetTransactionDetails
    @TransactionID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TypeID INT;
    
    -- 1. İşlem tipinin ID'sini öğreniyoruz
    SELECT @TypeID = TransactionTypeID  -- DEĞİŞTİRİLDİ: TransactionType -> TransactionTypeID
    FROM Transactions 
    WHERE TransactionID = @TransactionID;

    -- 2. ID'ye göre ilgili tabloya git
    -- 1 = SALE (Satış)
    IF @TypeID = 1 
    BEGIN
        SELECT 
            'SALE' AS DetailType,
            P.Brand AS ItemName,
            PT.TypeName AS Category,
            SI.Quantity,
            SI.UnitPrice,
            SI.TaxRate,
            SI.SubTotal AS PreTaxTotal, 
            SI.TaxAmount, 
            (SI.SubTotal + SI.TaxAmount) AS TotalPrice,
            NULL AS Description,
            NULL AS Status
        FROM SaleItem SI
        INNER JOIN Product P ON SI.ProductID = P.ProductID
        INNER JOIN ProductTypes PT ON PT.ProductTypeID = P.ProductTypeID
        WHERE SI.TransactionID = @TransactionID;
    END
    
    -- 2 = REPAIR (Tamir)
    ELSE IF @TypeID = 2 
    BEGIN
        SELECT 
            'REPAIR' AS DetailType,
            'Tamir Hizmeti' AS ItemName,
            'SERVICE' AS Category,
            1 AS Quantity,
            T.TotalAmount AS UnitPrice,
            T.TotalAmount AS TotalPrice,
            R.Description,
            R.Status
        FROM Transactions T
        INNER JOIN RepairTransaction R ON T.TransactionID = R.TransactionID
        WHERE T.TransactionID = @TransactionID;
    END
END;
GO

-- 6. ÜRÜN ARAMA (Supertype/Subtype mantığına dokunmaz, tablolara bakar)
CREATE OR ALTER PROCEDURE proc_SearchProducts 
    @ProductCategory NVARCHAR(20) = NULL, 
    @ColorCode NVARCHAR(50) = NULL,       
    @MaterialName NVARCHAR(50) = NULL,    
    @MinPrice DECIMAL(18,2) = NULL,       
    @MaxPrice DECIMAL(18,2) = NULL        
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        P.ProductID,
        P.Brand,
        PT.TypeName AS ProductType,
        P.StockQuantity,
        P.Price,
        
        -- Model Bilgisi
        CASE 
            WHEN PT.TypeName = 'FRAME' THEN F.FrameModel
            WHEN PT.TypeName = 'SUNGLASSES' THEN S.SunGlassesSerialNo
            WHEN PT.TypeName = 'CONTACTLENS' THEN CL.ContactLensSerialNo
            WHEN PT.TypeName = 'LENS' THEN L.LensSerialNo
            ELSE 'Unknown'
        END AS ModelOrSerial,

        -- Renk Bilgisi
        COALESCE(F.ColourCode, S.ColourCode, CL.Colour, 'N/A') AS ColorInfo
        
    FROM Product P
    INNER JOIN ProductTypes PT ON PT.ProductTypeID = P.ProductTypeID
    LEFT JOIN Frames F ON P.ProductID = F.ProductID
    LEFT JOIN Sunglasses S ON P.ProductID = S.ProductID
    LEFT JOIN ContactLenses CL ON P.ProductID = CL.ProductID
    LEFT JOIN Lenses L ON P.ProductID = L.ProductID 
    WHERE 
        (@ProductCategory IS NULL OR PT.TypeName = @ProductCategory)
        AND (@MinPrice IS NULL OR P.Price >= @MinPrice)
        AND (@MaxPrice IS NULL OR P.Price <= @MaxPrice)
        AND
        (@ColorCode IS NULL OR 
         (F.ColourCode LIKE '%' + @ColorCode + '%') OR 
         (S.ColourCode LIKE '%' + @ColorCode + '%') OR
         (CL.Colour LIKE '%' + @ColorCode + '%'))
        AND
        (@MaterialName IS NULL OR EXISTS (
            SELECT 1 
            FROM ProductMaterials PM
            INNER JOIN Materials M ON PM.MaterialID = M.MaterialID
            WHERE PM.ProductID = P.ProductID 
              AND M.MaterialName LIKE '%' + @MaterialName + '%'
        ));
END;
GO

--7 Login sayfası
CREATE OR ALTER PROCEDURE proc_StaffLogin
    @Email NVARCHAR(100),
    @PasswordHash NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FoundStaffID INT;

    -- 1. Kullanıcıyı Email adresine göre Staff tablosundan bul
    -- ve Credentials tablosuyla birleştir.
    SELECT 
        @FoundStaffID = S.StaffID
    FROM Staff S
    INNER JOIN StaffCredentials SC ON S.StaffID = SC.StaffID
    WHERE S.Email = @Email  -- DEĞİŞTİRİLDİ: MailAddress -> Email (Staff tablosundaki sütun adı)
      AND SC.PasswordHash = @PasswordHash;

    -- 2. Sonuç Kontrolü
    IF @FoundStaffID IS NOT NULL
    BEGIN
            -- Giriş Başarılı: Son giriş tarihini güncelle
            UPDATE StaffCredentials 
            SET LastLoginDate = GETDATE() 
            WHERE StaffID = @FoundStaffID;

            -- Geriye personelin ID'sini ve Rolünü/Pozisyonunu döndür
            SELECT 
                S.StaffID, 
                S.FirstName, 
                S.LastName, 
                S.Position,
                'SUCCESS' AS LoginStatus
            FROM Staff S
            WHERE S.StaffID = @FoundStaffID;
    END
    ELSE
    BEGIN
        RAISERROR('Kullanıcı bulunamadı.',16,1);
    END
END
GO

CREATE OR ALTER PROCEDURE proc_DeleteCustomerHistory
    @CustomerID INT
AS
BEGIN
    SET NOCOUNT ON;

        DELETE FROM Transactions
        WHERE CustomerID = @CustomerID;

        PRINT 'Müşteriye ait tüm finansal geçmiş başarıyla silindi.';
END;
GO

CREATE OR ALTER PROCEDURE proc_DeleteTransaction
    @TransactionID INT
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Güvenlik Kontrolü: Kayıt var mı?
    IF NOT EXISTS (SELECT 1 FROM Transactions WHERE TransactionID = @TransactionID)
    BEGIN

        RAISERROR('Hata: Silinmek istenen işlem bulunamadı.', 16, 1);
        RETURN;
    END

    DELETE FROM Transactions WHERE TransactionID = @TransactionID;
END;
GO

CREATE OR ALTER PROCEDURE proc_DeleteAllTransactionsWithinTimeRange
    @MonthsToKeep INT -- Örn: 12, 18, 24, 36
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. GÜVENLİK KONTROLÜ
    IF @MonthsToKeep <= 0
    BEGIN
        RAISERROR('Hata: Geçersiz ay sayısı! Lütfen 0 dan büyük bir değer giriniz.', 16, 1);
        RETURN;
    END

    -- 2. TARİH HESAPLAMA
    DECLARE @CutoffDate DATETIME = DATEADD(MONTH, -@MonthsToKeep, GETDATE());

    DELETE FROM Transactions
        WHERE TransactionDate < @CutoffDate;
END;
GO

CREATE OR ALTER PROCEDURE proc_StaffPerformanceReport
    @StartDate DATE,
    @EndDate DATE
AS
BEGIN
    SELECT 
        St.FirstName + ' ' + St.LastName AS StaffName,
        COUNT(T.TransactionID) AS TotalTransactions,
        ISNULL(SUM(T.TotalAmount), 0) AS TotalRevenueGenerated
    FROM Staff St
    LEFT JOIN Transactions T ON St.StaffID = T.StaffID
    AND (T.TransactionDate BETWEEN @StartDate AND @EndDate)
    AND T.TransactionTypeID = 1  -- DEĞİŞTİRİLDİ: TransactionType = 'SALE' -> TransactionTypeID = 1
    GROUP BY St.StaffID, St.FirstName, St.LastName
    ORDER BY TotalRevenueGenerated DESC;
END;
GO

CREATE OR ALTER PROCEDURE proc_GetMonthlyFinancialReport
    @Month INT,
    @Year INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        CAST(@Year AS NVARCHAR(4)) + ' - ' + CAST(@Month AS NVARCHAR(2)) AS Period,
        
        ISNULL(SUM(TotalRevenue), 0) AS TotalRevenue,
        ISNULL(SUM(CashCollected), 0) AS CashInflow,
        
        ISNULL(SUM(TotalRemainingBalance), 0) AS PendingReceivables, 
        
        ISNULL(SUM(TotalTransactionCount), 0) AS TransactionCount

    FROM view_DailyFinancialSummary
    WHERE MONTH(ReportDate) = @Month 
      AND YEAR(ReportDate) = @Year;
END;
GO

CREATE OR ALTER TRIGGER trg_UpdateRemainingBalanceOnPayment
ON Payment
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- 1. Hata Kontrolü: Ödenen tutar kalan borçtan fazla mı?
    IF EXISTS (
        SELECT 1 
        FROM inserted i
        INNER JOIN Transactions t ON i.TransactionID = t.TransactionID
        WHERE i.AmountPaid > t.RemainingBalance
    )
    BEGIN
        RAISERROR ('HATA: Ödenen tutar, kalan borçtan fazla olamaz!', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- 2. Transactions Bakiyesini Güncelle
    UPDATE T
    SET T.RemainingBalance = T.RemainingBalance - i.AmountPaid
    FROM Transactions T
    INNER JOIN inserted i ON T.TransactionID = i.TransactionID;
END;
GO

CREATE OR ALTER TRIGGER trg_MaintainStock
ON SaleItem
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    
    -- 1. STOK YETERLİLİK KONTROLÜ
    IF EXISTS (
        SELECT 1 
        FROM Product P
        INNER JOIN inserted i ON P.ProductID = i.ProductID
        LEFT JOIN deleted d ON i.TransactionID = d.TransactionID AND i.ProductID = d.ProductID
        WHERE P.StockQuantity < (i.Quantity - ISNULL(d.Quantity, 0))
    )
    BEGIN
        RAISERROR ('HATA: Yetersiz Stok!', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- 2. STOK GÜNCELLEME (Delta Yöntemi)
    UPDATE P
    SET P.StockQuantity = P.StockQuantity - (i.Quantity - ISNULL(d.Quantity, 0))
    FROM Product P
    INNER JOIN inserted i ON P.ProductID = i.ProductID
    LEFT JOIN deleted d ON i.TransactionID = d.TransactionID AND i.ProductID = d.ProductID;
END;
GO


CREATE OR ALTER VIEW view_CustomerOutstandingBalances AS
SELECT 
    C.CustomerID,
    C.FirstName + ' ' + C.LastName AS CustomerName,
    SUM(T.RemainingBalance) AS TotalOutstanding,
    MAX(T.TransactionDate) AS LastTransactionDate,
    COUNT(*) AS TransactionCount
FROM Customer C
JOIN Transactions T ON C.CustomerID = T.CustomerID
GROUP BY C.CustomerID, C.FirstName, C.LastName;
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
-- NOT: Göz ölçüleri artık Prescription tablosunda tutulduğu için burada yok
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
        COALESCE(CL.Type, L.Type) AS LensType
        -- Göz ölçüleri kaldırıldı - artık Prescription tablosunda
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

PRINT '✅ Tüm VIEW, PROCEDURE ve TRIGGER başarıyla güncellendi!';
GO
