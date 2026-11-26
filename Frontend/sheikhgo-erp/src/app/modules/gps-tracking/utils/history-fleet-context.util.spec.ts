import {
  HISTORY_PREFS_KEY,
  readHistoryPagePrefs,
  readStationary24hAlertPref,
  storeHistoryPagePrefs,
  storeStationary24hAlertPref
} from './history-fleet-context.util';

describe('stationary > 24h alert preference', () => {
  beforeEach(() => localStorage.removeItem(HISTORY_PREFS_KEY));
  afterEach(() => localStorage.removeItem(HISTORY_PREFS_KEY));

  it('is off by default', () => {
    expect(readStationary24hAlertPref()).toBeFalse();
  });

  it('round-trips between the Alerts page and GPS History', () => {
    storeStationary24hAlertPref(true);
    expect(readStationary24hAlertPref()).toBeTrue();
  });

  it('does not overwrite the other history page preferences', () => {
    storeHistoryPagePrefs({ stationaryOverOneHour: true, hideRecordsBefore: '2026-11-20', alertStationary24h: false });

    storeStationary24hAlertPref(true);

    expect(readHistoryPagePrefs()).toEqual({
      stationaryOverOneHour: true,
      hideRecordsBefore: '2026-11-20',
      alertStationary24h: true
    });
  });
});
