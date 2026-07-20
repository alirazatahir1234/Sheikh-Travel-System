/** Strips non-digit characters from integer-only fields. */
export function digitsOnlyIntegerInput(value: string): string {
  return value.replace(/\D/g, '');
}

/** Blocks non-digit keypresses (allows navigation/editing keys). */
export function blockNonDigitIntegerKey(event: KeyboardEvent): void {
  if (event.ctrlKey || event.metaKey || event.altKey) return;
  const allowed = ['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'];
  if (allowed.includes(event.key)) return;
  // Block letters including scientific-notation "e"/"E", signs, and decimals.
  if (event.key.length === 1 && !/\d/.test(event.key)) {
    event.preventDefault();
  }
}

export function parseOptionalPositiveInt(value: string | number | null | undefined): number | null {
  if (value === null || value === undefined || value === '') return null;

  if (typeof value === 'number') {
    if (!Number.isFinite(value) || value < 0 || !Number.isInteger(value)) return null;
    return value;
  }

  const raw = String(value).trim();
  // Reject scientific notation / mixed alphanumeric (e.g. "17E342353445").
  if (!/^\d+$/.test(raw)) return null;

  const n = Number(raw);
  return Number.isSafeInteger(n) && n >= 0 ? n : null;
}
