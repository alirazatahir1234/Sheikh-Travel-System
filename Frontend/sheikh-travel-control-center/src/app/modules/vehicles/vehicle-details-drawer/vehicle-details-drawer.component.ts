import { CommonModule, DatePipe, DecimalPipe } from '@angular/common';
import { Component, effect, inject, input, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, of } from 'rxjs';
import { VehicleService } from '../../../core/services/vehicle.service';
import { DriverService } from '../../../core/services/driver.service';
import { GpsTrackingService } from '../../../core/services/gps-tracking.service';
import {
  Vehicle,
  VehicleStatus,
  VehicleStatusLabels,
  FuelTypeLabels,
  MaintenanceStatusLabels,
  VehicleDocument,
  VehicleMaintenance,
  VehicleFuelSummary,
  VehicleGps
} from '../../../core/models/vehicle.model';
import { UiDrawerComponent } from '../../../shared/components/ui/drawer/ui-drawer.component';
import { UiTabsComponent } from '../../../shared/components/ui/tabs/ui-tabs.component';
import { UiButtonComponent } from '../../../shared/components/ui/button/ui-button.component';
import { UiInputComponent } from '../../../shared/components/ui/input/ui-input.component';
import { UiSelectComponent } from '../../../shared/components/ui/select/ui-select.component';
import { UiStatusBadgeComponent } from '../../../shared/components/ui/status-badge/ui-status-badge.component';
import { UiModalComponent } from '../../../shared/components/ui/modal/ui-modal.component';
import { UiEmptyStateComponent } from '../../../shared/components/ui/empty-state/ui-empty-state.component';
import { UiTab, UiSelectOption } from '../../../shared/components/ui/types/ui.types';
import { dateInputToIso } from '../../../core/utils/date-input.util';

@Component({
  selector: 'app-vehicle-details-drawer',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    DatePipe,
    DecimalPipe,
    UiDrawerComponent,
    UiTabsComponent,
    UiButtonComponent,
    UiInputComponent,
    UiSelectComponent,
    UiStatusBadgeComponent,
    UiModalComponent,
    UiEmptyStateComponent
  ],
  templateUrl: './vehicle-details-drawer.component.html',
  styleUrls: ['./vehicle-details-drawer.component.scss']
})
export class VehicleDetailsDrawerComponent {
  private readonly vehicleService = inject(VehicleService);
  private readonly driverService = inject(DriverService);
  private readonly gpsService = inject(GpsTrackingService);
  private readonly snackBar = inject(MatSnackBar);
  readonly router = inject(Router);

  readonly vehicleId = input<number | null>(null);
  readonly open = model(false);
  readonly statusChanged = output<void>();
  readonly closed = output<void>();

  activeTab = 'general';
  readonly loading = signal(false);
  readonly vehicle = signal<Vehicle | null>(null);

  readonly documents = signal<VehicleDocument[]>([]);
  readonly maintenance = signal<VehicleMaintenance[]>([]);
  readonly nextService = signal<VehicleMaintenance | null>(null);
  readonly fuelSummary = signal<VehicleFuelSummary | null>(null);
  readonly gps = signal<VehicleGps | null>(null);

  readonly tabs: UiTab[] = [
    { id: 'general', label: 'General', icon: 'info' },
    { id: 'documents', label: 'Documents', icon: 'description' },
    { id: 'maintenance', label: 'Maintenance', icon: 'build' },
    { id: 'fuel', label: 'Fuel', icon: 'local_gas_station' },
    { id: 'gps', label: 'GPS', icon: 'gps_fixed' }
  ];

  statusModalOpen = false;
  assignDriverModalOpen = false;
  assignGpsModalOpen = false;
  docModalOpen = false;

  selectedStatus = String(VehicleStatus.Available);
  statusReason = '';
  selectedDriverId: string | null = null;
  selectedGpsDeviceId: string | null = null;
  bookingId: number | null = null;

  newDocType = '';
  newDocUrl = '';
  newDocExpiry = '';
  newDocNotes = '';

  driverOptions: UiSelectOption[] = [];
  gpsOptions: UiSelectOption[] = [];
  statusOptions: UiSelectOption[] = Object.values(VehicleStatus)
    .filter((v): v is VehicleStatus => typeof v === 'number')
    .map(s => ({ value: String(s), label: VehicleStatusLabels[s] }));

  private loadedTabs = new Set<string>();

  constructor() {
    effect(() => {
      const id = this.vehicleId();
      const isOpen = this.open();
      if (isOpen && id) {
        this.loadedTabs.clear();
        this.activeTab = 'general';
        this.loadVehicle(id);
      }
    });
  }

  onTabChange(tabId: string): void {
    this.activeTab = tabId;
    const id = this.vehicleId();
    if (id && this.open() && !this.loadedTabs.has(tabId)) {
      this.loadTab(tabId, id);
    }
  }

  onDrawerClosed(): void {
    this.closed.emit();
  }

  statusLabel(status: VehicleStatus): string {
    return VehicleStatusLabels[status] ?? 'Unknown';
  }

  fuelTypeLabel(ft: number): string {
    return FuelTypeLabels[ft as keyof typeof FuelTypeLabels] ?? '—';
  }

  maintenanceStatusLabel(status: number): string {
    return MaintenanceStatusLabels[status as keyof typeof MaintenanceStatusLabels] ?? '—';
  }

  openStatusModal(): void {
    const v = this.vehicle();
    if (v) {
      this.selectedStatus = String(v.status);
    }
    this.statusReason = '';
    this.statusModalOpen = true;
  }

