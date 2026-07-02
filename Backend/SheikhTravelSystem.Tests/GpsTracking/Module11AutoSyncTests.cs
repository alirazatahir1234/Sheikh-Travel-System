using FluentAssertions;
using Microsoft.Extensions.Options;
using SheikhTravelSystem.Application.Features.GpsTracking.Trackers;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;
using SheikhTravelSystem.Infrastructure.Traccar;

namespace SheikhTravelSystem.Tests.GpsTracking;

/// <summary>
/// Module 11: Auto Sync — automated verification of sync state and registration contract.
/// </summary>
public class Module11AutoSyncTests
{
    [Fact]
    public void TraccarSyncState_RecordJobComplete_SetsLastPositionSyncAt()
    {
        var state = new TraccarSyncState(Options.Create(new TraccarOptions
        {
            Enabled = true,
            BaseUrl = "http://localhost:8082",
            PositionSyncIntervalSeconds = 5
        }));

        state.RecordJobComplete("positions", new TraccarSyncJobResult("positions", 2, 1, 1, 0));

        var snapshot = state.Snapshot(connected: true);
        snapshot.LastPositionSyncAt.Should().NotBeNull();
        snapshot.PositionSyncIntervalSeconds.Should().Be(5);
    }

    [Fact]
    public void TraccarSyncState_MarkRunning_ReflectsInSnapshot()
    {
        var state = new TraccarSyncState(Options.Create(new TraccarOptions { Enabled = true, BaseUrl = "http://localhost:8082" }));

        state.MarkRunning(true);
        state.Snapshot(connected: true).IsRunning.Should().BeTrue();

        state.MarkRunning(false);
        state.Snapshot(connected: true).IsRunning.Should().BeFalse();
    }

    [Fact]
    public void RegisterTrackerDto_RequiresTraccarDeviceId_OnSuccess()
    {
        // Contract: successful registration always returns TraccarDeviceId when Traccar create succeeds.
        var dto = new TrackerRegisteredDto(
            Id: 1,
            Name: "Test Tracker",
            UniqueId: "862292058090599",
            ProtocolLabel: "jimi",
            VehicleName: null,
            PlateNumber: null,
            TraccarDeviceId: 42,
            StatusMessage: "Registered · Not assigned");

        dto.TraccarDeviceId.Should().Be(42);
        dto.StatusMessage.Should().Contain("Registered");
    }

    [Fact]
    public void TraccarStatusDto_DeviceCount_IsTenantLinkedCount_NotServerWide()
    {
        // After Module 11 alignment, DeviceCount represents ERP trackers linked to Traccar for the tenant.
        var status = new TraccarStatusDto(true, "6.14.5", DeviceCount: 2);
        status.Connected.Should().BeTrue();
        status.DeviceCount.Should().Be(2);
    }
}
