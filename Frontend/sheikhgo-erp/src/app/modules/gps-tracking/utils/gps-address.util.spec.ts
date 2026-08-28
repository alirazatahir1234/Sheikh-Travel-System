import {
  splitDisplayAddress,
  formatResolvedAddress,
  isCoarseAddress,
  sanitizeFleetAddress
} from './gps-address.util';

describe('gps-address.util', () => {
  it('detects coarse Near/plus-code/tehsil strings and locality-only placeholders', () => {
    expect(isCoarseAddress('Pasrur, Punjab, Pakistan')).toBe(true);
    expect(isCoarseAddress('Near Mandar, 7M78+84W, Pasrur')).toBe(true);
    expect(isCoarseAddress('7M78+84W, Walled City, Pasrur, Pakistan')).toBe(true);
    expect(isCoarseAddress('Pasrur, Pasrur Tehsil, Sialkot District')).toBe(true);
    expect(isCoarseAddress('Circular Road, Sialkot, Punjab, Pakistan')).toBe(false);
    // Non-ASCII script (Urdu) and diacritics are valid addresses — not coarse.
    expect(isCoarseAddress('سیالکوٹ روڈ، پسرور')).toBe(false);
    expect(isCoarseAddress('Sīālkot Road, Pasrūr')).toBe(false);
    expect(isCoarseAddress(null)).toBe(true);
  });

  it('formats street-first without Near prefix', () => {
    expect(formatResolvedAddress('Circular Road, Sialkot', 'Ali Store')).toBe(
      'Circular Road, Sialkot'
    );
    expect(formatResolvedAddress('Near Shop, Circular Road, Sialkot', 'Shop')).toBe(
      'Circular Road, Sialkot'
    );
    expect(formatResolvedAddress('7M78+84W, Walled City, Pasrur', null)).toBe(
      'Walled City, Pasrur'
    );
  });

  it('sanitizes legacy Near and plus-code lines', () => {
    expect(sanitizeFleetAddress('Near Mandar hari ram, 7M78+84W, Walled City, Pasrur')).toBe(
      'Walled City, Pasrur'
    );
  });

  it('strips diacritics for plain English fleet display', () => {
    expect(sanitizeFleetAddress('Sīālkot Road, Pasrūr, Punjāb')).toBe(
      'Sialkot Road, Pasrur, Punjab'
    );
  });

  it('splits street-first display lines', () => {
    expect(splitDisplayAddress('Circular Road, Sialkot, Punjab, Pakistan')).toEqual({
      primary: 'Circular Road, Sialkot',
      secondary: 'Punjab, Pakistan'
    });
    expect(splitDisplayAddress('Near Shop, Circular Road, Sialkot, Punjab')).toEqual({
      primary: 'Circular Road',
      secondary: 'Sialkot, Punjab'
    });
  });
});
