import { L } from './leaflet-cluster';
import type * as LeafletTypes from 'leaflet';
import type { FleetTrackStatus } from '../models/gps-tracking.model';

export type FleetVehicleKind =
  | 'car'
  | 'suv'
  | 'van'
  | 'pickup'
  | 'truck'
  | 'bus'
  | 'motorcycle'
  | 'tractor';

export interface FleetVehicleMarkerOptions {
  status: FleetTrackStatus;
  /** Degrees clockwise from north (GPS course / heading). */
  heading?: number | null;
  vehicleType?: string | null;
  /** Show small badge: ignition | parked | sos | offline */
  badge?: 'ignition' | 'parked' | 'sos' | 'offline' | null;
  size?: number;
  /** Emphasize focused / selected vehicle. */
  selected?: boolean;
  /** Show decorative live GPS pulse ring (non-rotating). Selected only. */
  pulse?: boolean;
}

/** Enterprise fleet palette — keep in sync with Live Map pills / trails. */
const STATUS_COLORS: Record<FleetTrackStatus, { fill: string; stroke: string }> = {
  moving: { fill: '#2563EB', stroke: '#1D4ED8' },
  idle: { fill: '#F59E0B', stroke: '#B45309' },
  parked: { fill: '#0D9488', stroke: '#0F766E' },
  unknown: { fill: '#F59E0B', stroke: '#B45309' },
  offline: { fill: '#EF4444', stroke: '#B91C1C' },
  never_seen: { fill: '#94A3B8', stroke: '#64748B' },
  sos: { fill: '#DC2626', stroke: '#7F1D1D' },
  scheduled: { fill: '#3B82F6', stroke: '#1D4ED8' },
  delayed: { fill: '#EF4444', stroke: '#B91C1C' }
};

/** Normalize free-text vehicleType from ERP into a fleet icon kind. */
export function resolveVehicleKind(vehicleType?: string | null): FleetVehicleKind {
  const t = (vehicleType ?? '').toLowerCase();
  if (!t) return 'car';
  if (t.includes('motor') || t.includes('bike') || t.includes('scooter')) return 'motorcycle';
  if (t.includes('tractor') || t.includes('excavator') || t.includes('crane') || t.includes('construction')) {
    return 'tractor';
  }
  if (t.includes('bus') || t.includes('coaster')) return 'bus';
  if (t.includes('truck') || t.includes('trailer') || t.includes('lorry') || t.includes('heavy')) return 'truck';
  if (t.includes('pickup') || t.includes('hilux') || t.includes('ute')) return 'pickup';
  if (t.includes('van') || t.includes('minibus') || t.includes('ambulance')) return 'van';
  if (t.includes('suv') || t.includes('jeep') || t.includes('crossover')) return 'suv';
  return 'car';
}

/**
 * Status-flexible glyphs:
 * - moving / delayed / scheduled → heading chevron (rotates)
 * - idle → amber pause disc
 * - parked → teal "P" disc (reference fleet UI)
 * - offline / never_seen → muted disc
 * - sos → red alert disc
 */
function statusGlyphSvg(status: FleetTrackStatus): { paths: string; rotates: boolean } {
  switch (status) {
    case 'parked':
      return {
        rotates: false,
        paths: `
          <circle cx="16" cy="16" r="13"/>
          <text x="16" y="16.5" text-anchor="middle" dominant-baseline="central"
                fill="#ffffff" stroke="none" font-size="14" font-weight="800"
                font-family="system-ui,Segoe UI,sans-serif">P</text>`
      };
    case 'idle':
    case 'unknown':
      return {
        rotates: false,
        paths: `
          <circle cx="16" cy="16" r="13"/>
          <rect x="11" y="10" width="3.2" height="12" rx="1" fill="#ffffff" stroke="none"/>
          <rect x="17.8" y="10" width="3.2" height="12" rx="1" fill="#ffffff" stroke="none"/>`
      };
    case 'sos':
      return {
        rotates: false,
        paths: `
          <circle cx="16" cy="16" r="13"/>
          <text x="16" y="17" text-anchor="middle" dominant-baseline="central"
                fill="#ffffff" stroke="none" font-size="16" font-weight="900"
                font-family="system-ui,Segoe UI,sans-serif">!</text>`
      };
    case 'offline':
    case 'never_seen':
      return {
        rotates: false,
        paths: `
          <circle cx="16" cy="16" r="12"/>
          <circle cx="16" cy="16" r="4.5" fill="rgba(255,255,255,0.45)" stroke="none"/>`
      };
    case 'moving':
    case 'scheduled':
    case 'delayed':
    default:
      return {
        rotates: true,
        paths: `
          <path d="M16 1.5 L27.5 27.5 L16 21.5 L4.5 27.5 Z"/>
          <circle cx="16" cy="18.5" r="3.2" fill="rgba(255,255,255,0.4)" stroke="none"/>`
      };
  }
}

