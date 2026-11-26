using SheikhTravelSystem.Application.Features.TravelNexus.Common;

namespace SheikhTravelSystem.Application.Common.Interfaces.Repositories;

public sealed record TravelReportFilter(DateTime From, DateTime To, int? BranchId, int? AgentUserId, string? CountryCode);

public sealed record TravelEnquiryPerformanceRow(int? AgentUserId, string? AgentName, string? BranchName, int Enquiries, int Won, int Lost, int Open, decimal? AvgDaysToWin);
public sealed record TravelVisaProcessingRow(string CountryCode, string CountryName, string VisaTypeName, int Submitted, int Approved, int Rejected, int Pending, decimal? AvgProcessingDays);
public sealed record TravelCountryApplicationsRow(string CountryCode, string CountryName, int Cases, int Applicants, int Approved, int Rejected, int Pending);
public sealed record TravelPendingDocumentsRow(int VisaCaseId, string CaseNumber, string CustomerName, string CountryName, string VisaTypeName, string Status,
    DateTime? IntendedTravelDate, int Outstanding, string? AgentName);
public sealed record TravelGroupManifestRow(int GroupId, string GroupNumber, string Name, DateTime DepartureDate, DateTime ReturnDate, int Capacity, int Registered,
    int Confirmed, int VisasApproved, int Ticketed, string Status);
public sealed record TravelBookingsReportRow(string ServiceType, string SupplierName, string Status, string CurrencyCode, int Services, decimal Sell, decimal Cost);
public sealed record TravelPerformanceRow(string Key, string Label, int Enquiries, int Won, int VisaCases, int BookingFiles, string? CurrencyCode, decimal Invoiced, decimal Collected);
public sealed record TravelPilgrimPaymentsRow(int GroupId, string GroupNumber, string GroupName, DateTime DepartureDate, string CurrencyCode, int Pilgrims, decimal Quoted,
    decimal Invoiced, decimal Paid, decimal Balance);

/// <summary>Report facts the existing repositories do not already provide. Every statement filters on TenantId; the branch clamp applies to the owning row.</summary>
public interface ITravelReportRepository
{
    Task<IReadOnlyList<TravelEnquiryPerformanceRow>> GetEnquiryPerformanceAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default);
    Task<IReadOnlyList<TravelVisaProcessingRow>> GetVisaProcessingAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default);
    Task<IReadOnlyList<TravelCountryApplicationsRow>> GetCountryApplicationsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default);
    Task<IReadOnlyList<TravelPendingDocumentsRow>> GetPendingDocumentsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default);
    Task<IReadOnlyList<TravelGroupManifestRow>> GetGroupManifestsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default);
    Task<IReadOnlyList<TravelBookingsReportRow>> GetBookingsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default);
    /// <param name="byBranch">True groups by branch, false by agent.</param>
    Task<IReadOnlyList<TravelPerformanceRow>> GetPerformanceAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, bool byBranch, CancellationToken ct = default);
    Task<IReadOnlyList<TravelPilgrimPaymentsRow>> GetPilgrimPaymentsAsync(int tenantId, TravelBranchScope scope, TravelReportFilter f, CancellationToken ct = default);
}
