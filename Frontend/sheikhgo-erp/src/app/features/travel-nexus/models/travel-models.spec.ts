import {
  ENQUIRY_STATUS_LABEL, ENQUIRY_STATUS_VARIANT, TRAVEL_ENQUIRY_STATUSES, TRAVEL_VISA_CASE_STATUSES, VISA_PIPELINE_STAGES,
  VISA_STATUS_LABEL, VISA_STATUS_VARIANT
} from './travel.models';
import { asUtc, fromDateTimeLocal, isDue, toDateInput } from './travel-date.util';

describe('TravelNexus models', () => {
  it('labels and badge variants cover every enquiry and visa status', () => {
    for (const s of TRAVEL_ENQUIRY_STATUSES) {
      expect(ENQUIRY_STATUS_LABEL[s]).withContext(s).toBeTruthy();
      expect(ENQUIRY_STATUS_VARIANT[s]).withContext(s).toBeTruthy();
    }
    for (const s of TRAVEL_VISA_CASE_STATUSES) {
      expect(VISA_STATUS_LABEL[s]).withContext(s).toBeTruthy();
      expect(VISA_STATUS_VARIANT[s]).withContext(s).toBeTruthy();
    }
  });

  it('pipeline strip shows every visa status exactly once (so the strip sums to the total)', () => {
    expect([...VISA_PIPELINE_STAGES].sort()).toEqual([...TRAVEL_VISA_CASE_STATUSES].sort());
  });

  it('date helpers keep date-only values and treat offset-less API timestamps as UTC', () => {
    expect(toDateInput('2026-12-01T00:00:00')).toBe('2026-12-01');
    expect(asUtc('2026-12-01T10:00:00')).toBe('2026-12-01T10:00:00Z');
    expect(asUtc('2026-12-01T10:00:00Z')).toBe('2026-12-01T10:00:00Z');
    expect(fromDateTimeLocal('')).toBeNull();
    expect(isDue('2020-01-01T00:00:00')).toBeTrue();
    expect(isDue(null)).toBeFalse();
  });
});