  openAssignDriverModal(): void {
    this.driverService.getAll(1, 500).subscribe(result => {
      this.driverOptions = result.items.map(d => ({
        value: String(d.id),
        label: `${d.fullName} (${d.phone})`
      }));
      this.selectedDriverId = null;
      this.bookingId = null;
      this.assignDriverModalOpen = true;
    });
  }

  openAssignGpsModal(): void {
    this.gpsService.getDevices().subscribe(devices => {
      const v = this.vehicle();
      this.gpsOptions = devices.map(d => ({
        value: String(d.id),
        label: `${d.name} (${d.uniqueId})`
      }));
      this.selectedGpsDeviceId = v?.gpsDeviceId != null ? String(v.gpsDeviceId) : null;
      this.assignGpsModalOpen = true;
    });
  }

  submitStatusChange(): void {
    const id = this.vehicleId();
    if (!id) return;

    this.vehicleService.changeStatus(id, {
      status: Number(this.selectedStatus) as VehicleStatus,
      reason: this.statusReason || null
    }).subscribe({
      next: () => {
        this.snackBar.open('Status updated', 'Close', { duration: 2000 });
        this.statusModalOpen = false;
        this.loadVehicle(id);
        this.statusChanged.emit();
      },
      error: () => this.snackBar.open('Status update failed', 'Close', { duration: 3000 })
    });
  }

  submitAssignDriver(): void {
    const id = this.vehicleId();
    if (!id || !this.selectedDriverId) return;

    this.vehicleService.assignDriver(id, {
      driverId: Number(this.selectedDriverId),
      bookingId: this.bookingId || null
    }).subscribe({
      next: () => {
        this.snackBar.open('Driver assigned', 'Close', { duration: 2000 });
        this.assignDriverModalOpen = false;
        this.loadVehicle(id);
        this.statusChanged.emit();
      },
      error: () => this.snackBar.open('Assign driver failed', 'Close', { duration: 3000 })
    });
  }

  submitAssignGps(): void {
    const id = this.vehicleId();
    if (!id || !this.selectedGpsDeviceId) return;

    this.vehicleService.assignGps(id, { gpsDeviceId: Number(this.selectedGpsDeviceId) }).subscribe({
      next: () => {
        this.snackBar.open('GPS device assigned', 'Close', { duration: 2000 });
        this.assignGpsModalOpen = false;
        this.loadedTabs.delete('gps');
        this.loadTab('gps', id);
        this.loadVehicle(id);
      },
      error: () => this.snackBar.open('Assign GPS failed', 'Close', { duration: 3000 })
    });
  }

  submitDocument(): void {
    const id = this.vehicleId();
    if (!id || !this.newDocType.trim()) return;

    this.vehicleService.addDocument(id, {
      documentType: this.newDocType.trim(),
      fileUrl: this.newDocUrl || undefined,
      expiryDate: dateInputToIso(this.newDocExpiry) ?? undefined,
      notes: this.newDocNotes || undefined
    }).subscribe({
      next: () => {
        this.snackBar.open('Document added', 'Close', { duration: 2000 });
        this.docModalOpen = false;
        this.newDocType = '';
        this.newDocUrl = '';
        this.newDocExpiry = '';
        this.newDocNotes = '';
        this.loadedTabs.delete('documents');
        this.loadTab('documents', id);
      },
      error: () => this.snackBar.open('Upload failed', 'Close', { duration: 3000 })
    });
  }

  mapsUrl(gps: VehicleGps): string | null {
    if (gps.latitude == null || gps.longitude == null) return null;
    return `https://www.google.com/maps?q=${gps.latitude},${gps.longitude}`;
  }

  private prefetchGeneralTab(id: number): void {
    this.vehicleService.getMaintenance(id, 1, 1).pipe(
      catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 1 }))
    ).subscribe(r => this.nextService.set(r.items[0] ?? null));

    if (!this.loadedTabs.has('gps')) {
      this.vehicleService.getGps(id).pipe(catchError(() => of({} as VehicleGps))).subscribe(g => {
        this.gps.set(g);
      });
    }
  }

  private loadVehicle(id: number): void {
    this.loading.set(true);
    this.vehicleService.getById(id).subscribe({
      next: v => {
        this.vehicle.set(v);
        this.loading.set(false);
        this.loadedTabs.add('general');
        this.prefetchGeneralTab(id);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load vehicle', 'Close', { duration: 3000 });
      }
    });
  }

  private loadTab(tab: string, id: number): void {
    switch (tab) {
      case 'documents':
        this.vehicleService.getDocuments(id).pipe(catchError(() => of([]))).subscribe(docs => {
          this.documents.set(docs);
          this.loadedTabs.add(tab);
        });
        break;
      case 'maintenance':
        this.vehicleService.getMaintenance(id, 1, 20).pipe(
          catchError(() => of({ items: [], totalCount: 0, page: 1, pageSize: 20 }))
        ).subscribe(result => {
          this.maintenance.set(result.items);
          this.loadedTabs.add(tab);
        });
        break;
      case 'fuel':
        this.vehicleService.getFuel(id, 1, 20).pipe(
          catchError(() => of({ items: [], totalLiters: 0, totalCost: 0, totalCount: 0 }))
        ).subscribe(summary => {
          this.fuelSummary.set(summary);
          this.loadedTabs.add(tab);
        });
        break;
      case 'gps':
        this.vehicleService.getGps(id).pipe(catchError(() => of({} as VehicleGps))).subscribe(g => {
          this.gps.set(g);
          this.loadedTabs.add(tab);
        });
        break;
      default:
        this.loadedTabs.add(tab);
    }
  }
}
