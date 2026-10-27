import {
  formatNonMovingStandingUrdu,
  nonMovingExceedsAlertThreshold,
  NON_MOVING_ALERT_MINUTES
} from './non-moving-duration.util';

describe('formatNonMovingStandingUrdu', () => {
  it('formats minutes only', () => {
    expect(formatNonMovingStandingUrdu(18)).toBe('18 منٹ سے کھڑی ہے');
  });

  it('formats whole hours', () => {
    expect(formatNonMovingStandingUrdu(22 * 60)).toBe('22 گھنٹے سے کھڑی ہے');
  });

  it('formats hours and minutes', () => {
    expect(formatNonMovingStandingUrdu(22 * 60 + 18)).toBe('22 گھنٹے 18 منٹ سے کھڑی ہے');
  });

  it('formats days', () => {
    expect(formatNonMovingStandingUrdu(2 * 24 * 60)).toBe('2 دن سے کھڑی ہے');
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
