using Dapper;
using FluentValidation;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.DTOs;
using SheikhTravelSystem.Application.Features.GpsTracking.Services;

namespace SheikhTravelSystem.Application.Features.GpsTracking.Commands;

public record CreateGeofenceCommand(CreateGeofenceDto Geofence) : IRequest<ApiResponse<int>>;

public class CreateGeofenceCommandValidator : AbstractValidator<CreateGeofenceCommand>
{
    public CreateGeofenceCommandValidator()
    {
        RuleFor(x => x.Geofence.Name).NotEmpty().MaximumLength(100)
            .WithMessage("Geofence name is required (max 100 characters).");
        RuleFor(x => x.Geofence.Category).NotEmpty().WithMessage("Category is required.");
        RuleFor(x => x.Geofence.AreaType).NotEmpty();
        RuleFor(x => x.Geofence.Description).MaximumLength(250);
        RuleFor(x => x.Geofence).Custom((dto, ctx) =>
        {
            if (!GpsGeoHelper.TryValidateGeofenceGeometry(dto.AreaType, dto.RadiusMeters, dto.GeoJson, out var error) && error != null)
                ctx.AddFailure(error);
        });
    }
}

public class CreateGeofenceCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<CreateGeofenceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(CreateGeofenceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Geofence;
        var areaType = NormalizeAreaType(dto.AreaType);

        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0 AND LOWER(Name) = LOWER(@Name)",
            new { dto.Name },
            cancellationToken: cancellationToken));
        if (duplicate > 0)
            return ApiResponse<int>.FailResponse("A geofence with this name already exists.");

        var (centerLat, centerLng, radius) = NormalizeGeometry(areaType, dto.CenterLat, dto.CenterLng, dto.RadiusMeters, dto.GeoJson);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO Geofences (Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson, Color, Category, Description, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@Name, @AreaType, @CenterLat, @CenterLng, @RadiusMeters, @GeoJson, @Color, @Category, @Description, @IsActive, GETUTCDATE(), @CreatedBy, 0)",
            new
            {
                dto.Name,
                AreaType = areaType,
                CenterLat = centerLat,
                CenterLng = centerLng,
                RadiusMeters = radius,
                dto.GeoJson,
                Color = string.IsNullOrWhiteSpace(dto.Color) ? "#0f766e" : dto.Color,
                dto.Category,
                dto.Description,
                IsActive = dto.IsActive,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Geofence created.");
    }

    internal static string NormalizeAreaType(string? areaType)
    {
        var t = (areaType ?? "circle").Trim().ToLowerInvariant();
        return t is "polygon" or "rectangle" ? t : "circle";
    }

    internal static (double Lat, double Lng, double Radius) NormalizeGeometry(
        string areaType, double centerLat, double centerLng, double radiusMeters, string? geoJson)
    {
        if (areaType == "circle")
            return (centerLat, centerLng, radiusMeters);

        var ring = GpsGeoHelper.TryParsePolygonRing(geoJson);
        if (ring != null && ring.Count > 0)
        {
            var c = GpsGeoHelper.CentroidOfRing(ring);
            return (c.Lat, c.Lng, 0);
        }

        return (centerLat, centerLng, 0);
    }
}

public record UpdateGeofenceCommand(int Id, UpdateGeofenceDto Geofence) : IRequest<ApiResponse<bool>>;

public class UpdateGeofenceCommandValidator : AbstractValidator<UpdateGeofenceCommand>
{
    public UpdateGeofenceCommandValidator()
    {
        RuleFor(x => x.Geofence.Name).NotEmpty().MaximumLength(100)
            .WithMessage("Geofence name is required (max 100 characters).");
        RuleFor(x => x.Geofence.Category).NotEmpty().WithMessage("Category is required.");
        RuleFor(x => x.Geofence.Description).MaximumLength(250);
        RuleFor(x => x.Geofence).Custom((dto, ctx) =>
        {
            if (!GpsGeoHelper.TryValidateGeofenceGeometry(dto.AreaType, dto.RadiusMeters, dto.GeoJson, out var error) && error != null)
                ctx.AddFailure(error);
        });
    }
}

