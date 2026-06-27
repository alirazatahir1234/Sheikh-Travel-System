/** Strips non-digit characters from phone input. */
export function digitsOnlyPhoneInput(value: string): string {
  return value.replace(/\D/g, '');
}

/** Blocks non-digit keypresses in phone fields (allows navigation/editing keys). */
export function blockNonDigitPhoneKey(event: KeyboardEvent): void {
  if (event.ctrlKey || event.metaKey || event.altKey) return;
  const allowed = ['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'];
  if (allowed.includes(event.key)) return;
  if (event.key.length === 1 && !/\d/.test(event.key)) {
    event.preventDefault();
  }
}
