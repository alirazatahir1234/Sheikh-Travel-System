import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  Input,
  Output,
  ViewChild
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { A11yModule } from '@angular/cdk/a11y';
import { ConnectedPosition, OverlayModule } from '@angular/cdk/overlay';

/** One vehicle in the picker, already resolved for display (status, masked IMEI, compare tick). */
export interface HistoryVehicleCard {
  id: number;
  name: string;
  plate: string;
  driver: string;
  statusLabel: string;
  statusClass: 'online' | 'offline' | 'none';
  lastKnown: string;
  /** Tracker is not live: the last location may be cached. */
  cached: boolean;
  tracker: string;
  /** Null when no tracker is linked. Masked unless the user may see the full value. */
  imei: string | null;
  compare: boolean;
  active: boolean;
}

/**
 * Vehicle picker of the History toolbar: the button shows the vehicle in view, the panel holds
 * the vehicle list that used to fill the left column — search, Recent, status and compare.
 */
@Component({
  selector: 'app-history-vehicle-picker',
  standalone: true,
  imports: [CommonModule, OverlayModule, A11yModule],
  templateUrl: './history-vehicle-picker.component.html',
  styleUrls: ['./history-vehicle-picker.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class HistoryVehiclePickerComponent {
  @ViewChild('trigger') trigger?: ElementRef<HTMLButtonElement>;

  @Input() vehicleName = '';
  @Input() plate = '';
  @Input() driverName = '';
  @Input() recent: HistoryVehicleCard[] = [];
  @Input() vehicles: HistoryVehicleCard[] = [];
  /** Why the list is empty, when it is. */
  @Input() emptyMessage: string | null = null;
  @Input() search = '';
  @Input() stationaryOnly = false;
  @Input() compareCount = 0;
  @Input() compareLoading = false;
  @Input() compareMax = 4;

  @Output() searchChange = new EventEmitter<string>();
  @Output() stationaryOnlyChange = new EventEmitter<boolean>();
  @Output() vehicleSelect = new EventEmitter<number>();
  @Output() compareToggle = new EventEmitter<number>();
  @Output() compareClear = new EventEmitter<void>();
  @Output() openMap = new EventEmitter<number>();
  @Output() openLive = new EventEmitter<number>();

  open = false;

  readonly positions: ConnectedPosition[] = [
    { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top', offsetY: 6 },
    { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'bottom', offsetY: -6 }
  ];

  get title(): string {
    if (!this.vehicleName) return 'Select a vehicle';
    return this.plate ? `${this.vehicleName} · ${this.plate}` : this.vehicleName;
  }

  get subtitle(): string {
    if (!this.vehicleName) return 'No vehicle selected';
    return this.driverName && this.driverName !== '—' ? `Driver · ${this.driverName}` : 'No driver assigned';
  }

  get triggerLabel(): string {
    return this.vehicleName
      ? `Vehicle: ${this.vehicleName}${this.plate ? ', ' + this.plate : ''}. Change vehicle`
      : 'Select a vehicle';
  }

  toggle(): void {
    this.open = !this.open;
  }

  close(): void {
    if (!this.open) return;
    this.open = false;
    this.trigger?.nativeElement.focus();
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close();
    }
  }

  select(id: number): void {
    this.vehicleSelect.emit(id);
    this.close();
  }

  onSearch(event: Event): void {
    this.searchChange.emit((event.target as HTMLInputElement).value);
  }

  onStationary(event: Event): void {
    this.stationaryOnlyChange.emit((event.target as HTMLInputElement).checked);
  }

  trackCard(_: number, card: HistoryVehicleCard): number {
    return card.id;
  }
}
