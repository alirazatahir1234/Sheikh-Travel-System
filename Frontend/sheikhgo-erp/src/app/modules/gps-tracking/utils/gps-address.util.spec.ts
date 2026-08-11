import {
  splitDisplayAddress,
  formatResolvedAddress,
  isCoarseAddress,
  sanitizeFleetAddress
} from './gps-address.util';

describe('gps-address.util', () => {
  it('detects coarse Near/plus-code/tehsil strings but accepts city locality', () => {
    expect(isCoarseAddress('Pasrur, Punjab, Pakistan')).toBe(false);
    expect(isCoarseAddress('Near Mandar, 7M78+84W, Pasrur')).toBe(true);
    expect(isCoarseAddress('7M78+84W, Walled City, Pasrur, Pakistan')).toBe(true);
    expect(isCoarseAddress('Pasrur, Pasrur Tehsil, Sialkot District')).toBe(true);
    expect(isCoarseAddress('Circular Road, Sialkot, Punjab, Pakistan')).toBe(false);
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
