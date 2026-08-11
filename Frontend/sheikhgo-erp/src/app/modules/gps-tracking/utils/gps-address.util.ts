/**
 * Shared reverse-geocode display helpers for GPS live map / history / playback.
 */

const PLUS_CODE_RE = /\b[23456789CFGHJMPQRVWX]{4,8}\+[23456789CFGHJMPQRVWX]{2,3}\b/i;

/** City/admin hierarchy, plus-code, or legacy "Near {POI}" lines that need a refresh. */
export function isCoarseAddress(address?: string | null): boolean {
  const raw = address?.trim();
  if (!raw) return true;
  const lower = raw.toLowerCase();
  if (lower.includes('tehsil') || lower.includes('district') || lower.includes('division')) {
    return true;
  }
  if (/^near\s+/i.test(raw)) return true;
  if (PLUS_CODE_RE.test(raw)) return true;
  return false;
}

/** Strip legacy Near-prefix and plus-code segments for fleet display. */
export function sanitizeFleetAddress(address?: string | null): string | null {
  let raw = address?.trim() || '';
  if (!raw) return null;
  if (/^near\s+/i.test(raw)) {
    const comma = raw.indexOf(',');
    raw = comma > 0 ? raw.slice(comma + 1).trim() : '';
  }
  const parts = raw
    .split(',')
    .map(p => p.trim())
    .filter(p => p && !PLUS_CODE_RE.test(p));
  if (parts.length === 0) return null;
  return parts.join(', ');
}

/**
 * Street/locality first. PlaceName is optional metadata — do not prefix "Near {place}".
 * Mirrors backend TripReplayAddressEnricher.FormatResolvedAddress.
 */
export function formatResolvedAddress(
  formatted?: string | null,
  _placeName?: string | null
): string | null {
  return sanitizeFleetAddress(formatted);
}

export interface AddressDisplayLines {
  /** Street / locality line shown first. */
  primary: string | null;
  /** City / province / country shown under the primary line. */
  secondary: string | null;
}

/**
 * Split a single formatted address into primary + secondary display lines.
 * Prefers street on the first line and locality underneath.
 */
export function splitDisplayAddress(address?: string | null): AddressDisplayLines {
  const raw = sanitizeFleetAddress(address);
  if (!raw) return { primary: null, secondary: null };

  const parts = raw.split(',').map(p => p.trim()).filter(Boolean);
  if (parts.length <= 2) {
    return { primary: raw, secondary: null };
  }

  const take = parts.length >= 4 ? 2 : 1;
  return {
    primary: parts.slice(0, take).join(', '),
    secondary: parts.slice(take).join(', ')
  };
}

/** Compact list-card address: first 1–2 segments. */
export function shortAddressLine(address?: string | null): string | null {
  const raw = sanitizeFleetAddress(address);
  if (!raw) return null;
  const parts = raw.split(',').map(p => p.trim()).filter(Boolean);
  if (parts.length === 0) return null;
  if (parts.length === 1) return parts[0];
  return `${parts[0]}, ${parts[1]}`;
}

/** Build "City, State, Country" from structured reverse-geocode fields. */
export function buildLocalityLine(parts: {
  city?: string | null;
  state?: string | null;
  country?: string | null;
}): string | null {
  const bits = [parts.city, parts.state, parts.country]
    .map(p => p?.trim())
    .filter((p): p is string => !!p);
  if (bits.length === 0) return null;
  const unique: string[] = [];
  for (const b of bits) {
    if (unique.length === 0 || unique[unique.length - 1].toLowerCase() !== b.toLowerCase()) {
      unique.push(b);
    }
  }
  return unique.join(', ');
}
