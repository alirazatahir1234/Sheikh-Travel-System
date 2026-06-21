/** Parse API date strings safely (date-only uses noon local to avoid TZ shifts). */
export function parseApiDate(value: Date | string | null | undefined): Date | null {
  if (value == null || value === '') return null;
  if (value instanceof Date) return isNaN(value.getTime()) ? null : value;
  const s = String(value).trim();
  const dateOnly = s.match(/^(\d{4}-\d{2}-\d{2})$/);
  if (dateOnly) return new Date(`${dateOnly[1]}T12:00:00`);
  const d = new Date(s);
  return isNaN(d.getTime()) ? null : d;
}

export function formatAbsoluteDateTime(value: Date | string | null | undefined): string | null {
  const d = value instanceof Date ? value : parseApiDate(value);
  if (!d) return null;
  return new Intl.DateTimeFormat(undefined, {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(d);
}

/** Human-readable relative time; falls back to absolute for older dates. */
export function formatRelativeTime(from: Date | string | null | undefined, now = new Date()): string | null {
  const d = from instanceof Date ? from : parseApiDate(from);
  if (!d) return null;

  const sec = Math.floor((now.getTime() - d.getTime()) / 1000);
  if (sec < 0) return formatAbsoluteDateTime(d);

  if (sec < 10) return 'just now';
  if (sec < 60) return `${sec} second${sec === 1 ? '' : 's'} ago`;
  const min = Math.floor(sec / 60);
  if (min < 60) return `${min} minute${min === 1 ? '' : 's'} ago`;
  const hr = Math.floor(min / 60);
  if (hr < 24) return `${hr} hour${hr === 1 ? '' : 's'} ago`;
  const days = Math.floor(hr / 24);
  if (days < 7) return `${days} day${days === 1 ? '' : 's'} ago`;

  return formatAbsoluteDateTime(d);
}

export function toDateInputValue(value: Date | string | null | undefined): string {
  if (value == null || value === '') return '';
  if (typeof value === 'string') {
    const isoDate = value.match(/^(\d{4}-\d{2}-\d{2})/);
    if (isoDate) return isoDate[1];
  }
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
