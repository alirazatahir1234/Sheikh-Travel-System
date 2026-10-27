import {
  formatNonMovingStandingEnglish,
  nonMovingExceedsAlertThreshold,
  NON_MOVING_ALERT_MINUTES
} from './non-moving-duration.util';

describe('formatNonMovingStandingEnglish', () => {
  it('formats minutes only', () => {
    expect(formatNonMovingStandingEnglish(18)).toBe('Standing for 18 minutes');
  });

  it('formats whole hours', () => {
    expect(formatNonMovingStandingEnglish(22 * 60)).toBe('Standing for 22 hours');
  });

  it('formats hours and minutes', () => {
    expect(formatNonMovingStandingEnglish(21 * 60 + 57)).toBe(
      'Standing for 21 hours 57 minutes'
    );
    expect(formatNonMovingStandingEnglish(22 * 60 + 18)).toBe(
      'Standing for 22 hours 18 minutes'
    );
  });

  it('formats days', () => {
    expect(formatNonMovingStandingEnglish(2 * 24 * 60)).toBe('Standing for 2 days');
  });
});

describe('nonMovingExceedsAlertThreshold', () => {
  it('is false at exactly 22 hours', () => {
    expect(nonMovingExceedsAlertThreshold(NON_MOVING_ALERT_MINUTES)).toBe(false);
  });

  it('is true above 22 hours', () => {
    expect(nonMovingExceedsAlertThreshold(NON_MOVING_ALERT_MINUTES + 1)).toBe(true);
  });
});
