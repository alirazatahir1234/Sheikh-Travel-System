import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges
} from '@angular/core';
import { VehicleLocation, GpsEta } from '../../../../core/models/gps-tracking.model';

export type DetailTab = 'overview' | 'telemetry' | 'trip' | 'more';

@Component({
  standalone: false,
  selector: 'app-vehicle-detail-panel',
  templateUrl: './vehicle-detail-panel.component.html',
  styleUrls: ['./vehicle-detail-panel.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class VehicleDetailPanelComponent implements OnChanges {
  @Input() loc: VehicleLocation | null = null;
  @Input() dualStatus = '';
  @Input() statusBadge = '';
  @Input() statusIconName = 'directions_car';
  @Input() speedText = '—';
  @Input() headingText: string | null = null;
  @Input() gpsSignalText = '—';
  @Input() lastPingText = '—';
  @Input() locationPrimary: string | null = null;
  @Input() locationSecondary: string | null = null;
  @Input() addressResolving = false;
  @Input() mapsUrl = '';
  @Input() streetViewSrc: string | null = null;
  @Input() operationalLine = '';
  @Input() temperatureText: string | null = null;
  @Input() eta: GpsEta | null = null;
  @Input() followSelected = false;
  @Input() activeTab: DetailTab = 'overview';
  /** SUPER_ADMIN only — hide device command actions for fleet owners/managers. */
  @Input() canCommands = false;

  @Output() tabChange = new EventEmitter<DetailTab>();
  @Output() track = new EventEmitter<void>();
  @Output() follow = new EventEmitter<void>();
  @Output() command = new EventEmitter<void>();
  @Output() history = new EventEmitter<void>();
  @Output() profile = new EventEmitter<number>();
  @Output() closePanel = new EventEmitter<void>();
  @Output() streetViewError = new EventEmitter<number>();

  /** Collapses body/tabs/actions; header stays visible under the map. */
  collapsed = false;

  readonly tabs: { id: DetailTab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'telemetry', label: 'Telemetry' },
    { id: 'trip', label: 'Trip' },
    { id: 'more', label: 'More' }
  ];

  ngOnChanges(changes: SimpleChanges): void {
    const locChange = changes['loc'];
    if (!locChange || locChange.firstChange) return;
    const prev = locChange.previousValue as VehicleLocation | null;
    const next = locChange.currentValue as VehicleLocation | null;
    if (prev?.vehicleId !== next?.vehicleId) {
      this.collapsed = false;
    }
  }

  setTab(tab: DetailTab): void {
    this.tabChange.emit(tab);
  }

  toggleCollapsed(): void {
    this.collapsed = !this.collapsed;
  }

  /** True when the selected vehicle has a dialable/WhatsApp-able driver phone. */
  get hasDriverPhone(): boolean {
    return !!this.driverWhatsAppDigits && !!this.driverTelHref;
  }

  get driverTelHref(): string | null {
    const cleaned = this.normalizeTel(this.loc?.driverPhone);
    return cleaned ? `tel:${cleaned}` : null;
  }

  get driverWhatsAppHref(): string | null {
    const digits = this.driverWhatsAppDigits;
    return digits ? `https://wa.me/${digits}` : null;
  }

  private get driverWhatsAppDigits(): string | null {
    return this.normalizeWhatsAppDigits(this.loc?.driverPhone);
  }

  /** Strip formatting; keep leading + for the device dialer. */
  private normalizeTel(phone: string | null | undefined): string | null {
    const raw = (phone ?? '').trim();
    if (!raw) return null;
    const cleaned = raw.replace(/[^\d+]/g, '');
    return cleaned.length >= 7 ? cleaned : null;
  }

  /**
   * Digits for wa.me — converts common PK local mobiles (`03XXXXXXXXX`) to
   * `923XXXXXXXXX` and strips a leading +.
   */
  private normalizeWhatsAppDigits(phone: string | null | undefined): string | null {
    const raw = (phone ?? '').trim();
    if (!raw) return null;
    let digits = raw.replace(/[^\d+]/g, '');
    if (digits.startsWith('+')) digits = digits.slice(1);
    if (digits.startsWith('00')) digits = digits.slice(2);
    if (digits.length === 11 && digits.startsWith('0')) {
      digits = `92${digits.slice(1)}`;
    }
    return digits.length >= 10 ? digits : null;
  }
}
