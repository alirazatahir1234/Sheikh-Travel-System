import {
  aggregateHealthReasons,
  breakdownFromSummary,
  fleetHealthChartData,
  vehicleHealthById
} from './fleet-health.util';
import { FleetHealthSummary, FleetVehicleHealth } from '../../../core/models/gps-tracking.model';

describe('fleet-health.util (API-backed)', () => {
  const summary = (overrides: Partial<FleetHealthSummary> = {}): FleetHealthSummary => ({
    assessedPercent: 88,
    optimal: 2,
    healthy: 1,
    attention: 1,
    critical: 0,
    unknown: 1,
    total: 5,
    assessed: 4,
    vehicles: [],
    ...overrides
  });

  it('maps API summary without inventing assessed percent', () => {
    expect(breakdownFromSummary(null)).toEqual({
      optimal: 0,
      healthy: 0,
      attention: 0,
      critical: 0,
      unknown: 0,
      total: 0,
      assessed: 0,
      assessedPercent: null
    });
    expect(breakdownFromSummary(summary({ assessedPercent: null })).assessedPercent).toBeNull();
    expect(breakdownFromSummary(summary()).assessedPercent).toBe(88);
    expect(breakdownFromSummary(summary()).healthy).toBe(1);
  });

  it('builds a five-band chart including Healthy', () => {
    const chart = fleetHealthChartData(breakdownFromSummary(summary()));
    expect(chart.labels).toEqual(['Optimal', 'Healthy', 'Attention', 'Critical', 'Unknown']);
    expect(chart.datasets![0].data).toEqual([2, 1, 1, 0, 1]);
  });

  it('aggregates reasons only from Attention and Critical vehicles', () => {
    const vehicles: FleetVehicleHealth[] = [
      {
        vehicleId: 1,
        vehicleName: 'A',
        registrationNumber: '',
        band: 'Attention',
        score: 60,
        reasons: ['GPS stale', 'Maintenance due soon'],
        factors: []
      },
      {
        vehicleId: 2,
        vehicleName: 'B',
        registrationNumber: '',
        band: 'Critical',
        score: 20,
        reasons: ['Maintenance overdue', 'GPS stale'],
        factors: []
      },
      {
        vehicleId: 3,
        vehicleName: 'C',
        registrationNumber: '',
        band: 'Optimal',
        score: 100,
        reasons: ['should ignore'],
        factors: []
      }
    ];
    const reasons = aggregateHealthReasons(vehicles);
    expect(reasons.find(r => r.reason === 'GPS stale')?.count).toBe(2);
    expect(reasons.find(r => r.reason === 'Maintenance overdue')?.count).toBe(1);
    expect(reasons.find(r => r.reason === 'should ignore')).toBeUndefined();
  });

  it('indexes vehicles by id for detail panel lookup', () => {
    const map = vehicleHealthById([
      {
        vehicleId: 7,
        vehicleName: 'X',
        registrationNumber: '',
        band: 'Healthy',
        score: 80,
        reasons: [],
        factors: []
      }
    ]);
    expect(map.get(7)?.score).toBe(80);
  });
});
