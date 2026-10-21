using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Application.Features.Bookings.Commands;
using SheikhTravelSystem.Application.Features.Bookings.DTOs;
using SheikhTravelSystem.Application.Features.Notifications;
using SheikhTravelSystem.Application.Features.WhatsApp.Automation;

namespace SheikhTravelSystem.Tests.Bookings;

/// <summary>
/// Focused create → assign → unassign path covering go-live booking gaps
/// (no Playwright harness in ERP; uses existing xUnit + Moq stack).
/// </summary>
public class BookingCreateAssignUnassignTests
{
    [Fact]
    public async Task CreateThenAssignVehicleAndDriver_Succeeds()
    {
        var repo = new Mock<IBookingRepository>();
        var tenant = new Mock<ITenantContext>();
        var decision = new Mock<INotificationDecisionEngine>();
        var hooks = new Mock<IWhatsAppAutomationHooks>();
        var logger = new Mock<ILogger<CreateBookingCommandHandler>>();
        tenant.Setup(t => t.GetRequiredTenantId()).Returns(1);

        repo.Setup(r => r.CreateAsync(It.IsAny<CreateBookingDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateBookingResult(42, "BK-042", "Karachi → Lahore"));

        repo.Setup(r => r.AssignVehicleAsync(42, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingMutationResult.Ok());

        repo.Setup(r => r.AssignDriverAsync(42, 9, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingMutationResult.Ok("BK-042", assignedDriverUserId: 100));

        var createHandler = new CreateBookingCommandHandler(repo.Object, decision.Object, logger.Object);
        var vehicleHandler = new AssignVehicleCommandHandler(repo.Object);
        var driverHandler = new AssignDriverCommandHandler(
            repo.Object, tenant.Object, decision.Object, hooks.Object);

        var createResult = await createHandler.Handle(
            new CreateBookingCommand(new CreateBookingDto(
                CustomerId: 1,
                RouteId: 2,
                PickupTime: DateTime.UtcNow.AddHours(3),
                PassengerCount: 4,
                TotalAmount: 12000m,
                Notes: null)),
            CancellationToken.None);

        createResult.Success.Should().BeTrue();
        createResult.Data.Should().Be(42);

        var vehicleResult = await vehicleHandler.Handle(
            new AssignVehicleCommand(42, 7), CancellationToken.None);
        vehicleResult.Success.Should().BeTrue();

        var driverResult = await driverHandler.Handle(
            new AssignDriverCommand(42, 9), CancellationToken.None);
        driverResult.Success.Should().BeTrue();

        repo.Verify(r => r.CreateAsync(It.IsAny<CreateBookingDto>(), It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AssignVehicleAsync(42, 7, It.IsAny<CancellationToken>()), Times.Once);
        repo.Verify(r => r.AssignDriverAsync(42, 9, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnassignVehicle_ClearsAssignment()
    {
        var repo = new Mock<IBookingRepository>();
        repo.Setup(r => r.UnassignVehicleAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingMutationResult.Ok());

        var handler = new UnassignVehicleCommandHandler(repo.Object);
        var result = await handler.Handle(new UnassignVehicleCommand(42), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.UnassignVehicleAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnassignDriver_ClearsAssignment()
    {
        var repo = new Mock<IBookingRepository>();
        repo.Setup(r => r.UnassignDriverAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingMutationResult.Ok());

        var handler = new UnassignDriverCommandHandler(repo.Object);
        var result = await handler.Handle(new UnassignDriverCommand(42), CancellationToken.None);

        result.Success.Should().BeTrue();
        repo.Verify(r => r.UnassignDriverAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnassignVehicle_PropagatesRepositoryFailure()
    {
        var repo = new Mock<IBookingRepository>();
        repo.Setup(r => r.UnassignVehicleAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookingMutationResult.Fail("Can only unassign vehicle from pending or confirmed bookings."));

        var handler = new UnassignVehicleCommandHandler(repo.Object);
        var result = await handler.Handle(new UnassignVehicleCommand(5), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("pending or confirmed");
    }

    [Fact]
    public void BookingRepository_Source_ContainsUnassignNullUpdates()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "Backend", "SheikhTravelSystem.Infrastructure",
            "Persistence", "Repositories", "BookingRepository.cs");
        var text = File.ReadAllText(path);

        text.Should().Contain("UnassignDriverAsync");
        text.Should().Contain("UnassignVehicleAsync");
        text.Should().Contain("SET DriverId = NULL");
        text.Should().Contain("SET VehicleId = NULL");
    }

    [Fact]
    public void BookingsController_Source_ExposesUnassignEndpoints()
    {
        var root = FindRepoRoot();
        var path = Path.Combine(root, "Backend", "SheikhTravelSystem.API",
            "Controllers", "BookingsController.cs");
        var text = File.ReadAllText(path);

        text.Should().Contain("unassign-driver");
        text.Should().Contain("unassign-vehicle");
        text.Should().Contain("UnassignDriverCommand");
        text.Should().Contain("UnassignVehicleCommand");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Sheikh-Travel-System.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "Backend")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repo root not found");
    }
}
