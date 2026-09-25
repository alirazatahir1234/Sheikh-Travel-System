import {
  connectivityFromStatus,
  connectivityLabel,
  dualStatusLine,
  operationalLabel
} from './fleet-status-labels.util';

describe('fleet-status-labels', () => {
  it('separates connectivity from operational parked', () => {
    expect(connectivityFromStatus('parked')).toBe('online');
    expect(connectivityLabel('parked')).toBe('Online');
    expect(operationalLabel('parked')).toBe('Parked');
    expect(dualStatusLine('parked')).toBe('Online • Parked');
  });

  it('labels unknown operational state while online', () => {
    expect(connectivityFromStatus('unknown')).toBe('online');
    expect(dualStatusLine('unknown')).toBe('Online • Unknown');
    expect(operationalLabel('unknown')).toBe('Unknown');
  });

  it('labels offline and never seen without fake operational motion', () => {
    expect(dualStatusLine('offline')).toBe('Offline');
    expect(dualStatusLine('never_seen')).toBe('Never Seen');
    expect(operationalLabel('offline')).toBe('Unknown');
  });
});
