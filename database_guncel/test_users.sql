-- Test Users for ESC Gülen Optik
-- Run this script in SSMS after creating the database

USE ESC_GULEN_OPTIK;
GO

-- Check if test users already exist
IF NOT EXISTS (SELECT 1 FROM Staff WHERE Email = 'admin@optik.com')
BEGIN
    -- Create Admin User (Manager position = Admin access)
    INSERT INTO Staff (FirstName, LastName, Email, Salary, Position, DateOfBirth, PhoneNumber, JobStartDate)
    VALUES ('Admin', 'User', 'admin@optik.com', 10000, 'MANAGER', '1985-01-15', '5551234567', '2018-01-01');

    -- Get the StaffID for credentials
    DECLARE @AdminID INT = SCOPE_IDENTITY();

    -- Add credentials (password: admin123)
    INSERT INTO StaffCredentials (StaffID, PasswordHash)
    VALUES (@AdminID, 'admin123');

    PRINT 'Admin user created successfully!';
    PRINT 'Email: admin@optik.com';
    PRINT 'Password: admin123';
END
ELSE
BEGIN
    PRINT 'Admin user already exists.';
END
GO

IF NOT EXISTS (SELECT 1 FROM Staff WHERE Email = 'staff@optik.com')
BEGIN
    -- Create Staff User (Employee position = Staff access only)
    INSERT INTO Staff (FirstName, LastName, Email, Salary, Position, DateOfBirth, PhoneNumber, JobStartDate)
    VALUES ('Test', 'Employee', 'staff@optik.com', 5000, 'STAFF', '1995-06-20', '5559876543', '2022-03-15');

    -- Get the StaffID for credentials
    DECLARE @StaffID INT = SCOPE_IDENTITY();

    -- Add credentials (password: staff123)
    INSERT INTO StaffCredentials (StaffID, PasswordHash)
    VALUES (@StaffID, 'staff123');

    PRINT 'Staff user created successfully!';
    PRINT 'Email: staff@optik.com';
    PRINT 'Password: staff123';
END
ELSE
BEGIN
    PRINT 'Staff user already exists.';
END
GO

-- Verify users
SELECT 
    s.StaffID,
    s.FirstName + ' ' + s.LastName AS FullName,
    s.Email,
    s.Position,
    CASE WHEN sc.StaffID IS NOT NULL THEN 'Yes' ELSE 'No' END AS HasCredentials
FROM Staff s
LEFT JOIN StaffCredentials sc ON s.StaffID = sc.StaffID;
GO

