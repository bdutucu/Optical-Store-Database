CREATE DATABASE ESC_GULEN_OPTIK  -- ya da OptiTrack

GO 
USE ESC_GULEN_OPTIK;
GO

-- EMPLOYEE AND Customer PART UNTIL LINE 40 --
CREATE TABLE Staff (
    StaffID INT PRIMARY KEY IDENTITY(1,1), 
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    Email NVARCHAR(100) UNIQUE NOT NULL,
    Salary MONEY NOT NULL,
    Position NVARCHAR(50),
    DateOfBirth DATE,
    Age AS (DATEDIFF(year, DateOfBirth, GETDATE())), -- computed column(age)
    PhoneNumber NVARCHAR(15) UNIQUE, 
    JobStartDate DATE NOT NULL,
    YearsOfExperience AS (DATEDIFF(year, JobStartDate, GETDATE())), -- computed column(exprerience)
    CONSTRAINT CHK_Staff_Salary CHECK (Salary >= 0)
);

CREATE TABLE Customer (
    CustomerID INT PRIMARY KEY IDENTITY(1,1),
    NationalID NVARCHAR(20) UNIQUE NOT NULL,
    FirstName NVARCHAR(50) NOT NULL,
    LastName NVARCHAR(50) NOT NULL,
    MailAddress NVARCHAR(100),
    InsuranceInfo NVARCHAR(100),
    RegisteredByStaffID INT NOT NULL,
    FOREIGN KEY (RegisteredByStaffID) REFERENCES Staff(StaffID)
);

CREATE TABLE CustomerPhone (
    PhoneID INT PRIMARY KEY IDENTITY(1,1),
    CustomerID INT NOT NULL,
    PhoneNumber NVARCHAR(15) NOT NULL, 
    PhoneType NVARCHAR(10),
    FOREIGN KEY (CustomerID) REFERENCES Customer(CustomerID)
        ON DELETE CASCADE 
);
-- Product AND Product TYPE PART UNTIL LINE 130

-- Lookup table for product types to prevent faulty data insertions
CREATE TABLE ProductTypes (
    ProductTypeID INT PRIMARY KEY IDENTITY(1,1),
    TypeName NVARCHAR(50) NOT NULL UNIQUE
);

-- Transaction Types lookup table (YENİ EKLENEN TABLO)
CREATE TABLE TransactionTypes (
    TransactionTypeID INT PRIMARY KEY IDENTITY(1,1),
    TypeName NVARCHAR(50) NOT NULL UNIQUE  -- 'SALE', 'REPAIR'
);

-- Lookup table for materials (optional, yet prevents inserting same string again and again) 
CREATE TABLE Materials (
    MaterialID INT PRIMARY KEY IDENTITY(1,1),
    MaterialName NVARCHAR(50) NOT NULL UNIQUE,
    Description NVARCHAR(255) NULL
);

CREATE TABLE Product (
    ProductID INT PRIMARY KEY IDENTITY(1,1),
    Brand NVARCHAR(50) NOT NULL,
    StockQuantity INT DEFAULT 0 NOT NULL, 
    Price DECIMAL(18, 2),
    ProductTypeID INT NOT NULL,
    
    -- FK CONSTRAINT EKLENDİ
    CONSTRAINT FK_Product_ProductType FOREIGN KEY (ProductTypeID) 
        REFERENCES ProductTypes(ProductTypeID),
    CONSTRAINT CHK_Product_Stock CHECK (StockQuantity >= 0),  ---eklendi
    CONSTRAINT CHK_Product_Price CHECK (Price >= 0)           ---eklendi  
);

CREATE TABLE Frames (
    ProductID INT PRIMARY KEY,
    FrameModel NVARCHAR(100) NOT NULL,
    ColourCode NVARCHAR(50),  -- YENİ EKLENEN SÜTUN (view_ProductCatalog için gerekli)

    CONSTRAINT FK_Frames_Product FOREIGN KEY (ProductID) 
        REFERENCES Product(ProductID)
);

CREATE TABLE Sunglasses (
    ProductID INT PRIMARY KEY,
    SunGlassesSerialNo NVARCHAR(50) NOT NULL UNIQUE,
    Size NVARCHAR(20),       -- Örn: 52-18-140
    ColourCode NVARCHAR(50), -- Örn: C01 Black

    CONSTRAINT FK_Sunglasses_Product FOREIGN KEY (ProductID) 
        REFERENCES Product(ProductID)
);


