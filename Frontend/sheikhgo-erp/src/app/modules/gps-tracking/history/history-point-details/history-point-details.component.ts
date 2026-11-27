import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TripReplayPosition } from '../../../../core/models/gps-tracking.model';
import { formatClock, formatDay } from '../../utils/history-timeline.util';

/**
 * Right panel of the History workspace: the vehicle at the playhead. The clock and the
 * coordinates follow the playhead; the telemetry is that of the last GPS fix before it.
 */
@Component({
  selector: 'app-history-point-details',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './history-point-details.component.html',
  styleUrls: ['./history-point-details.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoryPointDetailsComponent {
  /** The playhead. Null when no route is loaded. */
  @Input() atMs: number | null = null;
  @Input() latitude: number | null = null;
  @Input() longitude: number | null = null;
  @Input() speedKmh: number | null = null;
  @Input() headingLabel = '—';
  /** The last fix at or before the playhead: ignition, battery, GSM and odometer come from it. */
  @Input() fix: TripReplayPosition | null = null;
  @Input() address = '';
  @Input() addressSecondary: string | null = null;
  @Input() motionLabel = '—';
  @Input() drivenSoFarKm: number | null = null;
  @Input() driverName = '—';
  @Input() timeZoneId: string | null = null;
  /** Short name of the zone the times are shown in, e.g. PKT. */
  @Input() zoneLabel = '';
  /** Period figures the strip does not carry. */
  @Input() engineHours: number | null = null;
  @Input() gpsPointCount: number | null = null;
  @Input() emptyMessage = 'The vehicle at the playhead appears here once a route is loaded.';

  @Output() closed = new EventEmitter<void>();
  @Output() openInMaps = new EventEmitter<void>();
  @Output() copyCoordinates = new EventEmitter<string>();

  get hasPoint(): boolean {
    return this.atMs != null && this.latitude != null && this.longitude != null;
  }

  get dateLabel(): string {
    if (this.atMs == null) return '';
    const day = formatDay(this.atMs, this.timeZoneId, true);
    return this.zoneLabel ? `${day} · ${this.zoneLabel}` : day;
  }

  get clockLabel(): string {
    return this.atMs == null ? '—' : formatClock(this.atMs, this.timeZoneId, true);
  }

  get coordinates(): string {
    return this.hasPoint ? `${this.latitude!.toFixed(5)}, ${this.longitude!.toFixed(5)}` : '';
  }

  get speedLabel(): string {
    return this.speedKmh == null ? '—' : `${Math.round(this.speedKmh)} km/h`;
  }

  get ignition(): 'on' | 'off' | 'unknown' {
    if (this.fix?.ignition == null) return 'unknown';
    return this.fix.ignition ? 'on' : 'off';
  }

  get batteryLabel(): string {
    return this.fix?.batteryLevel != null ? `${Math.round(this.fix.batteryLevel)}%` : '—';
  }

  get gsmLabel(): string {
    return this.fix?.satellites != null ? `${this.fix.satellites} (RSSI)` : '—';
  }

  get driverInitials(): string {
    const words = (this.driverName || '').trim().split(/\s+/).filter(w => /[\p{L}\p{N}]/u.test(w));
    if (!words.length) return '—';
    return words.slice(0, 2).map(w => w[0].toUpperCase()).join('');
  }

  get hasDriver(): boolean {
    return !!this.driverName && this.driverName !== '—';
  }
}
