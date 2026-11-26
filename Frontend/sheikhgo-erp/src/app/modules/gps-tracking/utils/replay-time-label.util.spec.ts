import { formatReplayTimeLabel } from './replay-time-label.util';

describe('formatReplayTimeLabel', () => {
  // 26 Nov 2026 02:29 UTC = 07:29 PKT
  const instant = '2026-11-26T02:29:00Z';
  const hour = 60 * 60 * 1000;

  it('shows time only within a single day', () => {
    const label = formatReplayTimeLabel(instant, 6 * hour, 'Asia/Karachi');
    expect(label).toMatch(/07:29/);
    expect(label).not.toMatch(/Nov/);
  });

  it('includes the date when the span exceeds 24 hours', () => {
    const label = formatReplayTimeLabel(instant, 7 * 24 * hour, 'Asia/Karachi');
    expect(label).toMatch(/Nov/);
    expect(label).toMatch(/26/);
    expect(label).toMatch(/07:29/);
  });
});
