/** Format for `<input type="date">` (YYYY-MM-DD). */
export function toDateInputValue(value: Date | string | null | undefined): string {
  if (value == null || value === '') return '';
  const d = value instanceof Date ? value : new Date(value);
  if (isNaN(d.getTime())) return '';
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/** Today's date for `<input type="date" min="…">` (local timezone). */
export function todayDateInputValue(): string {
  return toDateInputValue(new Date());
}

/** Format for `<input type="datetime-local">` (YYYY-MM-DDTHH:mm). */
export function toDateTimeLocalValue(value: Date | string | null | undefined): string {
  if (value == null || value === '') return '';
  const d = value instanceof Date ? value : new Date(value);
  if (isNaN(d.getTime())) return '';
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  const h = String(d.getHours()).padStart(2, '0');
  const min = String(d.getMinutes()).padStart(2, '0');
  return `${y}-${m}-${day}T${h}:${min}`;
}

/** Parse `<input type="date">` value to ISO string (start of local day). */
export function dateInputToIso(value: string | Date | null | undefined): string | null {
  if (value == null || value === '') return null;
  if (value instanceof Date) return isNaN(value.getTime()) ? null : value.toISOString();
  return new Date(`${value}T00:00:00`).toISOString();
}

/** Parse `<input type="datetime-local">` value to ISO string. */
export function dateTimeLocalToIso(value: string | Date | null | undefined): string | null {
  if (value == null || value === '') return null;
  if (value instanceof Date) return isNaN(value.getTime()) ? null : value.toISOString();
  const d = new Date(value);
  return isNaN(d.getTime()) ? null : d.toISOString();
}