public class UpdateGeofenceCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<UpdateGeofenceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpdateGeofenceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var dto = request.Geofence;
        var areaType = CreateGeofenceCommandHandler.NormalizeAreaType(dto.AreaType);

        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0 AND LOWER(Name) = LOWER(@Name) AND Id <> @Id",
            new { dto.Name, request.Id },
            cancellationToken: cancellationToken));
        if (duplicate > 0)
            return ApiResponse<bool>.FailResponse("A geofence with this name already exists.");

        var (centerLat, centerLng, radius) = CreateGeofenceCommandHandler.NormalizeGeometry(
            areaType, dto.CenterLat, dto.CenterLng, dto.RadiusMeters, dto.GeoJson);

        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE Geofences SET Name = @Name, AreaType = @AreaType, CenterLat = @CenterLat, CenterLng = @CenterLng,
              RadiusMeters = @RadiusMeters, GeoJson = @GeoJson, Color = @Color, Category = @Category,
              Description = @Description, IsActive = @IsActive, UpdatedAt = GETUTCDATE(), UpdatedBy = @UpdatedBy
              WHERE Id = @Id AND IsDeleted = 0",
            new
            {
                request.Id,
                dto.Name,
                AreaType = areaType,
                CenterLat = centerLat,
                CenterLng = centerLng,
                RadiusMeters = radius,
                dto.GeoJson,
                Color = string.IsNullOrWhiteSpace(dto.Color) ? "#0f766e" : dto.Color,
                dto.Category,
                dto.Description,
                dto.IsActive,
                UpdatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Geofence updated.")
            : ApiResponse<bool>.FailResponse("Geofence not found.");
    }
}

public record DeleteGeofenceCommand(int Id) : IRequest<ApiResponse<bool>>;

public class DeleteGeofenceCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteGeofenceCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteGeofenceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE Geofences SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE Id = @Id",
            new { request.Id },
            cancellationToken: cancellationToken));

        if (rows > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE GeofenceAssignments SET IsDeleted = 1, UpdatedAt = GETUTCDATE() WHERE GeofenceId = @Id AND IsDeleted = 0",
                new { request.Id },
                cancellationToken: cancellationToken));
        }

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Geofence deleted.")
            : ApiResponse<bool>.FailResponse("Geofence not found.");
    }
}

public record DuplicateGeofenceCommand(int Id) : IRequest<ApiResponse<int>>;

public class DuplicateGeofenceCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<DuplicateGeofenceCommand, ApiResponse<int>>
{
    public async Task<ApiResponse<int>> Handle(DuplicateGeofenceCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var src = await connection.QueryFirstOrDefaultAsync(new CommandDefinition(
            @"SELECT Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson, Color, Category, Description
              FROM Geofences WHERE Id = @Id AND IsDeleted = 0",
            new { request.Id },
            cancellationToken: cancellationToken));

        if (src is null)
            return ApiResponse<int>.FailResponse("Geofence not found.");

        string baseName = src.Name;
        var newName = $"{baseName} (Copy)";
        var n = 2;
        while (await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                   @"SELECT COUNT(1) FROM Geofences WHERE IsDeleted = 0 AND LOWER(Name) = LOWER(@Name)",
                   new { Name = newName },
                   cancellationToken: cancellationToken)) > 0)
        {
            newName = $"{baseName} (Copy {n++})";
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            @"INSERT INTO Geofences (Name, AreaType, CenterLat, CenterLng, RadiusMeters, GeoJson, Color, Category, Description, IsActive, CreatedAt, CreatedBy, IsDeleted)
              OUTPUT INSERTED.Id
              VALUES (@Name, @AreaType, @CenterLat, @CenterLng, @RadiusMeters, @GeoJson, @Color, @Category, @Description, 1, GETUTCDATE(), @CreatedBy, 0)",
            new
            {
                Name = newName,
                AreaType = (string)src.AreaType,
                CenterLat = (double)src.CenterLat,
                CenterLng = (double)src.CenterLng,
                RadiusMeters = (double)src.RadiusMeters,
                GeoJson = (string?)src.GeoJson,
                Color = (string?)src.Color ?? "#0f766e",
                Category = (string?)src.Category,
                Description = (string?)src.Description,
                CreatedBy = currentUser.UserId?.ToString()
            },
            cancellationToken: cancellationToken));

        return ApiResponse<int>.SuccessResponse(id, "Geofence duplicated.");
    }
}

public record UpsertGeofenceAssignmentsCommand(int GeofenceId, UpsertGeofenceAssignmentsDto Body)
    : IRequest<ApiResponse<bool>>;

