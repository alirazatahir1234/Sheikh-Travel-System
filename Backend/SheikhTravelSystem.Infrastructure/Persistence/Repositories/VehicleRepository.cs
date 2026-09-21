using Dapper;
using Microsoft.Data.SqlClient;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Infrastructure.Persistence;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers;
using SheikhTravelSystem.Application.Features.Vehicles;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Application.Features.Vehicles.Queries;
using SheikhTravelSystem.Domain.Enums;
using SheikhTravelSystem.Infrastructure.Persistence.Repositories.Sql;

namespace SheikhTravelSystem.Infrastructure.Persistence.Repositories;

public sealed class VehicleRepository(IDbConnectionFactory dbFactory) : IVehicleRepository
{
    public async Task<(IReadOnlyList<VehicleListItemDto> Items, int TotalCount)> GetPagedAsync(
        int tenantId, int page, int pageSize, bool includeDrafts, DataScopeResult? scope,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var clauses = new List<string> { "v.IsDeleted = 0", "v.TenantId = @TenantId" };
        if (!includeDrafts)
            clauses.Add("v.Status <> 5");

        var parameters = new DynamicParameters(new
        {
            Offset = offset,
            PageSize = pageSize,
            TenantId = tenantId
        });

        if (scope is not null)
            DataScopeSqlBuilder.ApplyVehicleScope(parameters, scope, "v", clauses);

        var whereClause = string.Join(" AND ", clauses);

        var vehicles = (await connection.QueryAsync<VehicleListItemDto>(
            new CommandDefinition(
                $@"SELECT {VehicleSql.ListSelect}
                  {VehicleSql.ListFrom}
                  WHERE {whereClause}
                  ORDER BY v.CreatedAt DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                parameters,
                cancellationToken: cancellationToken))).ToList();

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                $@"SELECT COUNT(*) FROM Vehicles v WHERE {whereClause}",
                parameters,
                cancellationToken: cancellationToken));

        return (vehicles, totalCount);
    }

    public async Task<VehicleDto?> GetByIdAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<VehicleDto>(
            new CommandDefinition(
                $@"SELECT {VehicleSql.DetailColumns}
                  {VehicleSql.DetailFrom}
                  WHERE v.Id = @Id AND v.TenantId = @TenantId AND v.IsDeleted = 0",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task EnsureExistsAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        var exists = await ExistsOn(connection, id, tenantId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Vehicle", id);
    }

    public async Task<int> CreateAsync(
        int tenantId, CreateVehicleDto dto, bool saveAsDraft, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var registration = string.IsNullOrWhiteSpace(dto.RegistrationNumber)
            ? $"DRAFT-{tenantId}-{DateTime.UtcNow.Ticks}"
            : dto.RegistrationNumber.Trim();

        if (!saveAsDraft || !registration.StartsWith("DRAFT-", StringComparison.Ordinal))
        {
            var exists = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE RegistrationNumber = @Reg AND IsDeleted = 0 AND TenantId = @TenantId) THEN 1 ELSE 0 END",
                    new { Reg = registration, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            if (exists)
                throw new ConflictException($"Vehicle with registration '{registration}' already exists.");
        }

        var status = saveAsDraft ? VehicleStatus.Draft : VehicleStatus.Available;
        var name = string.IsNullOrWhiteSpace(dto.Name) ? "Draft Vehicle" : dto.Name.Trim();
        var seating = dto.SeatingCapacity > 0 ? dto.SeatingCapacity : 1;
        var fuelAverage = dto.FuelAverage > 0 ? dto.FuelAverage : 1m;

        try
        {
            return await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    @"INSERT INTO Vehicles (TenantId, Name, RegistrationNumber, VehicleCode, VIN, Make, Model, Year,
                      Color, VehicleType, SeatingCapacity, FuelAverage, FuelType, EngineNo, ChassisNo,
                      CurrentMileage, InsuranceExpiryDate, PurchaseDate, PurchasePrice, PurchaseCurrencyCode, BranchId, DepartmentId,
                      Status, CreatedAt, IsDeleted)
                      VALUES (@TenantId, @Name, @RegistrationNumber, @VehicleCode, @VIN, @Make, @Model, @Year,
                      @Color, @VehicleType, @SeatingCapacity, @FuelAverage, @FuelType, @EngineNo, @ChassisNo,
                      @CurrentMileage, @InsuranceExpiryDate, @PurchaseDate, @PurchasePrice, @PurchaseCurrencyCode, @BranchId, @DepartmentId,
                      @Status, @CreatedAt, 0);
                      SELECT SCOPE_IDENTITY();",
                    new
                    {
                        TenantId = tenantId,
                        Name = name,
                        RegistrationNumber = registration,
                        dto.VehicleCode, dto.VIN, dto.Make, dto.Model, dto.Year,
                        dto.Color, dto.VehicleType,
                        SeatingCapacity = seating,
                        FuelAverage = fuelAverage,
                        FuelType = (int)dto.FuelType, dto.EngineNo, dto.ChassisNo,
                        dto.CurrentMileage, dto.InsuranceExpiryDate, dto.PurchaseDate, dto.PurchasePrice,
                        PurchaseCurrencyCode = NormalizeCurrencyCode(dto.PurchaseCurrencyCode),
                        dto.BranchId, dto.DepartmentId,
                        Status = (int)status,
                        CreatedAt = DateTime.UtcNow
                    },
                    cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601
            && ex.Message.Contains("UQ_Vehicles_Registration", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Vehicle with registration '{registration}' already exists.");
        }
    }

    public async Task UpdateAsync(
        int id, int tenantId, UpdateVehicleDto dto, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await ExistsOn(connection, id, tenantId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Vehicle", id);

        var registration = string.IsNullOrWhiteSpace(dto.RegistrationNumber)
            ? null
            : dto.RegistrationNumber.Trim();

        if (!string.IsNullOrWhiteSpace(registration))
        {
            var regConflict = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM Vehicles
                        WHERE RegistrationNumber = @Reg AND Id != @Id AND TenantId = @TenantId AND IsDeleted = 0
                      ) THEN 1 ELSE 0 END",
                    new { Reg = registration, Id = id, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            if (regConflict)
                throw new ConflictException($"Registration '{registration}' is already in use.");
        }

        if (!string.IsNullOrWhiteSpace(dto.EngineNo))
        {
            var engineDup = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Vehicles
                    WHERE TenantId = @TenantId AND IsDeleted = 0 AND Id != @Id
                      AND EngineNo IS NOT NULL AND LOWER(LTRIM(RTRIM(EngineNo))) = LOWER(LTRIM(RTRIM(@EngineNo)))
                  ) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId, EngineNo = dto.EngineNo },
                cancellationToken: cancellationToken));
            if (engineDup)
                throw new ConflictException("Vehicle already registered with this engine number.");
        }

        if (!string.IsNullOrWhiteSpace(dto.ChassisNo))
        {
            var chassisDup = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Vehicles
                    WHERE TenantId = @TenantId AND IsDeleted = 0 AND Id != @Id
                      AND ChassisNo IS NOT NULL AND LOWER(LTRIM(RTRIM(ChassisNo))) = LOWER(LTRIM(RTRIM(@ChassisNo)))
                  ) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId, ChassisNo = dto.ChassisNo },
                cancellationToken: cancellationToken));
            if (chassisDup)
                throw new ConflictException("Vehicle already registered with this chassis number.");
        }

        int rows;
        try
        {
            rows = await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Vehicles SET Name = @Name,
                      RegistrationNumber = COALESCE(@RegistrationNumber, RegistrationNumber),
                      VehicleCode = @VehicleCode, VIN = @VIN, Make = @Make, Model = @Model, Year = @Year,
                      Color = @Color, VehicleType = @VehicleType,
                      SeatingCapacity = @SeatingCapacity, FuelAverage = @FuelAverage, FuelType = @FuelType,
                      EngineNo = @EngineNo, ChassisNo = @ChassisNo,
                      CurrentMileage = @CurrentMileage, InsuranceExpiryDate = @InsuranceExpiryDate,
                      PurchaseDate = @PurchaseDate, PurchasePrice = @PurchasePrice,
                      PurchaseCurrencyCode = @PurchaseCurrencyCode,
                      BranchId = @BranchId, DepartmentId = @DepartmentId, Status = @Status,
                      UpdatedAt = @UpdatedAt
                      WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                    new
                    {
                        dto.Name,
                        RegistrationNumber = registration,
                        dto.VehicleCode, dto.VIN, dto.Make, dto.Model, dto.Year,
                        dto.Color, dto.VehicleType, dto.SeatingCapacity, dto.FuelAverage,
                        FuelType = (int)dto.FuelType, dto.EngineNo, dto.ChassisNo,
                        dto.CurrentMileage, dto.InsuranceExpiryDate, dto.PurchaseDate, dto.PurchasePrice,
                        PurchaseCurrencyCode = string.IsNullOrWhiteSpace(dto.PurchaseCurrencyCode)
                            ? null
                            : dto.PurchaseCurrencyCode.Trim().ToUpperInvariant(),
                        dto.BranchId, dto.DepartmentId, Status = (int)dto.Status,
                        UpdatedAt = DateTime.UtcNow, Id = id, TenantId = tenantId
                    },
                    cancellationToken: cancellationToken));
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601
            && ex.Message.Contains("UQ_Vehicles_Registration", StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException($"Registration '{dto.RegistrationNumber}' is already in use.");
        }

        if (rows == 0)
            throw new NotFoundException("Vehicle", id);
    }

    public async Task<IReadOnlyList<string>> SoftDeleteAsync(
        int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await ExistsOn(connection, id, tenantId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Vehicle", id);

        var fileUrls = (await connection.QueryAsync<string>(
            new CommandDefinition(
                @"SELECT FileUrl FROM VehicleDocuments
                  WHERE VehicleId = @Id AND TenantId = @TenantId
                    AND IsDeleted = 0 AND FileUrl IS NOT NULL AND FileUrl <> ''",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken))).ToList();

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE VehicleDocuments SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE VehicleId = @Id AND TenantId = @TenantId",
                new { UpdatedAt = DateTime.UtcNow, Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET IsDeleted = 1, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { UpdatedAt = DateTime.UtcNow, Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return fileUrls;
    }

    public async Task<VehicleStatus> ToggleStatusAsync(
        int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var currentStatus = await connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(
                "SELECT Status FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (currentStatus == null)
            throw new NotFoundException("Vehicle", id);

        var newStatus = (VehicleStatus)currentStatus == VehicleStatus.Available
            ? VehicleStatus.Retired
            : VehicleStatus.Available;

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { Id = id, Status = (int)newStatus, UpdatedAt = DateTime.UtcNow, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return newStatus;
    }

    public async Task ChangeStatusAsync(
        int id, int tenantId, VehicleStatus status, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await ExistsOn(connection, id, tenantId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Vehicle", id);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { Status = (int)status, UpdatedAt = DateTime.UtcNow, Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<int> AssignDriverAsync(
        int vehicleId, int tenantId, AssignVehicleDriverRequest body, string createdBy,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var vehicleExists = await ExistsOn(connection, vehicleId, tenantId, cancellationToken);
        if (!vehicleExists)
            throw new NotFoundException("Vehicle", vehicleId);

        var driverExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Drivers WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { body.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!driverExists)
            throw new NotFoundException("Driver", body.DriverId);

        var driverRow = await connection.QuerySingleAsync<(bool IsActive, int Status, string VerificationStatus, DateTime LicenseExpiry)>(
            new CommandDefinition(
                "SELECT IsActive, Status, VerificationStatus, LicenseExpiryDate FROM Drivers WHERE Id = @DriverId AND TenantId = @TenantId AND IsDeleted = 0",
                new { body.DriverId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        DriverAssignmentGuard.EnsureAssignableForVehicleTrip(
            driverRow.IsActive,
            (DriverStatus)driverRow.Status,
            driverRow.VerificationStatus,
            driverRow.LicenseExpiry);

        await DriverAssignmentOps.EnsureDriverNotOnActiveTripAsync(
            connection, tenantId, body.DriverId, cancellationToken);

        await DriverAssignmentOps.CompleteActiveAssignmentsAsync(
            connection, tenantId, body.DriverId, vehicleId, transaction: null, cancellationToken);

        if (body.BookingId is int bookingId)
        {
            var bookingStatus = await connection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    "SELECT Status FROM Bookings WHERE Id = @BookingId AND TenantId = @TenantId AND IsDeleted = 0",
                    new { BookingId = bookingId, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            if (bookingStatus is null)
                throw new NotFoundException("Booking", bookingId);

            if (bookingStatus != (int)BookingStatus.Pending && bookingStatus != (int)BookingStatus.Confirmed)
                throw new ConflictException("Can only assign driver to pending or confirmed bookings.");

            await connection.ExecuteAsync(
                new CommandDefinition(
                    @"UPDATE Bookings SET DriverId = @DriverId, VehicleId = @VehicleId, UpdatedAt = @UpdatedAt
                      WHERE Id = @BookingId AND TenantId = @TenantId",
                    new { body.DriverId, VehicleId = vehicleId, BookingId = bookingId, UpdatedAt = DateTime.UtcNow, TenantId = tenantId },
                    cancellationToken: cancellationToken));
        }

        var assignmentType = string.IsNullOrWhiteSpace(body.AssignmentType) ? "Trip" : body.AssignmentType.Trim();

        var assignmentId = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO AssignmentHistory (TenantId, VehicleId, DriverId, BookingId, AssignmentType,
                  Status, StartAt, CreatedAt, CreatedBy, IsDeleted)
                  VALUES (@TenantId, @VehicleId, @DriverId, @BookingId, @AssignmentType,
                  'Active', @StartAt, @CreatedAt, @CreatedBy, 0);
                  SELECT SCOPE_IDENTITY();",
                new
                {
                    TenantId = tenantId,
                    VehicleId = vehicleId,
                    body.DriverId,
                    body.BookingId,
                    AssignmentType = assignmentType,
                    StartAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = createdBy
                },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId AND Status = @Available",
                new
                {
                    Status = (int)VehicleStatus.OnTrip,
                    UpdatedAt = DateTime.UtcNow,
                    Id = vehicleId,
                    TenantId = tenantId,
                    Available = (int)VehicleStatus.Available
                },
                cancellationToken: cancellationToken));

        return assignmentId;
    }

    public async Task AssignGpsAsync(
        int vehicleId, int tenantId, int gpsDeviceId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var vehicleExists = await ExistsOn(connection, vehicleId, tenantId, cancellationToken);
        if (!vehicleExists)
            throw new NotFoundException("Vehicle", vehicleId);

        var deviceExists = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM GpsDevices
                    WHERE Id = @DeviceId AND IsDeleted = 0
                      AND (TenantId = @TenantId OR TenantId IS NULL)
                ) THEN 1 ELSE 0 END
                """,
                new { DeviceId = gpsDeviceId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (!deviceExists)
            throw new NotFoundException("GpsDevice", gpsDeviceId);

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE GpsDevices SET VehicleId = NULL, UpdatedAt = @UpdatedAt WHERE VehicleId = @VehicleId AND Id <> @DeviceId AND (TenantId = @TenantId OR TenantId IS NULL)",
                new { VehicleId = vehicleId, DeviceId = gpsDeviceId, UpdatedAt = DateTime.UtcNow, TenantId = tenantId },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET GpsDeviceId = @DeviceId, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { DeviceId = gpsDeviceId, UpdatedAt = DateTime.UtcNow, Id = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE GpsDevices SET VehicleId = @VehicleId, TenantId = @TenantId, UpdatedAt = @UpdatedAt WHERE Id = @DeviceId",
                new { VehicleId = vehicleId, DeviceId = gpsDeviceId, UpdatedAt = DateTime.UtcNow, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task PublishAsync(int id, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<(string Name, string RegistrationNumber, int SeatingCapacity, decimal FuelAverage, int Status, string? EngineNo, string? ChassisNo)>(
            new CommandDefinition(
                @"SELECT Name, RegistrationNumber, SeatingCapacity, FuelAverage, Status, EngineNo, ChassisNo
                  FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (row.Name is null)
            throw new NotFoundException("Vehicle", id);

        if (string.IsNullOrWhiteSpace(row.Name))
            throw new ConflictException("Vehicle name is required before publishing.");
        if (string.IsNullOrWhiteSpace(row.RegistrationNumber) || row.RegistrationNumber.StartsWith("DRAFT-", StringComparison.Ordinal))
            throw new ConflictException("A valid license plate is required before publishing.");
        if (row.SeatingCapacity <= 0)
            throw new ConflictException("Seating capacity is required before publishing.");
        if (row.FuelAverage <= 0)
            throw new ConflictException("Fuel economy is required before publishing.");

        var hasRegistrationDoc = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM VehicleDocuments
                WHERE VehicleId = @Id AND TenantId = @TenantId AND IsDeleted = 0
                  AND DocumentType = N'Registration'
                  AND NULLIF(LTRIM(RTRIM(FileUrl)), '') IS NOT NULL
              ) THEN 1 ELSE 0 END",
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!hasRegistrationDoc)
            throw new ConflictException("A registration card is required before publishing.");

        var hasVehicleImage = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            @"SELECT CASE WHEN EXISTS(
                SELECT 1 FROM VehicleDocuments
                WHERE VehicleId = @Id AND TenantId = @TenantId AND IsDeleted = 0
                  AND DocumentType = N'VehicleImage'
                  AND NULLIF(LTRIM(RTRIM(FileUrl)), '') IS NOT NULL
              ) THEN 1 ELSE 0 END",
            new { Id = id, TenantId = tenantId },
            cancellationToken: cancellationToken));

        if (!hasVehicleImage)
            throw new ConflictException("At least one vehicle image is required before publishing.");

        if (!string.IsNullOrWhiteSpace(row.EngineNo))
        {
            var engineDup = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Vehicles
                    WHERE TenantId = @TenantId AND IsDeleted = 0 AND Id <> @Id
                      AND EngineNo IS NOT NULL AND LOWER(LTRIM(RTRIM(EngineNo))) = LOWER(LTRIM(RTRIM(@EngineNo)))
                  ) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId, EngineNo = row.EngineNo },
                cancellationToken: cancellationToken));
            if (engineDup)
                throw new ConflictException("Vehicle already registered with this engine number.");
        }

        if (!string.IsNullOrWhiteSpace(row.ChassisNo))
        {
            var chassisDup = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM Vehicles
                    WHERE TenantId = @TenantId AND IsDeleted = 0 AND Id <> @Id
                      AND ChassisNo IS NOT NULL AND LOWER(LTRIM(RTRIM(ChassisNo))) = LOWER(LTRIM(RTRIM(@ChassisNo)))
                  ) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId, ChassisNo = row.ChassisNo },
                cancellationToken: cancellationToken));
            if (chassisDup)
                throw new ConflictException("Vehicle already registered with this chassis number.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE Vehicles SET Status = @Status, UpdatedAt = @UpdatedAt WHERE Id = @Id AND TenantId = @TenantId",
                new { Status = (int)VehicleStatus.Available, UpdatedAt = DateTime.UtcNow, Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));
    }

    public async Task<(IReadOnlyList<VehicleMaintenanceDto> Items, int TotalCount)> GetMaintenancePagedAsync(
        int vehicleId, int tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await EnsureExistsOn(connection, vehicleId, tenantId, cancellationToken);

        var offset = (page - 1) * pageSize;

        var records = await connection.QueryAsync<VehicleMaintenanceDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, Description, Cost, MaintenanceDate, NextDueDate,
                  Status, ServiceProvider, CreatedAt
                  FROM Maintenance
                  WHERE IsDeleted = 0 AND VehicleId = @VehicleId
                  ORDER BY MaintenanceDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { VehicleId = vehicleId, Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken));

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Maintenance WHERE IsDeleted = 0 AND VehicleId = @VehicleId",
                new { VehicleId = vehicleId },
                cancellationToken: cancellationToken));

        return (records.ToList(), totalCount);
    }

    public async Task<VehicleFuelSummaryDto> GetFuelSummaryAsync(
        int vehicleId, int tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        await EnsureExistsOn(connection, vehicleId, tenantId, cancellationToken);

        var offset = (page - 1) * pageSize;

        var logs = await connection.QueryAsync<VehicleFuelDto>(
            new CommandDefinition(
                @"SELECT Id, VehicleId, DriverId, Liters, PricePerLiter, TotalCost,
                  OdometerReading, FuelType, FuelDate, Station, CreatedAt
                  FROM FuelLogs
                  WHERE IsDeleted = 0 AND VehicleId = @VehicleId
                  ORDER BY FuelDate DESC
                  OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
                new { VehicleId = vehicleId, Offset = offset, PageSize = pageSize },
                cancellationToken: cancellationToken));

        var totals = await connection.QuerySingleAsync<(decimal TotalLiters, decimal TotalCost, int TotalCount)>(
            new CommandDefinition(
                @"SELECT ISNULL(SUM(Liters), 0) AS TotalLiters,
                         ISNULL(SUM(TotalCost), 0) AS TotalCost,
                         COUNT(*) AS TotalCount
                  FROM FuelLogs WHERE IsDeleted = 0 AND VehicleId = @VehicleId",
                new { VehicleId = vehicleId },
                cancellationToken: cancellationToken));

        return new VehicleFuelSummaryDto(logs.ToList(), totals.TotalLiters, totals.TotalCost, totals.TotalCount);
    }

    public async Task<VehicleGpsSnapshot?> GetGpsSnapshotAsync(
        int vehicleId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var row = await connection.QuerySingleOrDefaultAsync<VehicleGpsSnapshot>(
            new CommandDefinition(
                @"SELECT v.GpsDeviceId,
                         gd.Name AS DeviceName, gd.UniqueId, gd.IsActive, gd.LastSeenAt, gd.LastIgnition,
                         vcl.Latitude, vcl.Longitude, vcl.Speed, vcl.LastUpdate,
                         gd.SimNumber, COALESCE(tm.Name, gd.Model, gd.Name) AS ModelName, tb.Name AS BrandName,
                         gd.InstallationDate,
                         vcl.TotalDistanceKm, vcl.BatteryLevel, vcl.GsmSignal, vcl.Address,
                         CASE
                             WHEN (gd.LastSeenAt IS NOT NULL AND gd.LastSeenAt > DATEADD(minute, -30, GETUTCDATE()))
                               OR (vcl.LastUpdate IS NOT NULL AND vcl.LastUpdate > DATEADD(minute, -30, GETUTCDATE()))
                             THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT)
                         END AS GpsOnline,
                         vcl.Heading, vcl.FuelLevel,
                         gd.TraccarDeviceId
                  FROM Vehicles v
                  LEFT JOIN GpsDevices gd ON gd.Id = v.GpsDeviceId AND gd.IsDeleted = 0
                  LEFT JOIN TrackerModels tm ON tm.Id = gd.TrackerModelId
                  LEFT JOIN TrackerBrands tb ON tb.Id = tm.TrackerBrandId
                  LEFT JOIN VehicleCurrentLocation vcl ON vcl.VehicleId = v.Id
                  WHERE v.Id = @VehicleId AND v.TenantId = @TenantId AND v.IsDeleted = 0",
                new { VehicleId = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        return row;
    }

    public async Task<IReadOnlyList<VehicleDocumentDto>> GetDocumentsAsync(
        int vehicleId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();
        return (await connection.QueryAsync<VehicleDocumentDto>(new CommandDefinition(
            @"SELECT Id, VehicleId, DocumentType, FileUrl, ExpiryDate, Notes
              FROM (
                SELECT Id, VehicleId, DocumentType, FileUrl, ExpiryDate, Notes, CreatedAt,
                  ROW_NUMBER() OVER (
                    PARTITION BY DocumentType
                    ORDER BY
                      CASE WHEN NULLIF(LTRIM(RTRIM(FileUrl)), '') IS NOT NULL THEN 0 ELSE 1 END,
                      CreatedAt DESC
                  ) AS rn
                FROM VehicleDocuments
                WHERE VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0
              ) ranked
              WHERE DocumentType = N'VehicleImage' OR rn = 1
              ORDER BY CreatedAt DESC",
            new { VehicleId = vehicleId, TenantId = tenantId },
            cancellationToken: cancellationToken))).ToList();
    }

    public async Task<(int Id, bool Updated)> UpsertDocumentAsync(
        int vehicleId, int tenantId, string documentType, string? fileUrl, DateTime? expiryDate, string? notes,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var existingId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            @"SELECT TOP 1 Id
              FROM VehicleDocuments
              WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                AND DocumentType = @DocumentType AND IsDeleted = 0
              ORDER BY
                CASE WHEN NULLIF(LTRIM(RTRIM(FileUrl)), '') IS NOT NULL THEN 0 ELSE 1 END,
                CreatedAt DESC",
            new
            {
                TenantId = tenantId,
                VehicleId = vehicleId,
                DocumentType = documentType
            },
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE VehicleDocuments
                  SET FileUrl = COALESCE(@FileUrl, FileUrl),
                      ExpiryDate = COALESCE(@ExpiryDate, ExpiryDate),
                      Notes = COALESCE(@Notes, Notes),
                      UpdatedAt = GETUTCDATE()
                  WHERE Id = @Id AND TenantId = @TenantId",
                new
                {
                    Id = existingId.Value,
                    TenantId = tenantId,
                    FileUrl = fileUrl,
                    ExpiryDate = expiryDate,
                    Notes = notes
                },
                cancellationToken: cancellationToken));

            return (existingId.Value, true);
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO VehicleDocuments (TenantId, VehicleId, DocumentType, FileUrl, ExpiryDate, Notes, CreatedAt, IsDeleted)
              VALUES (@TenantId, @VehicleId, @DocumentType, @FileUrl, @ExpiryDate, @Notes, GETUTCDATE(), 0);
              SELECT CAST(SCOPE_IDENTITY() AS INT);",
            new
            {
                TenantId = tenantId,
                VehicleId = vehicleId,
                DocumentType = documentType,
                FileUrl = fileUrl,
                ExpiryDate = expiryDate,
                Notes = notes
            },
            cancellationToken: cancellationToken));

        return (id, false);
    }

    public async Task SetPrimaryImageAsync(
        int vehicleId, int documentId, int tenantId, CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var doc = await connection.QuerySingleOrDefaultAsync<VehicleImageDocumentRow>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, Notes
                  FROM VehicleDocuments
                  WHERE Id = @DocumentId AND VehicleId = @VehicleId AND TenantId = @TenantId AND IsDeleted = 0",
                new { DocumentId = documentId, VehicleId = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        if (doc is null)
            throw new NotFoundException("Vehicle document", documentId);

        if (!string.Equals(doc.DocumentType, "VehicleImage", StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Only vehicle images can be set as the display photo.");

        var existing = await connection.QueryAsync<VehicleImageDocumentRow>(
            new CommandDefinition(
                @"SELECT Id, DocumentType, Notes
                  FROM VehicleDocuments
                  WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                    AND DocumentType = N'VehicleImage' AND IsDeleted = 0",
                new { VehicleId = vehicleId, TenantId = tenantId },
                cancellationToken: cancellationToken));

        foreach (var row in existing)
        {
            var cleared = VehicleImageNotes.ClearPrimary(row.Notes);
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE VehicleDocuments
                  SET Notes = @Notes, UpdatedAt = GETUTCDATE()
                  WHERE Id = @Id AND TenantId = @TenantId",
                new { Notes = string.IsNullOrWhiteSpace(cleared) ? null : cleared, row.Id, TenantId = tenantId },
                cancellationToken: cancellationToken));
        }

        var primaryNotes = VehicleImageNotes.SetPrimary(doc.Notes);
        await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE VehicleDocuments
              SET Notes = @Notes, UpdatedAt = GETUTCDATE()
              WHERE Id = @DocumentId AND TenantId = @TenantId",
            new { Notes = primaryNotes, DocumentId = documentId, TenantId = tenantId },
            cancellationToken: cancellationToken));
    }

    public async Task<int> PersistUploadedDocumentAsync(
        int vehicleId, int tenantId, string documentType, string storageKey, DateTime? expiryDate, string? notes,
        CancellationToken cancellationToken = default)
    {
        using var connection = dbFactory.CreateConnection();

        var exists = await ExistsOn(connection, vehicleId, tenantId, cancellationToken);
        if (!exists)
            throw new NotFoundException("Vehicle", vehicleId);

        string? finalNotes = notes;
        var isVehicleImage = VehicleDocumentTypes.IsVehicleImage(documentType);
        if (isVehicleImage)
        {
            var angle = VehicleImageNotes.NormalizeAngle(notes);
            var anglePattern = angle + "|%";
            var hasPrimary = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM VehicleDocuments
                        WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                          AND DocumentType = N'VehicleImage' AND IsDeleted = 0
                          AND (
                            Notes LIKE N'%|primary%'
                            OR LOWER(LTRIM(RTRIM(Notes))) = N'primary'
                          )
                    ) THEN 1 ELSE 0 END",
                    new { VehicleId = vehicleId, TenantId = tenantId },
                    cancellationToken: cancellationToken));

            var replacingPrimary = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    @"SELECT CASE WHEN EXISTS(
                        SELECT 1 FROM VehicleDocuments
                        WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                          AND DocumentType = N'VehicleImage' AND IsDeleted = 0
                          AND (Notes = @Angle OR Notes LIKE @AnglePattern)
                          AND (
                            Notes LIKE N'%|primary%'
                            OR LOWER(LTRIM(RTRIM(Notes))) = N'primary'
                          )
                    ) THEN 1 ELSE 0 END",
                    new { VehicleId = vehicleId, TenantId = tenantId, Angle = angle, AnglePattern = anglePattern },
                    cancellationToken: cancellationToken));

            finalNotes = VehicleImageNotes.WithAngle(angle, isPrimary: !hasPrimary || replacingPrimary);

            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE VehicleDocuments
                  SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                  WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                    AND DocumentType = N'VehicleImage' AND IsDeleted = 0
                    AND (Notes = @Angle OR Notes LIKE @AnglePattern)",
                new
                {
                    VehicleId = vehicleId,
                    TenantId = tenantId,
                    Angle = angle,
                    AnglePattern = anglePattern
                },
                cancellationToken: cancellationToken));
        }
        else
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE VehicleDocuments
                  SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                  WHERE VehicleId = @VehicleId AND TenantId = @TenantId
                    AND DocumentType = @DocumentType AND IsDeleted = 0",
                new
                {
                    VehicleId = vehicleId,
                    TenantId = tenantId,
                    DocumentType = documentType
                },
                cancellationToken: cancellationToken));
        }

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                @"INSERT INTO VehicleDocuments
                    (TenantId, VehicleId, DocumentType, FileUrl, ExpiryDate, Notes, CreatedAt, IsDeleted)
                  VALUES
                    (@TenantId, @VehicleId, @DocumentType, @FileUrl, @ExpiryDate, @Notes, GETUTCDATE(), 0);
                  SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    TenantId = tenantId,
                    VehicleId = vehicleId,
                    DocumentType = documentType,
                    FileUrl = storageKey,
                    ExpiryDate = expiryDate,
                    Notes = finalNotes
                },
                cancellationToken: cancellationToken));
    }

    private static string? NormalizeCurrencyCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private static Task<bool> ExistsOn(System.Data.IDbConnection connection, int id, int tenantId, CancellationToken cancellationToken)
        => connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT CASE WHEN EXISTS(SELECT 1 FROM Vehicles WHERE Id = @Id AND TenantId = @TenantId AND IsDeleted = 0) THEN 1 ELSE 0 END",
                new { Id = id, TenantId = tenantId },
                cancellationToken: cancellationToken));

    private static async Task EnsureExistsOn(System.Data.IDbConnection connection, int id, int tenantId, CancellationToken cancellationToken)
    {
        if (!await ExistsOn(connection, id, tenantId, cancellationToken))
            throw new NotFoundException("Vehicle", id);
    }

    private sealed class VehicleImageDocumentRow
    {
        public int Id { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
