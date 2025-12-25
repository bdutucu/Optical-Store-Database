-- =====================================================
-- TEST DATA FOR LOGIN SYSTEM
-- Run this in SSMS after creating the database
-- =====================================================

USE ESC_GULEN_OPTIK;
GO

-- =====================================================
-- 1. CREATE ADMIN USER (Manager) + CREDENTIALS
-- =====================================================
-- First, create staff if not exists
IF NOT EXISTS (SELECT 1 FROM Staff WHERE Email = 'admin@optik.com')
BEGIN
    INSERT INTO Staff (FirstName, LastName, Email, Salary, Position, DateOfBirth, PhoneNumber, JobStartDate)
    VALUES ('Admin', 'Manager', 'admin@optik.com', 10000, 'Manager', '1985-01-15', '5551000001', '2020-01-01');
    PRINT 'Admin staff created';
END
ELSE
BEGIN
    PRINT 'Admin staff already exists';
END

-- Then add credentials (using subquery to get StaffID)
IF NOT EXISTS (
    SELECT 1 FROM StaffCredentials SC 
    INNER JOIN Staff S ON SC.StaffID = S.StaffID 
    WHERE S.Email = 'admin@optik.com'
)
BEGIN
    INSERT INTO StaffCredentials (StaffID, PasswordHash)
    SELECT StaffID, 'admin123' FROM Staff WHERE Email = 'admin@optik.com';
    PRINT 'Admin credentials created';
END
ELSE
BEGIN
    PRINT 'Admin credentials already exist';
END
GO

-- =====================================================
-- 2. CREATE REGULAR STAFF USER + CREDENTIALS
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM Staff WHERE Email = 'staff@optik.com')
BEGIN
    INSERT INTO Staff (FirstName, LastName, Email, Salary, Position, DateOfBirth, PhoneNumber, JobStartDate)
    VALUES ('John', 'Doe', 'staff@optik.com', 5000, 'Sales', '1990-06-20', '5551000002', '2022-03-15');
    PRINT 'Staff user created';
END
ELSE
BEGIN
    PRINT 'Staff user already exists';
END

-- Add credentials for staff
IF NOT EXISTS (
    SELECT 1 FROM StaffCredentials SC 
    INNER JOIN Staff S ON SC.StaffID = S.StaffID 
    WHERE S.Email = 'staff@optik.com'
)
BEGIN
    INSERT INTO StaffCredentials (StaffID, PasswordHash)
    SELECT StaffID, 'staff123' FROM Staff WHERE Email = 'staff@optik.com';
    PRINT 'Staff credentials created';
END
ELSE
BEGIN
    PRINT 'Staff credentials already exist';
END
GO

-- =====================================================
-- 3. ADD PRODUCT TYPES (if not exists)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM ProductTypes WHERE TypeName = 'FRAME')
BEGIN
    INSERT INTO ProductTypes (TypeName) VALUES ('FRAME'), ('SUNGLASSES'), ('CONTACTLENS'), ('LENS');
    PRINT 'Product types added';
END
GO

-- =====================================================
-- 4. ADD SAMPLE PRODUCTS
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM Product WHERE Brand = 'Ray-Ban')
BEGIN
    INSERT INTO Product (Brand, StockQuantity, Price, ProductTypeID) VALUES 
    ('Ray-Ban', 25, 1500.00, 1),
    ('Oakley', 15, 2000.00, 2),
    ('Acuvue', 50, 350.00, 3),
    ('Essilor', 30, 800.00, 4);
    
    -- Add Frame details
    INSERT INTO Frames (ProductID, FrameModel, ColourCode)
    SELECT ProductID, 'Wayfarer Classic', 'Black' FROM Product WHERE Brand = 'Ray-Ban';
    
    -- Add Sunglasses details
    INSERT INTO Sunglasses (ProductID, SunGlassesSerialNo, Size, ColourCode)
    SELECT ProductID, 'OAK-2024-001', '52-18-140', 'Matte Black' FROM Product WHERE Brand = 'Oakley';
    
    PRINT 'Sample products added';
END
GO

-- =====================================================
-- 5. ADD SAMPLE CUSTOMER
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM Customer WHERE NationalID = '12345678901')
BEGIN
    DECLARE @FirstStaffID INT = (SELECT TOP 1 StaffID FROM Staff ORDER BY StaffID);
    
    INSERT INTO Customer (NationalID, FirstName, LastName, MailAddress, InsuranceInfo, RegisteredByStaffID)
    VALUES ('12345678901', 'Test', 'Customer', 'test@email.com', 'SGK', @FirstStaffID);
    
    PRINT 'Sample customer added';
END
GO

-- =====================================================
-- VERIFICATION QUERIES
-- =====================================================
PRINT '';
PRINT '========== VERIFICATION ==========';

SELECT 'Staff Members:' AS Info;
SELECT StaffID, FirstName, LastName, Email, Position FROM Staff;

SELECT 'Staff Credentials:' AS Info;
SELECT SC.StaffID, S.Email, S.Position, SC.PasswordHash 
FROM StaffCredentials SC 
JOIN Staff S ON SC.StaffID = S.StaffID;

SELECT 'Customers:' AS Info;
SELECT CustomerID, FirstName, LastName, NationalID FROM Customer;

SELECT 'Products:' AS Info;
SELECT ProductID, Brand, Price, StockQuantity FROM Product;

PRINT '';
PRINT '========== LOGIN CREDENTIALS ==========';
PRINT 'Admin:  admin@optik.com / admin123';
PRINT 'Staff:  staff@optik.com / staff123';
PRINT '======================================';
GO
