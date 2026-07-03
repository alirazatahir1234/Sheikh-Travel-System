/**
 * Green→amber→red two-stage speed gradient for route replay polylines, clamped to 0–120 km/h.
 */
const STOPS: { speed: number; hex: string }[] = [
  { speed: 0, hex: '#10b981' },
  { speed: 60, hex: '#f59e0b' },
  { speed: 120, hex: '#dc2626' }
];

function hexToRgb(hex: string): [number, number, number] {
  const n = parseInt(hex.slice(1), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

function lerpColor(fromHex: string, toHex: string, t: number): string {
  const [r1, g1, b1] = hexToRgb(fromHex);
  const [r2, g2, b2] = hexToRgb(toHex);
  const r = Math.round(r1 + (r2 - r1) * t);
  const g = Math.round(g1 + (g2 - g1) * t);
  const b = Math.round(b1 + (b2 - b1) * t);
  return `rgb(${r}, ${g}, ${b})`;
}

export function speedToColor(speedKmh: number): string {
  const maxSpeed = STOPS[STOPS.length - 1].speed;
  const speed = Math.max(0, Math.min(maxSpeed, speedKmh || 0));
  const [lo, hi] = speed <= STOPS[1].speed ? [STOPS[0], STOPS[1]] : [STOPS[1], STOPS[2]];
  const t = hi.speed === lo.speed ? 0 : (speed - lo.speed) / (hi.speed - lo.speed);
  return lerpColor(lo.hex, hi.hex, t);
}

export const SPEED_HEATMAP_LEGEND = STOPS.map(s => ({ speed: s.speed, color: s.hex }));
