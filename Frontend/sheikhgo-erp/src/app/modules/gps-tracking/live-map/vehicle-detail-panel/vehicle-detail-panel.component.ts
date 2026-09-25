import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output
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
export class VehicleDetailPanelComponent {
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

  @Output() tabChange = new EventEmitter<DetailTab>();
  @Output() track = new EventEmitter<void>();
  @Output() follow = new EventEmitter<void>();
  @Output() command = new EventEmitter<void>();
  @Output() history = new EventEmitter<void>();
  @Output() profile = new EventEmitter<number>();
  @Output() streetViewError = new EventEmitter<number>();

  readonly tabs: { id: DetailTab; label: string }[] = [
    { id: 'overview', label: 'Overview' },
    { id: 'telemetry', label: 'Telemetry' },
    { id: 'trip', label: 'Trip' },
    { id: 'more', label: 'More' }
  ];

  setTab(tab: DetailTab): void {
    this.tabChange.emit(tab);
  }
}
