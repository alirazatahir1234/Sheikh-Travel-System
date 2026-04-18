using Dapper;
using MediatR;
using SheikhTravelSystem.Application.Common;
using SheikhTravelSystem.Application.Common.Interfaces;
using SheikhTravelSystem.Application.Features.Tracking.DTOs;

namespace SheikhTravelSystem.Application.Features.Tracking.Queries;

public record GetLiveTrackingQuery : IRequest<ApiResponse<List<TrackingDto>>>;

public class GetLiveTrackingQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetLiveTrackingQuery, ApiResponse<List<TrackingDto>>>
{
    public async Task<ApiResponse<List<TrackingDto>>> Handle(GetLiveTrackingQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        // Get latest position for each vehicle (within last 10 minutes)
        var tracking = await connection.QueryAsync<TrackingDto>(
            @"SELECT t.Id, t.VehicleId, t.DriverId, t.BookingId, t.Latitude, t.Longitude, t.Speed, t.Timestamp
              FROM VehicleTracking t
              INNER JOIN (
                  SELECT VehicleId, MAX(Timestamp) AS MaxTimestamp
                  FROM VehicleTracking
                  WHERE Timestamp > DATEADD(MINUTE, -10, GETUTCDATE()) AND IsDeleted = 0
                  GROUP BY VehicleId
              ) latest ON t.VehicleId = latest.VehicleId AND t.Timestamp = latest.MaxTimestamp");

        return ApiResponse<List<TrackingDto>>.SuccessResponse(tracking.ToList());
    }
}

public record GetTrackingHistoryQuery(int VehicleId, DateTime? FromDate, DateTime? ToDate)
    : IRequest<ApiResponse<List<TrackingDto>>>;

public class GetTrackingHistoryQueryHandler(IDbConnectionFactory dbFactory)
    : IRequestHandler<GetTrackingHistoryQuery, ApiResponse<List<TrackingDto>>>
{
    public async Task<ApiResponse<List<TrackingDto>>> Handle(GetTrackingHistoryQuery request, CancellationToken cancellationToken)
    {
        using var connection = dbFactory.CreateConnection();

        var fromDate = request.FromDate ?? DateTime.UtcNow.AddDays(-1);
        var toDate = request.ToDate ?? DateTime.UtcNow;

        var history = await connection.QueryAsync<TrackingDto>(
            @"SELECT Id, VehicleId, DriverId, BookingId, Latitude, Longitude, Speed, Timestamp
              FROM VehicleTracking
              WHERE VehicleId = @VehicleId AND Timestamp BETWEEN @FromDate AND @ToDate AND IsDeleted = 0
              ORDER BY Timestamp DESC",
            new { request.VehicleId, FromDate = fromDate, ToDate = toDate });

        return ApiResponse<List<TrackingDto>>.SuccessResponse(history.ToList());
    }
}
