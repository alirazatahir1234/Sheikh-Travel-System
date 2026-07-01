/** Strips non-digit characters from integer-only fields. */
export function digitsOnlyIntegerInput(value: string): string {
  return value.replace(/\D/g, '');
}

/** Blocks non-digit keypresses (allows navigation/editing keys). */
export function blockNonDigitIntegerKey(event: KeyboardEvent): void {
  if (event.ctrlKey || event.metaKey || event.altKey) return;
  const allowed = ['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'];
  if (allowed.includes(event.key)) return;
  if (event.key.length === 1 && !/\d/.test(event.key)) {
    event.preventDefault();
  }
}

export function parseOptionalPositiveInt(value: string | number | null | undefined): number | null {
  if (value === null || value === undefined || value === '') return null;
  const n = typeof value === 'number' ? value : parseInt(String(value), 10);
  return Number.isFinite(n) && n >= 0 ? n : null;
}