public class UpsertGeofenceAssignmentsCommandHandler(IDbConnectionFactory dbFactory, ICurrentUserService currentUser)
    : IRequestHandler<UpsertGeofenceAssignmentsCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(UpsertGeofenceAssignmentsCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM Geofences WHERE Id = @Id AND IsDeleted = 0",
            new { Id = request.GeofenceId },
            cancellationToken: cancellationToken));
        if (exists == 0)
            return ApiResponse<bool>.FailResponse("Geofence not found.");

        var dto = request.Body;
        var createdBy = currentUser.UserId?.ToString();

        if (dto.ReplaceVehicles)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE GeofenceAssignments SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
                  WHERE GeofenceId = @GeofenceId AND VehicleId IS NOT NULL AND IsDeleted = 0",
                new { request.GeofenceId },
                cancellationToken: cancellationToken));
        }

        if (dto.VehicleIds is { Length: > 0 })
        {
            foreach (var vehicleId in dto.VehicleIds.Distinct())
            {
                var already = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                    @"SELECT COUNT(1) FROM GeofenceAssignments
                      WHERE GeofenceId = @GeofenceId AND VehicleId = @VehicleId AND IsDeleted = 0",
                    new { request.GeofenceId, VehicleId = vehicleId },
                    cancellationToken: cancellationToken));
                if (already > 0) continue;

                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO GeofenceAssignments (GeofenceId, VehicleId, CreatedAt, CreatedBy, IsDeleted)
                      VALUES (@GeofenceId, @VehicleId, GETUTCDATE(), @CreatedBy, 0)",
                    new { request.GeofenceId, VehicleId = vehicleId, CreatedBy = createdBy },
                    cancellationToken: cancellationToken));
            }
        }

        if (dto.BranchId is > 0)
        {
            var already = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT COUNT(1) FROM GeofenceAssignments
                  WHERE GeofenceId = @GeofenceId AND BranchId = @BranchId AND IsDeleted = 0",
                new { request.GeofenceId, dto.BranchId },
                cancellationToken: cancellationToken));
            if (already == 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO GeofenceAssignments (GeofenceId, BranchId, CreatedAt, CreatedBy, IsDeleted)
                      VALUES (@GeofenceId, @BranchId, GETUTCDATE(), @CreatedBy, 0)",
                    new { request.GeofenceId, dto.BranchId, CreatedBy = createdBy },
                    cancellationToken: cancellationToken));
            }
        }

        if (dto.DepartmentId is > 0)
        {
            var already = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT COUNT(1) FROM GeofenceAssignments
                  WHERE GeofenceId = @GeofenceId AND DepartmentId = @DepartmentId AND IsDeleted = 0",
                new { request.GeofenceId, dto.DepartmentId },
                cancellationToken: cancellationToken));
            if (already == 0)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    @"INSERT INTO GeofenceAssignments (GeofenceId, DepartmentId, CreatedAt, CreatedBy, IsDeleted)
                      VALUES (@GeofenceId, @DepartmentId, GETUTCDATE(), @CreatedBy, 0)",
                    new { request.GeofenceId, dto.DepartmentId, CreatedBy = createdBy },
                    cancellationToken: cancellationToken));
            }
        }

        return ApiResponse<bool>.SuccessResponse(true, "Assignments saved.");
    }
}

public record DeleteGeofenceAssignmentCommand(int GeofenceId, int AssignmentId) : IRequest<ApiResponse<bool>>;

public class DeleteGeofenceAssignmentCommandHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<DeleteGeofenceAssignmentCommand, ApiResponse<bool>>
{
    public async Task<ApiResponse<bool>> Handle(DeleteGeofenceAssignmentCommand request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();
        var rows = await connection.ExecuteAsync(new CommandDefinition(
            @"UPDATE GeofenceAssignments SET IsDeleted = 1, UpdatedAt = GETUTCDATE()
              WHERE Id = @AssignmentId AND GeofenceId = @GeofenceId AND IsDeleted = 0",
            new { request.AssignmentId, request.GeofenceId },
            cancellationToken: cancellationToken));

        return rows > 0
            ? ApiResponse<bool>.SuccessResponse(true, "Assignment removed.")
            : ApiResponse<bool>.FailResponse("Assignment not found.");
    }
}
