/**
 * Shared reverse-geocode display helpers for GPS live map / history / playback.
 */

const PLUS_CODE_RE = /\b[23456789CFGHJMPQRVWX]{4,8}\+[23456789CFGHJMPQRVWX]{2,3}\b/i;
const LOCALITY_ONLY_TOKEN_RE =
  /\b(tehsil|district|division|province|region|state|city|town|village|pakistan|india|punjab|sindh|khyber|balochistan|pk)\b/i;
const ROAD_TOKEN_RE =
  /\b(road|rd\.?|street|st\.?|avenue|ave\.?|boulevard|blvd\.?|lane|ln\.?|drive|dr\.?|highway|hwy\.?|motorway|bypass|chowk|nagar|gt\s*road|g\.?t\.?\s*road|ring road|service road|link road|circular road|pasrur road|sialkot road)\b/i;

function removeDiacritics(input: string): string {
  return input.normalize('NFD').replace(/[\u0300-\u036f]/g, '');
}

/** True when a segment looks like a street / highway rather than a shop name. */
export function looksLikeRoadSegment(part?: string | null): boolean {
  const raw = part?.trim();
  if (!raw) return false;
  if (LOCALITY_ONLY_TOKEN_RE.test(raw) && !ROAD_TOKEN_RE.test(raw) && !/[0-9]/.test(raw)) {
    return false;
  }
  if (/[0-9]/.test(raw)) return true;
  return ROAD_TOKEN_RE.test(raw);
}

/**
 * Returns true when the address is an admin-area placeholder or
 * machine-generated token that needs a proper geocode lookup.
 *
 * NOTE: Non-ASCII script (Urdu/Arabic) is valid — some reverse geocoders
 * return transliterated names and it must NOT be treated as coarse.
 */
export function isCoarseAddress(address?: string | null): boolean {
  const raw = address?.trim();
  if (!raw) return true;
  // Non-ASCII script (Urdu/Arabic) is a valid geocoder result — do not thrash refresh.
  if (/[^\u0000-\u007f]/.test(raw) && !PLUS_CODE_RE.test(raw)) {
    return false;
  }
  const lower = raw.toLowerCase();
  // Admin keywords that indicate a district/tehsil level placeholder
  if (lower.includes('tehsil') || lower.includes('district') || lower.includes('division')) {
    return true;
  }
  if (/^near\s+/i.test(raw)) return true;
  if (PLUS_CODE_RE.test(raw)) return true;
  // Pure coordinates stored as address string
  if (/^-?\d+\.\d+\s*,\s*-?\d+\.\d+$/.test(raw)) return true;
  const parts = raw.split(',').map(p => p.trim()).filter(Boolean);
  if (parts.length === 0) return true;

  // Lone business / POI name with no road signal → refresh for street-level result.
  if (parts.length === 1 && !looksLikeRoadSegment(parts[0])) {
    return true;
  }

  if (!parts.some(looksLikeRoadSegment) && !parts.some(p => /[0-9]/.test(p))) {
    // Locality-only address ("Pasrur, Punjab, Pakistan") is not exact enough for fleet UI.
    if (parts.length <= 3) {
      const allLocalityLike = parts.every(part => {
        const p = part.toLowerCase();
        if (LOCALITY_ONLY_TOKEN_RE.test(p)) return true;
        if (/^[a-z]{2,3}$/i.test(p)) return true; // country codes like PK
        if (!/[0-9\/-]/.test(p) && p.split(/\s+/).length <= 2) return true;
        return false;
      });
      if (allLocalityLike) return true;
    }
    return true;
  }
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
    .filter(p => p && !PLUS_CODE_RE.test(p) && !/^near\s+/i.test(p));
  if (parts.length === 0) return null;
  return removeDiacritics(parts.join(', '));
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

  let parts = raw.split(',').map(p => p.trim()).filter(Boolean);
  if (parts.length === 0) return { primary: null, secondary: null };

  // Prefer a road-like segment as primary; skip leading POI/business names.
  const roadIdx = parts.findIndex(looksLikeRoadSegment);
  if (roadIdx > 0) {
    parts = parts.slice(roadIdx);
  } else if (roadIdx < 0) {
    // No street signal — never promote a business/POI name as the street line.
    if (parts.length === 1) {
      return { primary: null, secondary: parts[0] };
    }
    // "Peak Performance Partners, Sialkot, Punjab" → locality primary, POI secondary.
    const localityLike = parts.filter(part => {
      const p = part.toLowerCase();
      if (LOCALITY_ONLY_TOKEN_RE.test(p)) return true;
      if (/^[a-z]{2,3}$/i.test(p)) return true;
      return !/[0-9\/-]/.test(p) && p.split(/\s+/).length <= 2;
    });
    if (localityLike.length > 0 && localityLike.length < parts.length) {
      return {
        primary: localityLike.join(', '),
        secondary: parts.find(p => !localityLike.includes(p)) || null
      };
    }
    return { primary: null, secondary: parts[0] };
  }

  if (parts.length <= 2) {
    return { primary: parts.join(', '), secondary: null };
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

export interface ReverseGeocodeDisplay {
  formattedAddress?: string | null;
  placeName?: string | null;
  primaryAddress?: string | null;
  nearbyPlaceName?: string | null;
  localityLine?: string | null;
}

/** Canonical fleet display string from structured reverse-geocode payload. */
export function formatFleetDisplayAddress(info?: ReverseGeocodeDisplay | null): string | null {
  if (!info) return null;
  const primary = info.primaryAddress?.trim();
  const locality = info.localityLine?.trim();
  const near =
    info.nearbyPlaceName?.trim() ||
    info.placeName?.trim() ||
    null;
  // Never persist/display a lone POI as the street address.
  if (primary && near && primary.toLowerCase() === near.toLowerCase()) {
    return locality || formatResolvedAddress(info.formattedAddress, info.placeName);
  }
  if (primary) return locality ? `${primary}, ${locality}` : primary;
  if (locality) return locality;
  return formatResolvedAddress(info.formattedAddress, info.placeName);
}
