using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Payments.DTOs;
using SheikhTravelSystem.Domain.Enums;

namespace SheikhTravelSystem.Application.Features.Payments.Queries;

public record GetPaymentReportQuery(DateTime? FromDate, DateTime? ToDate) : IRequest<ApiResponse<PaymentReportDto>>;

public class GetPaymentReportQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetPaymentReportQuery, ApiResponse<PaymentReportDto>>
{
    public async Task<ApiResponse<PaymentReportDto>> Handle(GetPaymentReportQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var fromDate = request.FromDate ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var totalReceived = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                @"SELECT ISNULL(SUM(Amount), 0) FROM Payments
                  WHERE Status = @PaidStatus AND PaymentDate BETWEEN @FromDate AND @ToDate AND IsDeleted = 0",
                new { PaidStatus = (int)PaymentStatus.Paid, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));

        var totalPending = await connection.ExecuteScalarAsync<decimal>(
            new CommandDefinition(
                @"SELECT ISNULL(SUM(Amount), 0) FROM Payments
                  WHERE Status IN (@Pending, @Partial) AND PaymentDate BETWEEN @FromDate AND @ToDate AND IsDeleted = 0",
                new { Pending = (int)PaymentStatus.Pending, Partial = (int)PaymentStatus.PartiallyPaid, FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));

        var totalTransactions = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(*) FROM Payments WHERE PaymentDate BETWEEN @FromDate AND @ToDate AND IsDeleted = 0",
                new { FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));

        var recentPayments = await connection.QueryAsync<PaymentDto>(
            new CommandDefinition(
                @"SELECT TOP 10 Id, BookingId, Amount, PaymentMethod, Status, PaymentDate,
                  TransactionReference, Notes, CreatedAt
                  FROM Payments WHERE PaymentDate BETWEEN @FromDate AND @ToDate AND IsDeleted = 0
                  ORDER BY CreatedAt DESC",
                new { FromDate = fromDate, ToDate = toDate },
                cancellationToken: cancellationToken));

        var report = new PaymentReportDto(totalReceived, totalPending, totalTransactions, recentPayments.ToList());
        return ApiResponse<PaymentReportDto>.SuccessResponse(report);
    }
}
