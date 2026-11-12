using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common.Exceptions;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Vehicles.Commands;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Tests.Vehicles;

public class UpdateVehicleCompanyChangeTests
{
    private static UpdateVehicleDto SampleDto(int? companyId = null) => new(
        Name: "Coach One",
        RegistrationNumber: "ABC-123",
        Model: "Hino",
        Year: 2024,
        SeatingCapacity: 40,
        FuelAverage: 10m,
        FuelType: FuelType.Diesel,
        CurrentMileage: 1000m,
        InsuranceExpiryDate: DateTime.UtcNow.AddYears(1),
        Status: VehicleStatus.Available,
        CompanyId: companyId);

    [Fact]
    public async Task SuperAdmin_CanMoveVehicleToAnotherCompany()
    {
        var vehicles = new Mock<IVehicleRepository>();
        var users = new Mock<IUserRepository>();
        var scope = new Mock<IPlatformScope>();

        vehicles.Setup(v => v.GetTenantIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        scope.SetupGet(s => s.IsSuperAdmin).Returns(true);

        var handler = new UpdateVehicleCommandHandler(vehicles.Object, users.Object, scope.Object);
        var result = await handler.Handle(new UpdateVehicleCommand(7, SampleDto(3)), CancellationToken.None);

        result.Success.Should().BeTrue();
        scope.Verify(s => s.EnsureTenantAccess(1), Times.Once);
        scope.Verify(s => s.EnsureTenantAccess(3), Times.Once);
        users.Verify(u => u.EnsureOrgBelongsToTenantAsync(3, null, null, It.IsAny<CancellationToken>()), Times.Once);
        vehicles.Verify(v => v.PersistVehicleAsync(7, 1, 3, It.IsAny<UpdateVehicleDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NonSuperAdmin_CannotMoveVehicleToAnotherCompany()
    {
        var vehicles = new Mock<IVehicleRepository>();
        var users = new Mock<IUserRepository>();
        var scope = new Mock<IPlatformScope>();

        vehicles.Setup(v => v.GetTenantIdAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        scope.SetupGet(s => s.IsSuperAdmin).Returns(false);

        var handler = new UpdateVehicleCommandHandler(vehicles.Object, users.Object, scope.Object);
        var act = () => handler.Handle(new UpdateVehicleCommand(7, SampleDto(3)), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*cannot move vehicles*");
        vehicles.Verify(
            v => v.PersistVehicleAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<UpdateVehicleDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task MissingVehicle_ThrowsNotFound()
    {
        var vehicles = new Mock<IVehicleRepository>();
        var users = new Mock<IUserRepository>();
        var scope = new Mock<IPlatformScope>();

        vehicles.Setup(v => v.GetTenantIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        var handler = new UpdateVehicleCommandHandler(vehicles.Object, users.Object, scope.Object);
        var act = () => handler.Handle(new UpdateVehicleCommand(99, SampleDto()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public void RepositorySource_PersistsTenantIdOnUpdateAndCascadesScopedRows()
    {
        var repoPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "SheikhTravelSystem.Infrastructure",
            "Persistence", "Repositories", "VehicleRepository.cs"));

        var source = File.ReadAllText(repoPath);
        source.Should().Contain("TenantId = @TargetTenantId");
        source.Should().Contain("UPDATE VehicleDocuments SET TenantId = @TargetTenantId");
        source.Should().Contain("UPDATE GpsDevices SET TenantId = @TargetTenantId");
        source.Should().Contain("Status = N'Completed'");
    }
}
