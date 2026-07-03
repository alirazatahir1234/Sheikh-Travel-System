import { speedToColor } from './speed-heatmap.util';

function redChannel(color: string): number {
  const match = color.match(/^rgb\((\d+), (\d+), (\d+)\)$/);
  if (!match) throw new Error(`Unexpected color format: ${color}`);
  return Number(match[1]);
}

describe('speedToColor', () => {
  it('returns the exact green stop at 0 km/h', () => {
    expect(speedToColor(0)).toBe('rgb(16, 185, 129)');
  });

  it('returns the exact amber stop at 60 km/h', () => {
    expect(speedToColor(60)).toBe('rgb(245, 158, 11)');
  });

  it('returns the exact red stop at 120 km/h and above', () => {
    expect(speedToColor(120)).toBe('rgb(220, 38, 38)');
  });

  it('clamps negative speeds to the green stop', () => {
    expect(speedToColor(-10)).toBe(speedToColor(0));
  });

  it('clamps speeds above 120 km/h to the red stop', () => {
    expect(speedToColor(500)).toBe(speedToColor(120));
  });

  it('treats NaN as 0 km/h', () => {
    expect(speedToColor(NaN)).toBe(speedToColor(0));
  });

  it('always returns a valid rgb() string', () => {
    expect(speedToColor(35)).toMatch(/^rgb\(\d+, \d+, \d+\)$/);
  });

  it('shifts toward red as speed increases', () => {
    expect(redChannel(speedToColor(120))).toBeGreaterThan(redChannel(speedToColor(0)));
    expect(redChannel(speedToColor(90))).toBeGreaterThan(redChannel(speedToColor(30)));
  });
});