/** Shared inner HTML for Leaflet DivIcon and Google AdvancedMarker content. */
export function buildFleetVehicleMarkerInnerHtml(options: FleetVehicleMarkerOptions): {
  html: string;
  host: number;
  size: number;
} {
  const size = options.size ?? 32;
  const heading = options.heading != null && Number.isFinite(options.heading) ? options.heading : 0;
  const colors = STATUS_COLORS[options.status] ?? STATUS_COLORS.offline;
  const glyph = statusGlyphSvg(options.status);
  const rotateDeg = glyph.rotates ? heading : 0;
  const selected = !!options.selected;
  const pulse = !!options.pulse;
  const pad = pulse ? 12 : selected ? 8 : 4;
  const host = size + pad;

  // Letter/shape already encodes status — skip redundant parked/sos/offline corner badges.
  const badge =
    options.badge === 'parked' || options.badge === 'sos' || options.badge === 'offline'
      ? null
      : options.badge;

  const classes = [
    'fv-marker',
    `fv-marker--${options.status}`,
    glyph.rotates ? 'fv-marker--directional' : 'fv-marker--status-disc',
    selected ? 'fv-marker--selected' : '',
    pulse ? 'fv-marker--pulse' : ''
  ]
    .filter(Boolean)
    .join(' ');

  const pulseHtml = pulse
    ? `<span class="fv-pulse" style="--fv-pulse:${colors.fill}" aria-hidden="true">
         <span class="fv-pulse__ring"></span>
         <span class="fv-pulse__ring fv-pulse__ring--delay"></span>
       </span>`
    : '';

  const html = `
    <div class="${classes}" style="width:${host}px;height:${host}px;--fv-status:${colors.fill}">
      ${pulseHtml}
      <div class="fv-body" style="width:${size}px;height:${size}px">
        <div class="fv-rotator" style="transform:rotate(${rotateDeg}deg)">
          <svg class="fv-svg" viewBox="0 0 32 32" width="${size}" height="${size}" aria-hidden="true">
            <g fill="${colors.fill}" stroke="#ffffff" stroke-width="1.75" stroke-linejoin="round"
               paint-order="stroke fill">
              ${glyph.paths}
            </g>
          </svg>
        </div>
        ${badgeHtml(badge)}
      </div>
    </div>`;

  return { html, host, size };
}

function badgeHtml(badge: FleetVehicleMarkerOptions['badge']): string {
  if (!badge) return '';
  const map: Record<string, { label: string; bg: string }> = {
    ignition: { label: 'I', bg: '#0f766e' },
    parked: { label: 'P', bg: '#6D28D9' },
    sos: { label: '!', bg: '#dc2626' },
    offline: { label: '×', bg: '#64748b' }
  };
  const b = map[badge];
  if (!b) return '';
  return `<span class="fv-badge" style="background:${b.bg}">${b.label}</span>`;
}

export function resolveReplayStatus(speedKmh: number, ignition?: boolean | null): FleetTrackStatus {
  const speed = Number(speedKmh) || 0;
  if (speed >= 5) return 'moving';
  if (ignition === true) return 'idle';
  if (ignition === false) return 'parked';
  if (speed < 2) return 'parked';
  return 'unknown';
}

/**
 * Compact status-flexible DivIcon for Live Map / geofence overlays.
 * Pulse rings sit outside the rotator so they stay screen-aligned.
 */
export function createFleetVehicleDivIcon(options: FleetVehicleMarkerOptions): LeafletTypes.DivIcon {
  const { html, host, size } = buildFleetVehicleMarkerInnerHtml(options);
  return L.divIcon({
    className: 'fv-marker-host',
    html,
    iconSize: [host, host],
    iconAnchor: [host / 2, host / 2],
    popupAnchor: [0, -(size / 2) - 4]
  });
}

export function buildFleetVehiclePopup(fields: {
  name: string;
  plate?: string | null;
  driver?: string | null;
  tracker?: string | null;
  ignition?: boolean | null;
  speedKmh?: number | null;
  headingLabel?: string | null;
  address?: string | null;
  mapsUrl?: string | null;
  lastPing?: string | null;
  statusLabel?: string | null;
  /** Explicit GPS freshness line (preferred over lastPing alone). */
  gpsStatus?: string | null;
}): string {
  const esc = (s: string) =>
    s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  const ignition =
    fields.ignition === true ? 'ON' : fields.ignition === false ? 'OFF' : '—';
  const gpsLine = fields.gpsStatus?.trim() || fields.lastPing?.trim() || null;
  const lines: string[] = [
    `<div class="fv-popup">`,
    `<strong class="fv-popup__title">${esc(fields.name)}</strong>`
  ];
  if (fields.plate) lines.push(`<span class="fv-popup__plate">${esc(fields.plate)}</span>`);
  if (fields.driver) lines.push(`<span>Driver: ${esc(fields.driver)}</span>`);
  if (fields.tracker) lines.push(`<span>Tracker: ${esc(fields.tracker)}</span>`);
  if (fields.statusLabel) lines.push(`<span>${esc(fields.statusLabel)}</span>`);
  lines.push(`<span>Ignition: ${ignition}</span>`);
  if (fields.speedKmh != null) {
    const h = fields.headingLabel ? ` · ${esc(fields.headingLabel)}` : '';
    lines.push(`<span>${Math.round(fields.speedKmh)} km/h${h}</span>`);
  }
  if (gpsLine) {
    const tone = gpsLine.startsWith('Live GPS')
      ? 'fv-popup__gps--live'
      : gpsLine.startsWith('GPS position')
        ? 'fv-popup__gps--stale'
        : 'fv-popup__gps--none';
    lines.push(`<small class="fv-popup__gps ${tone}">${esc(gpsLine)}</small>`);
  }
  if (fields.address) {
    lines.push(`<span class="fv-popup__addr">📍 ${esc(fields.address)}</span>`);
    if (fields.mapsUrl) {
      lines.push(
        `<a class="fv-popup__maps" href="${esc(fields.mapsUrl)}" target="_blank" rel="noopener noreferrer">View on Google Maps</a>`
      );
    }
  }
  lines.push(`</div>`);
  return lines.join('');
}
