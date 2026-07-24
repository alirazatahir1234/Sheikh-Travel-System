using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.GpsTracking.Traccar;

namespace SheikhTravelSystem.Infrastructure.Services.GpsControl;

public sealed class TraccarGpsTransportProvider(ITraccarClient traccar) : IGpsTransportProvider
{
    public string Name => "Traccar";

    public bool CanHandle(string transport) =>
        string.Equals(transport, "Traccar", StringComparison.OrdinalIgnoreCase)
        || string.Equals(transport, "Auto", StringComparison.OrdinalIgnoreCase);

    public async Task<GpsTransportSendResult> SendAsync(
        GpsTransportSendRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.TraccarDeviceId is null or <= 0)
            return new GpsTransportSendResult(false, Name, null, "Device is not linked to Traccar.");

        var type = request.TraccarType ?? "custom";
        var attributes = request.Attributes;
        if (string.Equals(type, "custom", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(request.Payload)
            && (attributes is null || !attributes.ContainsKey("data")))
        {
            attributes = new Dictionary<string, object>(attributes ?? new Dictionary<string, object>())
            {
                ["data"] = request.Payload
            };
        }

        var sent = await traccar.SendCommandAsync(
            request.TraccarDeviceId.Value,
            type,
            attributes,
            cancellationToken);

        return sent
            ? new GpsTransportSendResult(true, Name, null, null)
            : new GpsTransportSendResult(false, Name, null, "Traccar dispatch failed.");
    }
}

public sealed class SmsGpsTransportProvider : IGpsTransportProvider
{
    public string Name => "Sms";

    public bool CanHandle(string transport) =>
        string.Equals(transport, "Sms", StringComparison.OrdinalIgnoreCase);

    public Task<GpsTransportSendResult> SendAsync(
        GpsTransportSendRequest request,
        CancellationToken cancellationToken = default)
    {
        // Phase 16.9: SMS adapter stub — payload is ready; tenant SMS gateway not wired.
        return Task.FromResult(new GpsTransportSendResult(
            false,
            Name,
            null,
            "SMS gateway not configured. Rendered payload stored for manual/gateway delivery: "
            + (request.Payload.Length > 120 ? request.Payload[..120] + "…" : request.Payload)));
    }
}

public sealed class SimulatorGpsTransportProvider : IGpsTransportProvider
{
    public string Name => "Simulator";

    public bool CanHandle(string transport) =>
        string.Equals(transport, "Simulator", StringComparison.OrdinalIgnoreCase);

    public Task<GpsTransportSendResult> SendAsync(
        GpsTransportSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var canned = request.Payload.ToUpperInvariant() switch
        {
            var p when p.Contains("STATUS") =>
                "Battery:85%;GPS:OK;ACC:OFF;GSM:22;Speed:0;Relay:0",
            var p when p.Contains("VERSION") => "VERSION:EV26R_FW_1.2.0",
            var p when p.Contains("ICCID") => "ICCID:89860000000000000001",
            var p when p.Contains("IMSI") => "IMSI:310150123456789",
            var p when p.Contains("PARAM") => "PARAM:OK;HBT:60;APN:internet",
            var p when p.Contains("RELAY") => "RELAY OK",
            var p when p.Contains("RESET") => "RESET OK",
            _ => $"SIM_OK:{request.Payload}"
        };

        return Task.FromResult(new GpsTransportSendResult(true, Name, canned, null));
    }
}

/// <summary>Stub providers so non-Traccar transports can plug in later without ERP changes.</summary>
public sealed class StubGpsTransportProvider(string name) : IGpsTransportProvider
{
    public string Name { get; } = name;

    public bool CanHandle(string transport) =>
        string.Equals(transport, Name, StringComparison.OrdinalIgnoreCase);

    public Task<GpsTransportSendResult> SendAsync(
        GpsTransportSendRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GpsTransportSendResult(
            false, Name, null, $"{Name} transport is not implemented yet (Stage 16 stub)."));
}

public sealed class GpsTransportRouter(IEnumerable<IGpsTransportProvider> providers) : IGpsTransportRouter
{
    public Task<GpsTransportSendResult> SendAsync(
        GpsTransportSendRequest request,
        CancellationToken cancellationToken = default)
    {
        var provider = providers.FirstOrDefault(p => p.CanHandle(request.Transport))
            ?? providers.FirstOrDefault(p => p.CanHandle("Traccar"));

        if (provider is null)
            return Task.FromResult(new GpsTransportSendResult(false, request.Transport, null, "No transport provider registered."));

        return provider.SendAsync(request, cancellationToken);
    }
}
