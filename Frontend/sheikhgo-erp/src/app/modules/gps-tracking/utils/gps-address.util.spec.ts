import {
  splitDisplayAddress,
  formatResolvedAddress,
  isCoarseAddress,
  sanitizeFleetAddress,
  formatGpsTripLocationLine,
  resolveTripEndpointLabel
} from './gps-address.util';

describe('gps-address.util', () => {
  it('detects coarse Near/plus-code/tehsil strings and locality-only placeholders', () => {
    expect(isCoarseAddress('Pasrur, Punjab, Pakistan')).toBe(true);
    expect(isCoarseAddress('Near Mandar, 7M78+84W, Pasrur')).toBe(true);
    expect(isCoarseAddress('7M78+84W, Walled City, Pasrur, Pakistan')).toBe(true);
    expect(isCoarseAddress('Pasrur, Pasrur Tehsil, Sialkot District')).toBe(true);
    expect(isCoarseAddress('Peak Performance Partners')).toBe(true);
    expect(isCoarseAddress('Peak Performance Partners, Sialkot, Punjab')).toBe(true);
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

  it('formats GPS trip endpoints from coordinates when address is missing', () => {
    expect(resolveTripEndpointLabel(null, 32.26261, 74.66339)).toBe('32.26261, 74.66339');
    expect(resolveTripEndpointLabel('Unknown', 32.1, 74.3)).toBe('32.10000, 74.30000');
    expect(
      formatGpsTripLocationLine({
        startAddress: null,
        endAddress: 'unknown',
        startLatitude: 32.1,
        startLongitude: 74.3,
        endLatitude: 32.2,
        endLongitude: 74.4
      })
    ).toBe('32.10000, 74.30000 → 32.20000, 74.40000');
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
    expect(splitDisplayAddress('Peak Performance Partners')).toEqual({
      primary: null,
      secondary: 'Peak Performance Partners'
    });
    expect(splitDisplayAddress('Peak Performance Partners, Sialkot, Punjab')).toEqual({
      primary: 'Sialkot, Punjab',
      secondary: 'Peak Performance Partners'
    });
    expect(splitDisplayAddress('Gujranwala City Tehsil, Punjab')).toEqual({
      primary: 'Gujranwala City Tehsil, Punjab',
      secondary: null
    });
    expect(splitDisplayAddress('Gujranwala City Tehsil, Gujranwala, Punjab, Pakistan')).toEqual({
      primary: 'Gujranwala City Tehsil, Gujranwala',
      secondary: 'Punjab, Pakistan'
    });
  });
});