CREATE TABLE ContactLenses (
    ProductID INT PRIMARY KEY,
    ContactLensSerialNo NVARCHAR(50) NOT NULL UNIQUE,
    Colour NVARCHAR(50),
    Type NVARCHAR(50), -- Örn: Toric, Multifocal
    -- Measurements Of Right Eye 
    Right_SPH DECIMAL(4, 2), -- Örn: -1.25
    Right_CYL DECIMAL(4, 2), -- Örn: -0.75
    Right_AX INT,            -- Örn: 180

    -- Measurements Of Left Eye 
    Left_SPH DECIMAL(4, 2),
    Left_CYL DECIMAL(4, 2),
    Left_AX INT, 

    CONSTRAINT FK_ContactLenses_Product FOREIGN KEY (ProductID) 
        REFERENCES Product(ProductID)
);

CREATE TABLE Lenses (
    ProductID INT PRIMARY KEY,
    LensSerialNo NVARCHAR(50) NOT NULL UNIQUE,
    Type NVARCHAR(50), -- Örn: Progressive, Single Vision

    -- Measurements Of Right Eye
    Right_SPH DECIMAL(4, 2),
    Right_CYL DECIMAL(4, 2),
    Right_AX INT,

    -- Measurements Of Left Eye
    Left_SPH DECIMAL(4, 2),
    Left_CYL DECIMAL(4, 2),
    Left_AX INT,  

    CONSTRAINT FK_Lenses_Product FOREIGN KEY (ProductID) 
        REFERENCES Product(ProductID)
);

CREATE TABLE ProductMaterials (
    ID INT PRIMARY KEY IDENTITY(1,1),
    ProductID INT NOT NULL,
    MaterialID INT NOT NULL,
    ComponentPart NVARCHAR(50),
    CONSTRAINT FK_ProductMaterials_Product FOREIGN KEY (ProductID) 
        REFERENCES Product(ProductID),
    CONSTRAINT FK_ProductMaterials_Material FOREIGN KEY (MaterialID) 
        REFERENCES Materials(MaterialID)
);

--tek tablo haline geldi icerikler merglendi
-- PRESCRIPTION PART UNTIL 166
CREATE TABLE Prescription (
    PrescriptionID INT PRIMARY KEY IDENTITY(1,1),
    DateOfPrescription DATE NOT NULL,
    DoctorName NVARCHAR(100),  --external doctor, bizim calisan degil
    
    -- EER'de eksik olan ilişkiyi burada kuruyoruz:
    CustomerID INT NOT NULL,
    StaffID INT NULL,  --internal doctor, gozlukcude kontrol ettik hastayi (goz numarasini makinada)

      -- Sağ Göz Ölçüleri
    Right_SPH DECIMAL(4, 2), -- Örn: +2.50
    Right_CYL DECIMAL(4, 2), -- Örn: -0.75
    Right_AX INT,            -- Örn: 180
    
    -- Sol Göz Ölçüleri
    Left_SPH DECIMAL(4, 2),
    Left_CYL DECIMAL(4, 2),
    Left_AX INT,

    -- (Opsiyonel) PD (Pupil Mesafesi) genelde reçetede olur, eklemek istersen:
    -- PD_Distance DECIMAL(4, 1), 


    CONSTRAINT FK_Prescription_Customer FOREIGN KEY (CustomerID) REFERENCES Customer(CustomerID),
    CONSTRAINT FK_Prescription_Staff FOREIGN KEY (StaffID) REFERENCES Staff(StaffID)    
);


-- asagisi eklendi 

CREATE TABLE Transactions (
    TransactionID INT PRIMARY KEY IDENTITY(1,1), -- Hem SaleID hem RepairID yerine geçer
    CustomerID INT NOT NULL,
    StaffID INT NOT NULL,
    TransactionDate DATETIME DEFAULT GETDATE(), -- Tek Tarih (Single Source of Truth)
    TotalAmount MONEY NOT NULL DEFAULT 0,       -- Tek Tutar
    RemainingBalance MONEY NOT NULL DEFAULT 0,  -- DEFAULT 0 EKLENDİ (Ödenecek kalan tutar)

    TransactionTypeID INT NOT NULL,


    FOREIGN KEY (CustomerID) REFERENCES Customer(CustomerID),
    FOREIGN KEY (StaffID) REFERENCES Staff(StaffID),
    
    -- FK CONSTRAINT EKLENDİ (TransactionTypes tablosuna referans)
    CONSTRAINT FK_Transactions_TransactionType FOREIGN KEY (TransactionTypeID) 
        REFERENCES TransactionTypes(TransactionTypeID),

    -- Constraint: Kalan tutar, ana tutardan büyük olamaz
    CONSTRAINT CHK_Balance_Valid CHECK (RemainingBalance >= 0 AND RemainingBalance <= TotalAmount)
);


CREATE TABLE SaleTransaction (
    TransactionID INT PRIMARY KEY,
    
    FOREIGN KEY (TransactionID) REFERENCES Transactions(TransactionID)
        ON DELETE CASCADE 
);

