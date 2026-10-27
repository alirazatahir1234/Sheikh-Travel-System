import {
  aggregateTripAnalyticsFromTrips,
  mergeTripAnalyticsSummaries,
  normalizeTripAnalyticsSummary,
  summaryForSelectedTrip
} from './trip-analytics.util';
import { GpsTrip, TripEvent } from '../../../core/models/gps-tracking.model';

describe('trip-analytics.util', () => {
  it('normalizeTripAnalyticsSummary reads PascalCase', () => {
    const s = normalizeTripAnalyticsSummary({
      TripCount: 3,
      DistanceKm: 42.5,
      DrivingMinutes: 120,
      IdleMinutes: 10,
      AvgSpeedKmh: 35,
      MaxSpeedKmh: 88,
      OverspeedCount: 2,
      EngineHours: 2
    });
    expect(s?.tripCount).toBe(3);
    expect(s?.distanceKm).toBe(42.5);
    expect(s?.overspeedCount).toBe(2);
    expect(s?.engineHours).toBe(2);
  });

  it('aggregateTripAnalyticsFromTrips sums moving trips', () => {
    const trips: GpsTrip[] = [
      {
        vehicleId: 1,
        startTime: '2026-01-01T08:00:00Z',
        endTime: '2026-01-01T09:00:00Z',
        distanceKm: 10,
        avgSpeedKmh: 40,
        maxSpeedKmh: 60,
        durationMinutes: 60
      },
      {
        vehicleId: 1,
        startTime: '2026-01-01T10:00:00Z',
        endTime: '2026-01-01T10:05:00Z',
        distanceKm: 0.05,
        avgSpeedKmh: 0,
        maxSpeedKmh: 0,
        durationMinutes: 5,
        status: 'Stop'
      }
    ];
    const agg = aggregateTripAnalyticsFromTrips(trips, 2, []);
    expect(agg.tripCount).toBe(2);
    expect(agg.distanceKm).toBe(10);
    expect(agg.drivingMinutes).toBe(60);
    expect(agg.engineHours).toBe(1);
  });

  it('mergeTripAnalyticsSummaries fills engine hours from fallback', () => {
    const merged = mergeTripAnalyticsSummaries(
      {
        tripCount: 5,
        distanceKm: 0,
        drivingMinutes: 0,
        idleMinutes: 0,
        avgSpeedKmh: 0,
        maxSpeedKmh: 0,
        stopCount: 0,
        overspeedCount: 0,
        harshBrakeCount: 0,
        harshAccelCount: 0,
        engineHours: null
      },
      {
        tripCount: 5,
        distanceKm: 100,
        drivingMinutes: 180,
        idleMinutes: 0,
        avgSpeedKmh: 45,
        maxSpeedKmh: 90,
        stopCount: 1,
        overspeedCount: 3,
        harshBrakeCount: 0,
        harshAccelCount: 0,
        engineHours: 3
      }
    );
    expect(merged?.distanceKm).toBe(100);
    expect(merged?.overspeedCount).toBe(3);
    expect(merged?.engineHours).toBe(3);
  });

  it('summaryForSelectedTrip counts overspeed in window', () => {
    const trip: GpsTrip = {
      vehicleId: 1,
      startTime: '2026-01-01T08:00:00Z',
      endTime: '2026-01-01T09:00:00Z',
      distanceKm: 20,
      avgSpeedKmh: 50,
      maxSpeedKmh: 95,
      durationMinutes: 60
    };
    const events: TripEvent[] = [
      { time: '2026-01-01T08:30:00Z', type: 'deviceOverspeed' },
      { time: '2026-01-02T08:30:00Z', type: 'overspeed' }
    ];
    const s = summaryForSelectedTrip(trip, events);
    expect(s.overspeedCount).toBe(1);
    expect(s.distanceKm).toBe(20);
  });
});
