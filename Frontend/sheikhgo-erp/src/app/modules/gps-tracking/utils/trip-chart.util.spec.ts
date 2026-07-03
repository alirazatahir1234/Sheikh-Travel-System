import {
  distanceTrendData,
  drivingVsIdleData,
  fuelConsumptionData,
  speedTrendData,
  tripsPerDayData
} from './trip-chart.util';
import { GpsTrip, TripAnalyticsSummary } from '../../../core/models/gps-tracking.model';

function createTrip(overrides: Partial<GpsTrip> = {}): GpsTrip {
  return {
    vehicleId: 1,
    vehicleName: 'Vehicle 1',
    startTime: '2026-01-01T12:00:00.000Z',
    endTime: '2026-01-01T12:30:00.000Z',
    distanceKm: 10,
    avgSpeedKmh: 40,
    maxSpeedKmh: 60,
    durationMinutes: 30,
    ...overrides
  };
}

function createSummary(overrides: Partial<TripAnalyticsSummary> = {}): TripAnalyticsSummary {
  return {
    tripCount: 1,
    distanceKm: 10,
    drivingMinutes: 0,
    idleMinutes: 0,
    avgSpeedKmh: 40,
    maxSpeedKmh: 60,
    stopCount: 0,
    overspeedCount: 0,
    harshBrakeCount: 0,
    harshAccelCount: 0,
    ...overrides
  };
}

describe('distanceTrendData', () => {
  it('returns an empty chart for an empty trip list', () => {
    expect(distanceTrendData([])).toEqual({ labels: [], datasets: [] });
  });

  it('sums distance within the same calendar day into a single bucket', () => {
    const trips = [
      createTrip({ startTime: '2026-01-01T08:00:00.000Z', distanceKm: 10 }),
      createTrip({ startTime: '2026-01-01T18:00:00.000Z', distanceKm: 5 })
    ];

    const result = distanceTrendData(trips);

    expect(result.labels?.length).toBe(1);
    expect((result.datasets[0].data as number[])[0]).toBe(15);
  });

  it('buckets trips on different days chronologically', () => {
    const trips = [
      createTrip({ startTime: '2026-01-05T12:00:00.000Z', distanceKm: 5 }),
      createTrip({ startTime: '2026-01-01T12:00:00.000Z', distanceKm: 10 })
    ];

    const result = distanceTrendData(trips);

    expect(result.labels?.length).toBe(2);
    expect((result.datasets[0].data as number[])).toEqual([10, 5]);
  });
});

describe('speedTrendData', () => {
  it('returns an empty chart for an empty trip list', () => {
    expect(speedTrendData([])).toEqual({ labels: [], datasets: [] });
  });

  it('averages speed per day', () => {
    const trips = [
      createTrip({ startTime: '2026-01-01T08:00:00.000Z', avgSpeedKmh: 40 }),
      createTrip({ startTime: '2026-01-01T18:00:00.000Z', avgSpeedKmh: 60 })
    ];

    const result = speedTrendData(trips);

    expect((result.datasets[0].data as number[])[0]).toBe(50);
  });
});

describe('tripsPerDayData', () => {
  it('returns an empty chart for an empty trip list', () => {
    expect(tripsPerDayData([])).toEqual({ labels: [], datasets: [] });
  });

  it('counts trips per calendar day', () => {
    const trips = [
      createTrip({ startTime: '2026-01-01T08:00:00.000Z' }),
      createTrip({ startTime: '2026-01-01T18:00:00.000Z' }),
      createTrip({ startTime: '2026-01-02T08:00:00.000Z' })
    ];

    const result = tripsPerDayData(trips);

    expect(result.labels?.length).toBe(2);
    expect((result.datasets[0].data as number[])).toEqual([2, 1]);
  });
});

describe('drivingVsIdleData', () => {
  it('returns an empty chart for a null summary', () => {
    expect(drivingVsIdleData(null)).toEqual({ labels: [], datasets: [] });
  });

  it('returns an empty chart when both driving and idle minutes are zero', () => {
    expect(drivingVsIdleData(createSummary({ drivingMinutes: 0, idleMinutes: 0 }))).toEqual({ labels: [], datasets: [] });
  });

  it('returns driving/idle minutes as a two-slice dataset', () => {
    const result = drivingVsIdleData(createSummary({ drivingMinutes: 120, idleMinutes: 30 }));

    expect(result.labels).toEqual(['Driving', 'Idle']);
    expect(result.datasets[0].data).toEqual([120, 30]);
  });
});

describe('fuelConsumptionData', () => {
  it('returns an empty chart when no trips have fuel data', () => {
    const trips = [createTrip({ fuelLiters: null })];
    expect(fuelConsumptionData(trips, false)).toEqual({ labels: [], datasets: [] });
  });

  it('groups by day when not fleet-wide', () => {
    const trips = [
      createTrip({ startTime: '2026-01-01T08:00:00.000Z', fuelLiters: 3 }),
      createTrip({ startTime: '2026-01-01T18:00:00.000Z', fuelLiters: 2 })
    ];

    const result = fuelConsumptionData(trips, false);

    expect(result.labels?.length).toBe(1);
    expect((result.datasets[0].data as number[])[0]).toBe(5);
  });

  it('groups by vehicle, sorted descending, when fleet-wide', () => {
    const trips = [
      createTrip({ vehicleId: 1, vehicleName: 'Alpha', fuelLiters: 2 }),
      createTrip({ vehicleId: 2, vehicleName: 'Bravo', fuelLiters: 8 })
    ];

    const result = fuelConsumptionData(trips, true);

    expect(result.labels).toEqual(['Bravo', 'Alpha']);
    expect((result.datasets[0].data as number[])).toEqual([8, 2]);
  });
});
