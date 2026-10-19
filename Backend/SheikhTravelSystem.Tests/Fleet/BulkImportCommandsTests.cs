using FluentAssertions;
using Moq;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Drivers.Commands;
using SheikhTravelSystem.Application.Features.Drivers.DTOs;
using SheikhTravelSystem.Application.Features.Vehicles.Commands;
using SheikhTravelSystem.Application.Features.Vehicles.DTOs;
using SheikhTravelSystem.Domain.Enums;
using MediatR;

namespace SheikhTravelSystem.Tests.Fleet;

public class BulkImportCommandsTests
{
    [Fact]
    public async Task BulkVehicles_dry_run_skips_existing_registration()
    {
        var repo = new Mock<IVehicleRepository>();
        var tenant = new Mock<ITenantContext>();
        var mediator = new Mock<IMediator>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);
        repo.Setup(r => r.RegistrationExistsAsync(1, "ABC-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new BulkCreateVehiclesCommandHandler(mediator.Object, repo.Object, tenant.Object);
        var result = await handler.Handle(
            new BulkCreateVehiclesCommand(
            [
                new CreateVehicleDto("Van", "ABC-1", null, null, 7, 10, FuelType.Petrol, 0, null)
            ],
            new BulkCreateVehiclesOptions(DryRun: true, SkipDuplicates: true)),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Skipped.Should().Be(1);
        result.Data.Succeeded.Should().Be(0);
        mediator.Verify(m => m.Send(It.IsAny<CreateVehicleCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BulkDrivers_dry_run_marks_ready_rows()
    {
        var repo = new Mock<IDriverRepository>();
        var tenant = new Mock<ITenantContext>();
        var mediator = new Mock<IMediator>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(2);
        repo.Setup(r => r.CheckAvailabilityAsync(2, "03001112222", It.IsAny<string?>(), It.IsAny<string?>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DriverAvailabilityDto(true, true, true));

        var handler = new BulkCreateDriversCommandHandler(mediator.Object, repo.Object, tenant.Object);
        var result = await handler.Handle(
            new BulkCreateDriversCommand(
            [
                new CreateDriverDto(
                    "Ali", "Khan", "03001112222", "LIC-1", DateTime.UtcNow.AddYears(2),
                    Email: "ali@example.com", DateOfBirth: DateTime.UtcNow.AddYears(-25))
            ],
            new BulkCreateDriversOptions(DryRun: true)),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Data!.Created.Should().ContainSingle(c => c.DryRun);
        mediator.Verify(m => m.Send(It.IsAny<CreateDriverCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
