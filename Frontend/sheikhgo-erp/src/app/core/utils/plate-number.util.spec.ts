import { normalizePlateNumber } from './plate-number.util';

describe('normalizePlateNumber', () => {
  const cases: [string | null | undefined, string][] = [
    ['BWY 217', 'BWY-217'],
    ['LEC-8825', 'LEC-8825'],
    ['  lec  8825 ', 'LEC-8825'],
    ['lea - 19 - 1234', 'LEA-19-1234'],
    ['ABC_123', 'ABC-123'],
    ['ABC–123', 'ABC-123'],
    ['-ABC-123-', 'ABC-123'],
    ['ABC123', 'ABC123'],
    ['', ''],
    ['   ', ''],
    [null, ''],
    [undefined, '']
  ];

  cases.forEach(([input, expected]) => {
    it(`normalises ${JSON.stringify(input)} to ${JSON.stringify(expected)}`, () => {
      expect(normalizePlateNumber(input)).toBe(expected);
    });
  });

  it('treats the space and hyphen forms as the same plate', () => {
    expect(normalizePlateNumber('BWY 217')).toBe(normalizePlateNumber('bwy-217'));
  });
});
