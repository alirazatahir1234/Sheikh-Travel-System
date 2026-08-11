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
  parked: { fill: '#8B5CF6', stroke: '#6D28D9' },
  offline: { fill: '#94A3B8', stroke: '#64748B' },
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
 * Top-down SVG silhouettes pointing "up" (north). Caller rotates the wrapper by heading.
 * Strong nose chevron makes heading obvious at 28–34px.
 */
function vehicleSvgPath(kind: FleetVehicleKind): string {
  const nose = `<path d="M16 1 L20 6 L12 6 Z" fill="#ffffff" stroke="none"/>`;
  switch (kind) {
    case 'truck':
      return `
        ${nose}
        <rect x="9" y="5" width="14" height="8" rx="1.5"/>
        <rect x="7" y="13" width="18" height="14" rx="1.5"/>
        <rect x="10" y="7" width="12" height="4" rx="1" fill="rgba(255,255,255,0.35)"/>
        <circle cx="11" cy="25" r="2" fill="rgba(0,0,0,0.35)"/>
        <circle cx="21" cy="25" r="2" fill="rgba(0,0,0,0.35)"/>`;
    case 'bus':
      return `
        ${nose}
        <rect x="8" y="5" width="16" height="22" rx="3"/>
        <rect x="10" y="8" width="12" height="3" rx="1" fill="rgba(255,255,255,0.35)"/>
        <rect x="10" y="13" width="12" height="2.5" rx="0.5" fill="rgba(255,255,255,0.25)"/>
        <rect x="10" y="17" width="12" height="2.5" rx="0.5" fill="rgba(255,255,255,0.25)"/>
        <circle cx="11" cy="25" r="1.8" fill="rgba(0,0,0,0.35)"/>
        <circle cx="21" cy="25" r="1.8" fill="rgba(0,0,0,0.35)"/>`;
    case 'van':
      return `
        ${nose}
        <path d="M8 11 L10 5 H22 L24 11 V24 H8 Z"/>
        <rect x="10" y="7" width="12" height="4" rx="1" fill="rgba(255,255,255,0.35)"/>
        <circle cx="11" cy="23" r="2" fill="rgba(0,0,0,0.35)"/>
        <circle cx="21" cy="23" r="2" fill="rgba(0,0,0,0.35)"/>`;
    case 'pickup':
      return `
        ${nose}
        <path d="M9 9 L11 4 H21 L23 9 V14 H25 V24 H7 V14 H9 Z"/>
        <rect x="11" y="5" width="10" height="3.5" rx="1" fill="rgba(255,255,255,0.35)"/>
        <rect x="9" y="14" width="14" height="5" rx="0.5" fill="rgba(0,0,0,0.12)"/>
        <circle cx="11" cy="23" r="2" fill="rgba(0,0,0,0.35)"/>
        <circle cx="21" cy="23" r="2" fill="rgba(0,0,0,0.35)"/>`;
    case 'suv':
      return `
        ${nose}
        <path d="M8 12 L10 6 H22 L24 12 V23 H8 Z"/>
        <rect x="10" y="7" width="12" height="4" rx="1" fill="rgba(255,255,255,0.35)"/>
        <circle cx="11" cy="22" r="2.2" fill="rgba(0,0,0,0.35)"/>
        <circle cx="21" cy="22" r="2.2" fill="rgba(0,0,0,0.35)"/>`;
    case 'motorcycle':
      return `
        ${nose}
        <circle cx="16" cy="9" r="2.5"/>
        <rect x="14.5" y="11" width="3" height="9" rx="1"/>
        <path d="M10 22 L16 14 L22 22" fill="none" stroke="#ffffff" stroke-width="1.5"/>
        <circle cx="11" cy="24" r="2.2" fill="rgba(0,0,0,0.35)"/>
        <circle cx="21" cy="24" r="2.2" fill="rgba(0,0,0,0.35)"/>`;
    case 'tractor':
      return `
        ${nose}
        <rect x="10" y="5" width="12" height="7" rx="1.5"/>
        <rect x="8" y="12" width="16" height="9" rx="1"/>
        <circle cx="12" cy="23" r="3" fill="rgba(0,0,0,0.4)"/>
        <circle cx="22" cy="21" r="2.2" fill="rgba(0,0,0,0.35)"/>`;
    case 'car':
    default:
      return `
        ${nose}
        <path d="M9 12 L11 5 H21 L23 12 V22 H9 Z"/>
        <rect x="11" y="7" width="10" height="4" rx="1" fill="rgba(255,255,255,0.4)"/>
        <path d="M11 12 H21" stroke="rgba(0,0,0,0.15)" stroke-width="1"/>
        <circle cx="11.5" cy="21.5" r="2.1" fill="rgba(0,0,0,0.35)"/>
        <circle cx="20.5" cy="21.5" r="2.1" fill="rgba(0,0,0,0.35)"/>`;
  }
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
  return 'idle';
}

/**
 * Compact rotating SVG vehicle DivIcon for Live Map.
 * Pulse rings sit outside the rotator so they stay screen-aligned.
 */
export function createFleetVehicleDivIcon(options: FleetVehicleMarkerOptions): LeafletTypes.DivIcon {
  const size = options.size ?? 30;
  const heading = options.heading != null && Number.isFinite(options.heading) ? options.heading : 0;
  const kind = resolveVehicleKind(options.vehicleType);
  const colors = STATUS_COLORS[options.status] ?? STATUS_COLORS.offline;
  const path = vehicleSvgPath(kind);
  const selected = !!options.selected;
  const pulse = !!options.pulse;
  const pad = pulse ? 12 : selected ? 8 : 4;
  const host = size + pad;

  const classes = [
    'fv-marker',
    `fv-marker--${options.status}`,
    selected ? 'fv-marker--selected' : '',
    pulse ? 'fv-marker--pulse' : ''
  ].filter(Boolean).join(' ');

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
        <div class="fv-rotator" style="transform:rotate(${heading}deg)">
          <svg class="fv-svg" viewBox="0 0 32 32" width="${size}" height="${size}" aria-hidden="true">
            <g fill="${colors.fill}" stroke="#ffffff" stroke-width="1.5" stroke-linejoin="round"
               paint-order="stroke fill">
              ${path}
            </g>
          </svg>
        </div>
        ${badgeHtml(options.badge)}
      </div>
    </div>`;

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
