-- Optional identity fields for CNIC / OCR capture (nullable for existing rows).
IF COL_LENGTH('dbo.Customers', 'FatherOrHusbandName') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD FatherOrHusbandName NVARCHAR(200) NULL;
END
IF COL_LENGTH('dbo.Customers', 'Gender') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD Gender NVARCHAR(20) NULL;
END
IF COL_LENGTH('dbo.Customers', 'DateOfBirth') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD DateOfBirth DATE NULL;
END
IF COL_LENGTH('dbo.Customers', 'Nationality') IS NULL
BEGIN
    ALTER TABLE dbo.Customers ADD Nationality NVARCHAR(120) NULL;
END
GO
