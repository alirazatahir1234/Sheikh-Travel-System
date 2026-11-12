using FluentAssertions;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Domain.Enums;
using Xunit;

namespace SheikhTravelSystem.Tests.Vehicles;

public class VehicleDetailMapperTests
{
    [Fact]
    public void ToDto_MapsAllEnrichmentFields_IncludingCompanyTrailingOnDto()
    {
        var created = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var row = new VehicleDetailRow
        {
            Id = 8074,
            Name = "BTB-262",
            RegistrationNumber = "BTB-262",
            VehicleCode = "V-8074",
            Make = "Hino",
            Model = "RK",
            Year = 2019,
            SeatingCapacity = 45,
            FuelAverage = 8.5m,
            FuelType = FuelType.Diesel,
            CurrentMileage = 120000m,
            Status = VehicleStatus.Available,
            IsActive = true,
            CreatedAt = created,
            CompanyId = 5,
            CompanyName = "Tenant Five",
            DriverId = 99,
            DriverName = "Ali Driver",
            GpsOnline = true,
            HasGpsDevice = true,
            LocationLatitude = 31.52,
            LocationLongitude = 74.35,
            EngineIgnition = false,
            NextServiceDue = created.AddMonths(1),
            ServiceAlert = "Service Due",
        };

        var dto = VehicleDetailMapper.ToDto(row);

        dto.Id.Should().Be(8074);
        dto.Name.Should().Be("BTB-262");
        dto.RegistrationNumber.Should().Be("BTB-262");
        dto.CompanyId.Should().Be(5);
        dto.CompanyName.Should().Be("Tenant Five");
        dto.DriverId.Should().Be(99);
        dto.DriverName.Should().Be("Ali Driver");
        dto.GpsOnline.Should().BeTrue();
        dto.HasGpsDevice.Should().BeTrue();
        dto.LocationLatitude.Should().Be(31.52);
        dto.LocationLongitude.Should().Be(74.35);
        dto.EngineIgnition.Should().BeFalse();
        dto.ServiceAlert.Should().Be("Service Due");
        dto.FuelType.Should().Be(FuelType.Diesel);
        dto.Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public void ToDto_AllowsNullOptionalColumns()
    {
        var row = new VehicleDetailRow
        {
            Id = 8073,
            Name = "Spare",
            RegistrationNumber = "XYZ-1",
            SeatingCapacity = 1,
            FuelAverage = 1m,
            FuelType = FuelType.Petrol,
            CurrentMileage = 0m,
            Status = VehicleStatus.Available,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        var dto = VehicleDetailMapper.ToDto(row);

        dto.CompanyId.Should().BeNull();
        dto.CompanyName.Should().BeNull();
        dto.DriverId.Should().BeNull();
        dto.GpsDeviceId.Should().BeNull();
        dto.LocationLatitude.Should().BeNull();
        dto.EngineIgnition.Should().BeNull();
        dto.GpsOnline.Should().BeFalse();
        dto.HasGpsDevice.Should().BeFalse();
    }
}
