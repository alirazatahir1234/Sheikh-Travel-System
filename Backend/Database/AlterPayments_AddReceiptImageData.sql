-- Run once on existing databases that already have the Payments table.
IF COL_LENGTH('dbo.Payments', 'ReceiptImageData') IS NULL
BEGIN
    ALTER TABLE dbo.Payments ADD ReceiptImageData NVARCHAR(MAX) NULL;
END
GO