CREATE TABLE RepairTransaction (
    TransactionID INT PRIMARY KEY,
    Description NVARCHAR(255),
    Status NVARCHAR(50),
    
    FOREIGN KEY (TransactionID) REFERENCES Transactions(TransactionID)
        ON DELETE CASCADE 
);

CREATE TABLE SaleItem (
    TransactionID INT NOT NULL, 
    ProductID INT NOT NULL,
    Quantity INT CHECK (Quantity > 0) DEFAULT 1,
    UnitPrice MONEY NOT NULL,
    PrescriptionID INT NULL,
    TaxRate DECIMAL(5, 2) DEFAULT 20.00, 
    SubTotal AS CAST((UnitPrice * Quantity) AS MONEY), 
    TaxAmount AS CAST((UnitPrice * Quantity * (TaxRate / 100.0)) AS MONEY),
    LineTotal AS CAST((UnitPrice * Quantity * (1 + (TaxRate / 100.0))) AS MONEY),

    CONSTRAINT PK_SaleItem PRIMARY KEY (TransactionID, ProductID),

    CONSTRAINT FK_SaleItem_SaleTransaction FOREIGN KEY (TransactionID) 
        REFERENCES SaleTransaction(TransactionID)
        ON DELETE CASCADE,

    CONSTRAINT FK_SaleItem_Product FOREIGN KEY (ProductID) 
        REFERENCES Product(ProductID),

     CONSTRAINT FK_SaleItem_Prescription 
        FOREIGN KEY (PrescriptionID) REFERENCES Prescription(PrescriptionID)
);


CREATE TABLE Invoice (
    InvoiceID INT PRIMARY KEY IDENTITY(1,1),
    TransactionID INT NOT NULL UNIQUE, 
    
    InvoiceNumber NVARCHAR(50) NOT NULL UNIQUE, 
    
    
    -- Fatura Tipi ve Durumu
    IsEInvoice BIT DEFAULT 1,
    Status NVARCHAR(20) DEFAULT 'ISSUED', -- DRAFT, ISSUED, CANCELLED

    -- Ana işlem silinirse faturası da silinsin (Cascade)
    CONSTRAINT FK_Invoice_Transaction FOREIGN KEY (TransactionID) 
        REFERENCES Transactions(TransactionID)
        ON DELETE CASCADE
);

CREATE TABLE Payment (
    PaymentID INT PRIMARY KEY IDENTITY(1,1),
    TransactionID INT NOT NULL,
    PaymentDate DATETIME DEFAULT GETDATE(),
    AmountPaid MONEY NOT NULL,
    PaymentType NVARCHAR(20),

CONSTRAINT FK_Payment_Transaction FOREIGN KEY (TransactionID) 
        REFERENCES Transactions(TransactionID)
        ON DELETE CASCADE,
CONSTRAINT CHK_Payment_Amount CHECK (AmountPaid > 0)
);

CREATE TABLE Cash (
    PaymentID INT PRIMARY KEY,
    ReceivedBy NVARCHAR(50),
    CONSTRAINT FK_Cash_Payment FOREIGN KEY (PaymentID) REFERENCES Payment(PaymentID)
);

CREATE TABLE CreditCard (
    PaymentID INT PRIMARY KEY,
    CardOwner NVARCHAR(100),
    CONSTRAINT FK_CC_Payment FOREIGN KEY (PaymentID) REFERENCES Payment(PaymentID)
);
GO

CREATE TABLE StaffCredentials (
    StaffID INT PRIMARY KEY,
    PasswordHash NVARCHAR(255) NOT NULL,
    LastLoginDate DATETIME NULL,

    CONSTRAINT FK_StaffCredentials_Staff FOREIGN KEY (StaffID) 
        REFERENCES Staff(StaffID)
        ON DELETE CASCADE
);
GO

-- Varsayılan Transaction Type değerlerini ekle
INSERT INTO TransactionTypes (TypeName) VALUES ('SALE'), ('REPAIR');
GO


--eklendi
--  Customer/date reporting and history (covering)
CREATE NONCLUSTERED INDEX IX_Transactions_CustomerDate
ON Transactions (CustomerID, TransactionDate)
INCLUDE (TotalAmount, RemainingBalance, TransactionTypeID);


---- Product browse/search (covering for price/stock)
CREATE NONCLUSTERED INDEX IX_Product_BrandType
ON Product (Brand, ProductTypeID)
INCLUDE (Price, StockQuantity);


--  Payment lookups and daily summaries
CREATE NONCLUSTERED INDEX IX_Payment_TransactionDate
ON Payment (TransactionID, PaymentDate)
INCLUDE (AmountPaid);