import { maskImei } from './imei-mask.util';

describe('maskImei', () => {
  it('shows only the last four digits', () => {
    expect(maskImei('864943041230598')).toBe('•••• 0598');
  });

  it('never returns the full identifier', () => {
    const imei = '864943041230598';
    expect(maskImei(imei)).not.toContain(imei.slice(0, 11));
  });

  it('handles short and padded values', () => {
    expect(maskImei(' 0598 ')).toBe('•••• 0598');
    expect(maskImei('98')).toBe('•••• 98');
  });

  it('returns empty for a missing value', () => {
    expect(maskImei(null)).toBe('');
    expect(maskImei(undefined)).toBe('');
    expect(maskImei('  ')).toBe('');
  });
});
