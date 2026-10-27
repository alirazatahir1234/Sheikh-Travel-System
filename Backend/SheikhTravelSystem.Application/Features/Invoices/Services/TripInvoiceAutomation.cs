using Microsoft.Extensions.Logging;
using SheikhTravelSystem.Application.Common.Interfaces.Repositories;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Invoices.Services;

public interface ITripInvoiceAutomation
{
    Task TryAutoIssueAndEmailOnTripCompletedAsync(
        int tenantId, int tripId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Creates a customer invoice when a trip completes, then emails the client when an address exists.
/// </summary>
public sealed class TripInvoiceAutomation(
    ICustomerInvoiceRepository invoiceRepository,
    IInvoiceIssueService invoiceIssueService,
    IInvoiceEmailService invoiceEmailService,
    ILogger<TripInvoiceAutomation> logger) : ITripInvoiceAutomation
{
    public async Task TryAutoIssueAndEmailOnTripCompletedAsync(
        int tenantId, int tripId, CancellationToken cancellationToken = default)
    {
        if (await invoiceRepository.HasActiveInvoiceForTripAsync(tripId, tenantId, cancellationToken))
            return;

        var trip = await invoiceRepository.GetTripSnapshotAsync(tripId, tenantId, cancellationToken);
        if (trip is null || trip.Status != (int)TripStatus.Completed)
            return;

        var issue = await invoiceIssueService.IssueFromTripAsync(tenantId, userId: null, tripId, cancellationToken);
        if (!issue.Success || issue.Data <= 0)
        {
            if (!issue.Success)
                logger.LogInformation("Trip {TripId} auto-invoice skipped: {Message}", tripId, issue.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(trip.CustomerEmail))
        {
            logger.LogInformation("Trip {TripId} invoice {InvoiceId} created; no customer email to send.", tripId, issue.Data);
            return;
        }

        try
        {
            await invoiceEmailService.SendAsync(issue.Data, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Trip {TripId} invoice {InvoiceId} email dispatch failed.", tripId, issue.Data);
        }
    }
}
