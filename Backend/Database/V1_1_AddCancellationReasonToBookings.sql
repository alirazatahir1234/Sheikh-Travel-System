IF COL_LENGTH('Bookings', 'CancellationReason') IS NULL
BEGIN
    ALTER TABLE Bookings
    ADD CancellationReason NVARCHAR(500) NULL;
END;
