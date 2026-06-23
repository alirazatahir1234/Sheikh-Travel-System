import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatIconModule } from '@angular/material/icon';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { ExportService, ExportColumn } from '../../../../core/services/export.service';
import { VehicleServiceHistoryItem, ServiceType } from '../../../../core/models/maintenance.model';
import { Vehicle, VehicleListItem } from '../../../../core/models/vehicle.model';
import { VehicleHistoryProfileCardComponent } from './components/vehicle-history-profile-card.component';
import {
  ServiceHistoryFilterState,
  ServiceHistoryFiltersComponent
} from './components/service-history-filters.component';
import { ServiceHistoryTimelineComponent } from './components/service-history-timeline.component';
import { ServiceHistoryStatsComponent } from './components/service-history-stats.component';
import {
  ServiceHistoryExportDialogComponent,
  ServiceHistoryExportFormat
} from './components/service-history-export-dialog.component';
import { computeAvgIntervalDays, isAccidentService } from './utils/service-type.util';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

function defaultDateRange(): { from: string; to: string } {
  const to = new Date();
  const from = new Date();
  from.setFullYear(from.getFullYear() - 1);
  return { from: from.toISOString().slice(0, 10), to: to.toISOString().slice(0, 10) };
}

function defaultFilters(): ServiceHistoryFilterState {
  return { vehicleId: null, ...defaultDateRange(), serviceType: '' };
}

@Component({
  selector: 'app-service-history-page',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    VehicleHistoryProfileCardComponent,
    ServiceHistoryFiltersComponent,
    ServiceHistoryStatsComponent,
    ServiceHistoryTimelineComponent,
    ServiceHistoryExportDialogComponent
  ],
  templateUrl: './service-history-page.component.html',
  styleUrls: ['./service-history-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ServiceHistoryPageComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly vehicleService = inject(VehicleService);
  private readonly exportService = inject(ExportService);
  private readonly toast = inject(UiToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  readonly vehicles = signal<VehicleListItem[]>([]);
  readonly serviceTypes = signal<ServiceType[]>([]);
  readonly selectedVehicle = signal<Vehicle | null>(null);
  readonly items = signal<VehicleServiceHistoryItem[]>([]);
  readonly loading = signal(false);
  readonly exportOpen = signal(false);

  readonly draftFilters = signal<ServiceHistoryFilterState>(defaultFilters());
  readonly filters = signal<ServiceHistoryFilterState>(defaultFilters());

  readonly vehicleImageUrl = computed(() => {
    const id = this.filters().vehicleId;
    if (!id) return null;
    return this.vehicles().find(v => v.id === id)?.imageUrl ?? null;
  });

  readonly selectedListItem = computed(() => {
    const id = this.filters().vehicleId;
    if (!id) return null;
    return this.vehicles().find(v => v.id === id) ?? null;
  });

  readonly stats = computed(() => {
    const rows = this.items();
    return {
      totalServices: rows.length,
      totalCost: rows.reduce((s, r) => s + (r.totalCost ?? 0), 0),
      avgIntervalDays: computeAvgIntervalDays(rows),
      accidentRecords: rows.filter(r => isAccidentService(r.serviceType)).length
    };
  });

  readonly lastServiceDate = computed(() => {
    const rows = this.items();
    if (!rows.length) return null;
    const dates = rows
      .map(r => r.serviceDate)
      .filter(Boolean)
      .sort((a, b) => new Date(b).getTime() - new Date(a).getTime());
    return dates[0] ?? null;
  });

  ngOnInit(): void {
    const vehicleId = this.route.snapshot.queryParamMap.get('vehicleId');
    if (vehicleId) {
      const id = +vehicleId;
      const withVehicle = { ...defaultFilters(), vehicleId: id };
      this.draftFilters.set(withVehicle);
      this.filters.set(withVehicle);
      this.loadVehicle(id);
    }

    this.vehicleService.getAll(1, 500).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => this.vehicles.set(r.items),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load vehicles'))
    });

    this.maintenanceService.getServiceTypes().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: types => this.serviceTypes.set(types)
    });

    this.loadHistory();
  }

  onDraftChange(state: ServiceHistoryFilterState): void {
    this.draftFilters.set(state);
  }

  onApply(): void {
    const next = this.draftFilters();
    this.filters.set(next);
    if (next.vehicleId) this.loadVehicle(next.vehicleId);
    else this.selectedVehicle.set(null);
    this.loadHistory();
  }

  onReset(): void {
    const reset = defaultFilters();
    this.draftFilters.set(reset);
    this.filters.set(reset);
    this.selectedVehicle.set(null);
    this.loadHistory();
  }

  openExport(): void {
    this.exportOpen.set(true);
  }

  closeExport(): void {
    this.exportOpen.set(false);
  }

  onExport(format: ServiceHistoryExportFormat): void {
    this.exportRows(format);
  }

  private loadVehicle(id: number): void {
    this.vehicleService.getById(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: v => this.selectedVehicle.set(v),
      error: () => this.selectedVehicle.set(null)
    });
  }

  private loadHistory(): void {
    const f = this.filters();
    this.loading.set(true);
    const from = f.from ? new Date(f.from).toISOString() : undefined;
    const to = f.to ? new Date(new Date(f.to).getTime() + 86_400_000).toISOString() : undefined;

    this.maintenanceService.getServiceHistory({
      vehicleId: f.vehicleId ?? undefined,
      from,
      to,
      serviceType: f.serviceType || undefined
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rows => { this.items.set(rows); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to load history'));
      }
    });
  }

  private exportRows(format: ServiceHistoryExportFormat): void {
    const rows = this.items();
    if (!rows.length) {
      this.toast.warning('No records to export');
      return;
    }

    const cols: ExportColumn<VehicleServiceHistoryItem>[] = [
      { header: 'Date', accessor: r => r.serviceDate },
      { header: 'Vehicle', accessor: r => r.vehicleName ?? '' },
      { header: 'Service Type', accessor: r => r.serviceType },
      { header: 'Workshop', accessor: r => r.workshopName ?? '' },
      { header: 'Technician', accessor: r => r.technicianName ?? '' },
      { header: 'Labor', accessor: r => r.laborCost },
      { header: 'Parts', accessor: r => r.partsCost },
      { header: 'Total', accessor: r => r.totalCost },
      { header: 'Notes', accessor: r => r.notes ?? '' }
    ];

    const plate = this.selectedVehicle()?.registrationNumber?.replace(/\W+/g, '-') ?? 'all';
    const filename = `service-history-${plate}`;
    const title = 'Vehicle Service History';

    if (format === 'excel') {
      this.exportService.exportExcel(rows, cols, { title, filename });
    } else {
      this.exportService.exportPdf(rows, cols, { title, filename });
    }
    this.toast.success(`${format === 'excel' ? 'Excel' : 'PDF'} exported`);
  }
}
