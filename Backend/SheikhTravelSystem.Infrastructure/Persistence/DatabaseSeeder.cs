using Dapper;
using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Infrastructure.Persistence;

/// <summary>
/// Populates the database with baseline demo data the first time the API boots
/// against a fresh DB. Each table is seeded only when empty, so the seeder is
/// safe to run on every startup.
/// </summary>
public class DatabaseSeeder(
    IDbConnectionFactory dbFactory,
    IPasswordHasher passwordHasher,
    ILogger<DatabaseSeeder> logger) : IDatabaseSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        await EnsureSchemaEvolutionAsync(connection, cancellationToken);

        await SeedUsersAsync(connection, cancellationToken);
        await SeedCustomersAsync(connection, cancellationToken);
        await SeedVehiclesAsync(connection, cancellationToken);
        await SeedDriversAsync(connection, cancellationToken);
        await SeedRoutesAsync(connection, cancellationToken);
        await SeedBookingsAsync(connection, cancellationToken);
        await SeedPaymentsAsync(connection, cancellationToken);
        await SeedFuelLogsAsync(connection, cancellationToken);
        await SeedMaintenanceAsync(connection, cancellationToken);
        await SeedNotificationsAsync(connection, cancellationToken);
        await SeedVehicleTrackingAsync(connection, cancellationToken);
        await SeedAuditLogsAsync(connection, cancellationToken);
        await SeedDriverAllowanceRulesAsync(connection, cancellationToken);

        logger.LogInformation("Database seeding complete.");
    }

    /// <summary>
    /// Development helper: deletes every seeded row (FK-safe order) and re-seeds.
    /// </summary>
    public async Task ResetAndSeedAsync(CancellationToken cancellationToken = default)
    {
        using (var connection = dbFactory.CreateConnection())
        {
            // Delete children before parents to respect foreign keys.
            var deleteStatements = new[]
            {
                "DELETE FROM VehicleTracking",
                "DELETE FROM Payments",
                "DELETE FROM Bookings",
                "DELETE FROM FuelLogs",
                "DELETE FROM Maintenance",
                "DELETE FROM Notifications",
                "DELETE FROM AuditLogs",
                "DELETE FROM Customers",
                "DELETE FROM Vehicles",
                "DELETE FROM Drivers",
                "DELETE FROM Routes",
                // Keep admin login usable — only remove users the seeder added.
                "DELETE FROM Users WHERE Email IN ('dispatcher@sheikhtravel.com','driver@sheikhtravel.com','accountant@sheikhtravel.com')"
            };

            foreach (var sql in deleteStatements)
            {
                await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
            }

            // Reseed identity counters so IDs start at 1 again.
            var reseedStatements = new[]
            {
                "DBCC CHECKIDENT ('Customers', RESEED, 0)",
                "DBCC CHECKIDENT ('Vehicles', RESEED, 0)",
                "DBCC CHECKIDENT ('Drivers', RESEED, 0)",
                "DBCC CHECKIDENT ('Routes', RESEED, 0)",
                "DBCC CHECKIDENT ('Bookings', RESEED, 0)",
                "DBCC CHECKIDENT ('Payments', RESEED, 0)",
                "DBCC CHECKIDENT ('FuelLogs', RESEED, 0)",
                "DBCC CHECKIDENT ('Maintenance', RESEED, 0)",
                "DBCC CHECKIDENT ('Notifications', RESEED, 0)",
                "DBCC CHECKIDENT ('VehicleTracking', RESEED, 0)",
                "DBCC CHECKIDENT ('AuditLogs', RESEED, 0)"
            };

            foreach (var sql in reseedStatements)
            {
                try
                {
                    await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
                }
                catch (Exception ex)
                {
                    // DBCC CHECKIDENT requires permissions; swallow and continue — not fatal.
                    logger.LogWarning(ex, "Identity reseed skipped for: {Sql}", sql);
                }
            }
        }

        logger.LogInformation("Database wiped — reseeding.");
        await SeedAsync(cancellationToken);
    }

    // ---------------------------------------------------------------------
    // Users
    // ---------------------------------------------------------------------
    private async Task SeedUsersAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        // Default password for all seeded users
        const string defaultPassword = "Pass@123";

        // Admin may already exist from CreateDatabase.sql; insert only missing emails.
        var seedUsers = new[]
        {
            new { FullName = "System Admin",  Email = "admin@sheikhtravel.com",      Password = defaultPassword, Phone = "03001234567", Role = (int)UserRole.Admin },
            new { FullName = "Ops Dispatcher", Email = "dispatcher@sheikhtravel.com", Password = defaultPassword, Phone = "03011234567", Role = (int)UserRole.Dispatcher },
            new { FullName = "Field Driver",   Email = "driver@sheikhtravel.com",     Password = defaultPassword, Phone = "03021234567", Role = (int)UserRole.Driver },
            new { FullName = "Finance Team",   Email = "accountant@sheikhtravel.com", Password = defaultPassword, Phone = "03031234567", Role = (int)UserRole.Accountant }
        };

        foreach (var u in seedUsers)
        {
            var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Users WHERE Email = @Email) THEN 1 ELSE 0 END",
                new { u.Email },
                cancellationToken: ct));

            if (exists)
            {
                // Update existing admin password to ensure it matches the default
                if (u.Email == "admin@sheikhtravel.com")
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "UPDATE Users SET PasswordHash = @PasswordHash WHERE Email = @Email",
                        new { PasswordHash = passwordHasher.Hash(u.Password), u.Email },
                        cancellationToken: ct));
                    logger.LogInformation("Updated password for {Email}", u.Email);
                }
                continue;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                @"INSERT INTO Users (FullName, Email, PasswordHash, Phone, Role, IsActive, CreatedAt, CreatedBy, IsDeleted)
                  VALUES (@FullName, @Email, @PasswordHash, @Phone, @Role, 1, @CreatedAt, 'seeder', 0);",
                new
                {
                    u.FullName,
                    u.Email,
                    PasswordHash = passwordHasher.Hash(u.Password),
                    u.Phone,
                    u.Role,
                    CreatedAt = DateTime.UtcNow
                },
                cancellationToken: ct));

            logger.LogInformation("Seeded user {Email}", u.Email);
        }
    }

    // ---------------------------------------------------------------------
    // Customers
    // ---------------------------------------------------------------------
    private async Task SeedCustomersAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "Customers", ct)) return;

        var rows = new[]
        {
            new { FullName = "Ahmed Khan",     Phone = "03001111111", Email = "ahmed.khan@example.com",     Address = "House 12, F-7, Islamabad", CNIC = "3520112345671" },
            new { FullName = "Bilal Raza",     Phone = "03002222222", Email = "bilal.raza@example.com",     Address = "Street 4, DHA, Lahore",    CNIC = "3520112345672" },
            new { FullName = "Sara Malik",     Phone = "03003333333", Email = "sara.malik@example.com",     Address = "Block B, Gulshan, Karachi", CNIC = "3520112345673" },
            new { FullName = "Hina Ali",       Phone = "03004444444", Email = "hina.ali@example.com",       Address = "Bahria Town, Rawalpindi",   CNIC = "3520112345674" },
            new { FullName = "Usman Sheikh",   Phone = "03005555555", Email = "usman.sheikh@example.com",   Address = "Model Town, Lahore",        CNIC = "3520112345675" }
        };

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO Customers (FullName, Phone, Email, Address, CNIC, IsActive, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@FullName, @Phone, @Email, @Address, @CNIC, 1, @CreatedAt, 'seeder', 0);",
            rows.Select(r => new
            {
                r.FullName, r.Phone, r.Email, r.Address, r.CNIC,
                CreatedAt = DateTime.UtcNow
            }),
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} customers", rows.Length);
    }

    // ---------------------------------------------------------------------
    // Vehicles
    // ---------------------------------------------------------------------
    private async Task SeedVehiclesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "Vehicles", ct)) return;

        var rows = new[]
        {
            new { Name = "Toyota Hiace",  Reg = "LEA-1234", Model = "Hiace GL",     Year = 2022, Seats = 14, Avg = 8.5m,  Fuel = (int)FuelType.Diesel, Mileage = 42000m, Status = (int)VehicleStatus.Available },
            new { Name = "Toyota Corolla", Reg = "LEB-5678", Model = "Corolla Altis", Year = 2023, Seats = 4,  Avg = 14.0m, Fuel = (int)FuelType.Petrol, Mileage = 18000m, Status = (int)VehicleStatus.Available },
            new { Name = "Honda Civic",    Reg = "LEC-9012", Model = "Civic RS",     Year = 2023, Seats = 4,  Avg = 13.0m, Fuel = (int)FuelType.Petrol, Mileage = 22000m, Status = (int)VehicleStatus.Available },
            new { Name = "Suzuki APV",     Reg = "LED-3456", Model = "APV GLX",      Year = 2021, Seats = 8,  Avg = 10.0m, Fuel = (int)FuelType.CNG,    Mileage = 60000m, Status = (int)VehicleStatus.Maintenance }
        };

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO Vehicles (Name, RegistrationNumber, Model, [Year], SeatingCapacity, FuelAverage, FuelType, CurrentMileage, InsuranceExpiryDate, Status, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@Name, @Reg, @Model, @Year, @Seats, @Avg, @Fuel, @Mileage, @InsuranceExpiryDate, @Status, @CreatedAt, 'seeder', 0);",
            rows.Select(r => new
            {
                r.Name, r.Reg, r.Model, r.Year, r.Seats, r.Avg, r.Fuel, r.Mileage,
                InsuranceExpiryDate = DateTime.UtcNow.AddMonths(9),
                r.Status,
                CreatedAt = DateTime.UtcNow
            }),
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} vehicles", rows.Length);
    }

    // ---------------------------------------------------------------------
    // Drivers
    // ---------------------------------------------------------------------
    private async Task SeedDriversAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "Drivers", ct)) return;

        var rows = new[]
        {
            new { FullName = "Imran Yousaf",  Phone = "03101112222", License = "LHR-D-10001", CNIC = "3520287654321", Address = "Shalimar, Lahore",      Status = (int)DriverStatus.Available },
            new { FullName = "Kashif Butt",   Phone = "03202223333", License = "LHR-D-10002", CNIC = "3520287654322", Address = "Township, Lahore",      Status = (int)DriverStatus.Available },
            new { FullName = "Rashid Hussain", Phone = "03303334444", License = "ISB-D-20003", CNIC = "3520287654323", Address = "G-11, Islamabad",      Status = (int)DriverStatus.OffDuty },
            new { FullName = "Farhan Tariq",   Phone = "03404445555", License = "KHI-D-30004", CNIC = "3520287654324", Address = "North Nazimabad, KHI", Status = (int)DriverStatus.Available }
        };

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO Drivers (FullName, Phone, LicenseNumber, LicenseExpiryDate, CNIC, [Address], Status, IsActive, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@FullName, @Phone, @License, @LicenseExpiryDate, @CNIC, @Address, @Status, 1, @CreatedAt, 'seeder', 0);",
            rows.Select(r => new
            {
                r.FullName, r.Phone, r.License,
                LicenseExpiryDate = DateTime.UtcNow.AddYears(2),
                r.CNIC, r.Address, r.Status,
                CreatedAt = DateTime.UtcNow
            }),
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} drivers", rows.Length);
    }

    // ---------------------------------------------------------------------
    // Schema evolution — idempotent ALTER statements for existing databases.
    // Safe to run on every startup: each step checks COL_LENGTH before acting.
    // ---------------------------------------------------------------------
    private async Task EnsureSchemaEvolutionAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        var statements = new[]
        {
            "IF COL_LENGTH('Routes', 'Name') IS NULL ALTER TABLE Routes ADD Name NVARCHAR(200) NULL;",
            "IF COL_LENGTH('Routes', 'EstimatedMinutes') IS NULL ALTER TABLE Routes ADD EstimatedMinutes INT NULL;",
            "IF COL_LENGTH('Vehicles', 'Model') IS NULL ALTER TABLE Vehicles ADD Model NVARCHAR(100) NULL;",
            "IF COL_LENGTH('Vehicles', 'Year') IS NULL ALTER TABLE Vehicles ADD [Year] INT NULL;",
            "IF COL_LENGTH('Bookings', 'CancellationReason') IS NULL ALTER TABLE Bookings ADD CancellationReason NVARCHAR(500) NULL;",
            @"IF OBJECT_ID('DriverAllowanceRules', 'U') IS NULL
              CREATE TABLE DriverAllowanceRules (
                Id              INT IDENTITY(1,1) PRIMARY KEY,
                Name            NVARCHAR(150)   NOT NULL,
                CalculationType INT             NOT NULL,
                Value           DECIMAL(18, 4)  NOT NULL,
                Priority        INT             NOT NULL DEFAULT 100,
                MinDistanceKm   DECIMAL(18, 2)  NULL,
                MaxDistanceKm   DECIMAL(18, 2)  NULL,
                VehicleFuelType INT             NULL,
                RouteFilter     NVARCHAR(200)   NULL,
                IsActive        BIT             NOT NULL DEFAULT 1,
                Notes           NVARCHAR(500)   NULL,
                CreatedAt       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
                UpdatedAt       DATETIME2       NULL,
                CreatedBy       NVARCHAR(100)   NULL,
                UpdatedBy       NVARCHAR(100)   NULL,
                IsDeleted       BIT             NOT NULL DEFAULT 0
              );"
        };

        foreach (var sql in statements)
        {
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Schema evolution step failed: {Sql}", sql);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Routes
    // ---------------------------------------------------------------------
    private async Task SeedRoutesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "Routes", ct)) return;

        var rows = new[]
        {
            new { Name = "Lahore \u2013 Islamabad Express",  Source = "Lahore",    Destination = "Islamabad", Distance = 375m,  EstimatedMinutes = 270, BasePrice = 6500m },
            new { Name = "Lahore \u2013 Karachi Overnight", Source = "Lahore",    Destination = "Karachi",   Distance = 1200m, EstimatedMinutes = 900, BasePrice = 18000m },
            new { Name = "Islamabad \u2013 Peshawar",       Source = "Islamabad", Destination = "Peshawar",  Distance = 190m,  EstimatedMinutes = 150, BasePrice = 4000m },
            new { Name = "Karachi \u2013 Hyderabad",        Source = "Karachi",   Destination = "Hyderabad", Distance = 165m,  EstimatedMinutes = 140, BasePrice = 3500m },
            new { Name = "Lahore \u2013 Multan",            Source = "Lahore",    Destination = "Multan",    Distance = 340m,  EstimatedMinutes = 240, BasePrice = 6000m },
            new { Name = "Islamabad \u2013 Murree",         Source = "Islamabad", Destination = "Murree",    Distance = 65m,   EstimatedMinutes = 90,  BasePrice = 2500m }
        };

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO Routes (Name, Source, Destination, Distance, EstimatedMinutes, BasePrice, IsActive, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@Name, @Source, @Destination, @Distance, @EstimatedMinutes, @BasePrice, 1, @CreatedAt, 'seeder', 0);",
            rows.Select(r => new
            {
                r.Name, r.Source, r.Destination, r.Distance, r.EstimatedMinutes, r.BasePrice,
                CreatedAt = DateTime.UtcNow
            }),
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} routes", rows.Length);
    }

    // ---------------------------------------------------------------------
    // Bookings
    // ---------------------------------------------------------------------
    private async Task SeedBookingsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "Bookings", ct)) return;

        // Rely on the IDs generated by the previous seed steps. We look them up
        // dynamically so the seeder still works if prior data existed.
        var customerIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT TOP 5 Id FROM Customers WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();
        var routeIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT TOP 5 Id FROM Routes WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();
        var vehicleIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT TOP 5 Id FROM Vehicles WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();
        var driverIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT TOP 5 Id FROM Drivers WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();

        if (customerIds.Count == 0 || routeIds.Count == 0)
        {
            logger.LogWarning("Skipping booking seed – required customers/routes missing.");
            return;
        }

        var now = DateTime.UtcNow;
        var rows = new[]
        {
            new { CustomerId = customerIds[0], RouteId = routeIds[0], VehicleId = (int?)vehicleIds.ElementAtOrDefault(0), DriverId = (int?)driverIds.ElementAtOrDefault(0), Pickup = now.AddDays(1).AddHours(2),  Passengers = 2, Amount = 7500m,  Status = (int)BookingStatus.Confirmed, Notes = "Airport pickup, bring card machine." },
            new { CustomerId = customerIds[1 % customerIds.Count], RouteId = routeIds[1 % routeIds.Count], VehicleId = (int?)vehicleIds.ElementAtOrDefault(1), DriverId = (int?)driverIds.ElementAtOrDefault(1), Pickup = now.AddDays(3),              Passengers = 4, Amount = 19500m, Status = (int)BookingStatus.Pending,   Notes = "Family trip, extra luggage." },
            new { CustomerId = customerIds[2 % customerIds.Count], RouteId = routeIds[2 % routeIds.Count], VehicleId = (int?)vehicleIds.ElementAtOrDefault(2), DriverId = (int?)driverIds.ElementAtOrDefault(2), Pickup = now.AddDays(-2),             Passengers = 1, Amount = 4200m,  Status = (int)BookingStatus.Completed, Notes = "Business meeting." },
            new { CustomerId = customerIds[3 % customerIds.Count], RouteId = routeIds[3 % routeIds.Count], VehicleId = (int?)vehicleIds.ElementAtOrDefault(0), DriverId = (int?)driverIds.ElementAtOrDefault(3), Pickup = now.AddDays(5),              Passengers = 6, Amount = 4000m,  Status = (int)BookingStatus.Confirmed, Notes = "Wedding event transport." },
            new { CustomerId = customerIds[4 % customerIds.Count], RouteId = routeIds[4 % routeIds.Count], VehicleId = (int?)vehicleIds.ElementAtOrDefault(1), DriverId = (int?)driverIds.ElementAtOrDefault(0), Pickup = now.AddDays(-1),             Passengers = 2, Amount = 6200m,  Status = (int)BookingStatus.Cancelled, Notes = "Customer cancelled." }
        };

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO Bookings (CustomerId, RouteId, VehicleId, DriverId, PickupTime, PassengerCount, TotalAmount, Status, Notes, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@CustomerId, @RouteId, @VehicleId, @DriverId, @Pickup, @Passengers, @Amount, @Status, @Notes, @CreatedAt, 'seeder', 0);",
            rows.Select(r => new
            {
                r.CustomerId, r.RouteId, r.VehicleId, r.DriverId,
                r.Pickup, r.Passengers, r.Amount, r.Status, r.Notes,
                CreatedAt = DateTime.UtcNow
            }),
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} bookings", rows.Length);
    }

    // ---------------------------------------------------------------------
    // Payments
    // ---------------------------------------------------------------------
    private async Task SeedPaymentsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "Payments", ct)) return;

        var bookings = (await connection.QueryAsync<BookingSummary>(new CommandDefinition(
            "SELECT Id, TotalAmount, Status FROM Bookings WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();

        if (bookings.Count == 0) return;

        var payments = new List<object>();
        foreach (var b in bookings)
        {
            var status = b.Status switch
            {
                (int)BookingStatus.Completed => (int)PaymentStatus.Paid,
                (int)BookingStatus.Confirmed => (int)PaymentStatus.PartiallyPaid,
                (int)BookingStatus.Cancelled => (int)PaymentStatus.Refunded,
                _                            => (int)PaymentStatus.Pending
            };

            var amount = status switch
            {
                (int)PaymentStatus.Paid          => b.TotalAmount,
                (int)PaymentStatus.PartiallyPaid => Math.Round(b.TotalAmount / 2m, 2),
                (int)PaymentStatus.Refunded      => b.TotalAmount,
                _                                => 0m
            };

            payments.Add(new
            {
                BookingId = b.Id,
                Amount = amount,
                PaymentMethod = status == (int)PaymentStatus.Paid ? "Cash" : "Card",
                Status = status,
                PaymentDate = DateTime.UtcNow.AddDays(-1),
                TransactionReference = $"TXN-{b.Id:D6}",
                Notes = status == (int)PaymentStatus.Refunded ? "Refund issued." : null,
                CreatedAt = DateTime.UtcNow
            });
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"IF COL_LENGTH('Payments', 'ReceiptImageData') IS NOT NULL
              BEGIN
                INSERT INTO Payments (BookingId, Amount, PaymentMethod, Status, PaymentDate, TransactionReference, Notes, ReceiptImageData, CreatedAt, CreatedBy, IsDeleted)
                VALUES (@BookingId, @Amount, @PaymentMethod, @Status, @PaymentDate, @TransactionReference, @Notes, NULL, @CreatedAt, 'seeder', 0);
              END
              ELSE
              BEGIN
                INSERT INTO Payments (BookingId, Amount, PaymentMethod, Status, PaymentDate, TransactionReference, Notes, CreatedAt, CreatedBy, IsDeleted)
                VALUES (@BookingId, @Amount, @PaymentMethod, @Status, @PaymentDate, @TransactionReference, @Notes, @CreatedAt, 'seeder', 0);
              END",
            payments,
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} payments", payments.Count);
    }

    // ---------------------------------------------------------------------
    // FuelLogs
    // ---------------------------------------------------------------------
    private async Task SeedFuelLogsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "FuelLogs", ct)) return;

        var vehicleIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Vehicles WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();
        var driverIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Drivers WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();

        if (vehicleIds.Count == 0) return;

        var rows = new List<object>();
        for (int i = 0; i < vehicleIds.Count; i++)
        {
            var vehicleId = vehicleIds[i];
            var driverId = driverIds.Count > 0 ? (int?)driverIds[i % driverIds.Count] : null;
            var liters = 35m + i * 5;
            var ppl = 280m + i;
            rows.Add(new
            {
                VehicleId = vehicleId,
                DriverId = driverId,
                Liters = liters,
                PricePerLiter = ppl,
                TotalCost = Math.Round(liters * ppl, 2),
                OdometerReading = 42000m + i * 500,
                FuelType = (int)FuelType.Diesel,
                FuelDate = DateTime.UtcNow.AddDays(-i - 1),
                Station = "PSO Shell Main",
                CreatedAt = DateTime.UtcNow
            });
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO FuelLogs (VehicleId, DriverId, Liters, PricePerLiter, TotalCost, OdometerReading, FuelType, FuelDate, Station, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@VehicleId, @DriverId, @Liters, @PricePerLiter, @TotalCost, @OdometerReading, @FuelType, @FuelDate, @Station, @CreatedAt, 'seeder', 0);",
            rows,
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} fuel logs", rows.Count);
    }

    // ---------------------------------------------------------------------
    // Maintenance
    // ---------------------------------------------------------------------
    private async Task SeedMaintenanceAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "Maintenance", ct)) return;

        var vehicleIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Vehicles WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();
        if (vehicleIds.Count == 0) return;

        var rows = vehicleIds.Select((vid, idx) => new
        {
            VehicleId = vid,
            Description = idx % 2 == 0 ? "Oil change and filter replacement" : "Brake pad inspection",
            Cost = idx % 2 == 0 ? 8500m : 4200m,
            MaintenanceDate = DateTime.UtcNow.AddDays(-(idx + 1) * 10),
            NextDueDate = (DateTime?)DateTime.UtcNow.AddMonths(3),
            Status = (int)MaintenanceStatus.Completed,
            ServiceProvider = "Toyota Service Center",
            CreatedAt = DateTime.UtcNow
        }).ToList<object>();

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO Maintenance (VehicleId, Description, Cost, MaintenanceDate, NextDueDate, Status, ServiceProvider, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@VehicleId, @Description, @Cost, @MaintenanceDate, @NextDueDate, @Status, @ServiceProvider, @CreatedAt, 'seeder', 0);",
            rows,
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} maintenance records", rows.Count);
    }

    // ---------------------------------------------------------------------
    // Notifications
    // ---------------------------------------------------------------------
    private async Task SeedNotificationsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "Notifications", ct)) return;

        var adminId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 Id FROM Users WHERE Email = 'admin@sheikhtravel.com'", cancellationToken: ct));

        var rows = new[]
        {
            new { UserId = adminId, Title = "Welcome to Sheikh Travel", Message = "Seed data has been loaded. You can start exploring the system.", Type = (int)NotificationType.BookingCreated, ReferenceId = (int?)null },
            new { UserId = adminId, Title = "Pending bookings",          Message = "There are pending bookings awaiting confirmation.",               Type = (int)NotificationType.BookingCreated, ReferenceId = (int?)null },
            new { UserId = (int?)null, Title = "System notice",          Message = "This is a global notification visible to all users.",             Type = (int)NotificationType.VehicleOffline, ReferenceId = (int?)null }
        };

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO Notifications (UserId, Title, [Message], Type, IsRead, ReferenceId, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@UserId, @Title, @Message, @Type, 0, @ReferenceId, @CreatedAt, 'seeder', 0);",
            rows.Select(r => new
            {
                r.UserId, r.Title, r.Message, r.Type, r.ReferenceId,
                CreatedAt = DateTime.UtcNow
            }),
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} notifications", rows.Length);
    }

    // ---------------------------------------------------------------------
    // VehicleTracking
    // ---------------------------------------------------------------------
    private async Task SeedVehicleTrackingAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "VehicleTracking", ct)) return;

        var vehicleIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Vehicles WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();
        var driverIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Drivers WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();
        var bookingIds = (await connection.QueryAsync<int>(new CommandDefinition(
            "SELECT Id FROM Bookings WHERE IsDeleted = 0 ORDER BY Id", cancellationToken: ct))).ToList();

        if (vehicleIds.Count == 0) return;

        // A small trail of recent GPS pings around Lahore/Islamabad for each vehicle.
        var waypoints = new (double Lat, double Lng, decimal Speed)[]
        {
            (31.5204, 74.3587, 45m),  // Lahore
            (32.0836, 72.6711, 80m),  // Sargodha
            (33.6844, 73.0479, 60m),  // Islamabad
            (33.7294, 73.0931, 20m)   // F-7 Islamabad
        };

        var rows = new List<object>();
        var baseTime = DateTime.UtcNow.AddMinutes(-waypoints.Length * vehicleIds.Count);

        for (int v = 0; v < vehicleIds.Count; v++)
        {
            for (int w = 0; w < waypoints.Length; w++)
            {
                var wp = waypoints[w];
                rows.Add(new
                {
                    VehicleId = vehicleIds[v],
                    DriverId = driverIds.Count > 0 ? (int?)driverIds[v % driverIds.Count] : null,
                    BookingId = bookingIds.Count > 0 ? (int?)bookingIds[v % bookingIds.Count] : null,
                    Latitude = wp.Lat,
                    Longitude = wp.Lng,
                    Speed = wp.Speed,
                    Timestamp = baseTime.AddMinutes((v * waypoints.Length) + w),
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO VehicleTracking (VehicleId, DriverId, BookingId, Latitude, Longitude, Speed, [Timestamp], CreatedAt, CreatedBy, IsDeleted)
              VALUES (@VehicleId, @DriverId, @BookingId, @Latitude, @Longitude, @Speed, @Timestamp, @CreatedAt, 'seeder', 0);",
            rows,
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} vehicle tracking points", rows.Count);
    }

    // ---------------------------------------------------------------------
    // AuditLogs
    // ---------------------------------------------------------------------
    private async Task SeedAuditLogsAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "AuditLogs", ct)) return;

        var rows = new[]
        {
            new { Action = "Create", EntityName = "Customer", EntityId = (int?)1, OldValues = (string?)null, NewValues = "{\"FullName\":\"Ahmed Khan\"}",  UserId = "seeder", IpAddress = "127.0.0.1" },
            new { Action = "Create", EntityName = "Vehicle",  EntityId = (int?)1, OldValues = (string?)null, NewValues = "{\"Name\":\"Toyota Hiace\"}",    UserId = "seeder", IpAddress = "127.0.0.1" },
            new { Action = "Create", EntityName = "Booking",  EntityId = (int?)1, OldValues = (string?)null, NewValues = "{\"Status\":2}",                 UserId = "seeder", IpAddress = "127.0.0.1" },
            new { Action = "Update", EntityName = "Booking",  EntityId = (int?)3, OldValues = (string?)"{\"Status\":2}", NewValues = "{\"Status\":4}",      UserId = "seeder", IpAddress = "127.0.0.1" }
        };

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO AuditLogs ([Action], EntityName, EntityId, OldValues, NewValues, UserId, IpAddress, CreatedAt, CreatedBy, IsDeleted)
              VALUES (@Action, @EntityName, @EntityId, @OldValues, @NewValues, @UserId, @IpAddress, @CreatedAt, 'seeder', 0);",
            rows.Select(r => new
            {
                r.Action, r.EntityName, r.EntityId, r.OldValues, r.NewValues, r.UserId, r.IpAddress,
                CreatedAt = DateTime.UtcNow
            }),
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} audit log entries", rows.Length);
    }

    // ---------------------------------------------------------------------
    // Driver Allowance Rules
    // ---------------------------------------------------------------------
    private async Task SeedDriverAllowanceRulesAsync(System.Data.IDbConnection connection, CancellationToken ct)
    {
        if (await TableHasRowsAsync(connection, "DriverAllowanceRules", ct)) return;

        // CalculationType ids must match Domain.Enums.AllowanceCalculationType.
        var rows = new[]
        {
            new { Name = "Long-haul profit share (≥ 500km)", CalculationType = 4, Value = 35m,   Priority = 10,
                  MinDistanceKm = (decimal?)500m, MaxDistanceKm = (decimal?)null,
                  VehicleFuelType = (int?)null,  RouteFilter = (string?)null,
                  Notes = "35% of trip profit for long-haul routes." },

            new { Name = "Standard per-km rate",             CalculationType = 2, Value = 15m,   Priority = 50,
                  MinDistanceKm = (decimal?)null, MaxDistanceKm = (decimal?)null,
                  VehicleFuelType = (int?)null,  RouteFilter = (string?)null,
                  Notes = "PKR 15/km for any route." },

            new { Name = "Per-day overnight allowance",      CalculationType = 3, Value = 2500m, Priority = 80,
                  MinDistanceKm = (decimal?)null, MaxDistanceKm = (decimal?)null,
                  VehicleFuelType = (int?)null,  RouteFilter = (string?)null,
                  Notes = "PKR 2,500 per trip day." },

            new { Name = "Fallback fixed allowance",         CalculationType = 1, Value = 1500m, Priority = 1000,
                  MinDistanceKm = (decimal?)null, MaxDistanceKm = (decimal?)null,
                  VehicleFuelType = (int?)null,  RouteFilter = (string?)null,
                  Notes = "PKR 1,500 fixed — applies when no other rule matches." }
        };

        await connection.ExecuteAsync(new CommandDefinition(
            @"INSERT INTO DriverAllowanceRules
                (Name, CalculationType, Value, Priority, MinDistanceKm, MaxDistanceKm,
                 VehicleFuelType, RouteFilter, IsActive, Notes, CreatedAt, CreatedBy, IsDeleted)
              VALUES
                (@Name, @CalculationType, @Value, @Priority, @MinDistanceKm, @MaxDistanceKm,
                 @VehicleFuelType, @RouteFilter, 1, @Notes, @CreatedAt, 'seeder', 0);",
            rows.Select(r => new
            {
                r.Name, r.CalculationType, r.Value, r.Priority,
                r.MinDistanceKm, r.MaxDistanceKm, r.VehicleFuelType, r.RouteFilter, r.Notes,
                CreatedAt = DateTime.UtcNow
            }),
            cancellationToken: ct));

        logger.LogInformation("Seeded {Count} driver allowance rules", rows.Length);
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------
    private static async Task<bool> TableHasRowsAsync(System.Data.IDbConnection connection, string tableName, CancellationToken ct)
    {
        var sql = $"SELECT CASE WHEN EXISTS(SELECT 1 FROM [{tableName}]) THEN 1 ELSE 0 END";
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, cancellationToken: ct));
    }

    private sealed class BookingSummary
    {
        public int Id { get; set; }
        public decimal TotalAmount { get; set; }
        public int Status { get; set; }
    }
}
