import {
  buildFleetVehicleMarkerInnerHtml,
  buildFleetVehiclePopup,
  resolveReplayStatus,
  resolveVehicleKind,
  type FleetVehicleKind,
  type FleetVehicleMarkerOptions
} from '../leaflet/fleet-vehicle-marker';

export { buildFleetVehiclePopup, resolveReplayStatus, resolveVehicleKind };
export type { FleetVehicleKind, FleetVehicleMarkerOptions };

/**
 * Build an HTMLElement for AdvancedMarkerElement.content.
 * Status-flexible glyphs (chevron / P / pause / alert) — same markup as Leaflet.
 */
export function createFleetVehicleMarkerElement(options: FleetVehicleMarkerOptions): HTMLElement {
  const { html } = buildFleetVehicleMarkerInnerHtml(options);
  const wrap = document.createElement('div');
  wrap.className = 'fv-marker-host';
  wrap.innerHTML = html;
  return wrap;
}
