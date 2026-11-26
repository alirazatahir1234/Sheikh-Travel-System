/**
 * One plate format at entry: trimmed, upper-case, any run of spaces, hyphens or underscores
 * as a single hyphen ("bwy 217" → "BWY-217"). Mirrors PlateNumberNormalizer on the API, which
 * applies the same rule on save. Existing vehicles are not rewritten until they are edited.
 */
export function normalizePlateNumber(value: string | null | undefined): string {
  const trimmed = (value ?? '').trim();
  if (!trimmed) return '';
  return trimmed
    .replace(/[\s\-_‐-―]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .toUpperCase();
}
