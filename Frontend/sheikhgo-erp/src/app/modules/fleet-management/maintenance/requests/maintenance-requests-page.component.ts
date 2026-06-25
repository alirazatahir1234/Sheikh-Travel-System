import { ChangeDetectionStrategy, Component, effect, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { finalize } from 'rxjs/operators';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { MaintenanceContextService } from '../maintenance-context.service';
import {
  MaintenanceRequest,
  MaintenanceRequestStats,
  CreateMaintenanceRequestPayload
} from '../../../../core/models/maintenance.model';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { RequestKpiChipsComponent } from './components/request-kpi-chips.component';
import { RequestTableComponent } from './components/request-table.component';
import { RequestMobileCardsComponent } from './components/request-mobile-cards.component';
import { RequestDetailDrawerComponent } from './components/request-detail-drawer.component';
import { RequestCreateFormComponent } from './components/request-create-form.component';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

@Component({
  selector: 'app-maintenance-requests-page',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule,
    RequestKpiChipsComponent,
    RequestTableComponent,
    RequestMobileCardsComponent,
    RequestDetailDrawerComponent,
    RequestCreateFormComponent
  ],
  templateUrl: './maintenance-requests-page.component.html',
  styleUrls: ['./maintenance-requests-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MaintenanceRequestsPageComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly vehicleService = inject(VehicleService);
  private readonly toast = inject(UiToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly ctx = inject(MaintenanceContextService);

  readonly requests = signal<MaintenanceRequest[]>([]);
  readonly stats = signal<MaintenanceRequestStats | null>(null);
  readonly vehicles = signal<VehicleListItem[]>([]);
  readonly statusFilter = signal('');
  readonly showForm = signal(false);
  readonly formResetKey = signal(0);
  readonly saving = signal(false);
  readonly selectedId = signal<number | null>(null);

  constructor() {
    effect(() => {
      this.ctx.searchTerm();
      this.load();
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    const status = this.route.snapshot.queryParamMap.get('status');
    if (status) this.statusFilter.set(status);
    if (this.route.snapshot.queryParamMap.get('create') === 'true') this.showForm.set(true);

    this.loadStats();
    this.vehicleService.getAll(1, 500).subscribe({
      next: r => this.vehicles.set(r.items),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load vehicles'))
    });
  }

  load(): void {
    const search = this.ctx.searchTerm() || undefined;
    const status = this.statusFilter() || undefined;
    this.maintenanceService.getRequests(1, 100, status, search).subscribe({
      next: r => this.requests.set(r.items),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load requests'))
    });
  }

  loadStats(): void {
    this.maintenanceService.getRequestStats().subscribe({
      next: s => this.stats.set(s),
      error: () => this.stats.set({ open: 0, approved: 0, inProgress: 0, pendingApproval: 0 })
    });
  }

  onFilterChange(status: string): void {
    this.statusFilter.set(status);
    this.load();
  }

  onSelect(id: number): void {
    this.selectedId.set(id);
  }

  onChanged(): void {
    this.load();
    this.loadStats();
  }

  submit(form: CreateMaintenanceRequestPayload): void {
    this.saving.set(true);
    this.maintenanceService.createRequest({
      ...form,
      description: form.description.trim()
    }).pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: () => {
        this.showForm.set(false);
        this.formResetKey.update(k => k + 1);
        this.load();
        this.loadStats();
        this.toast.success('Request created');
      },
      error: err => {
        this.toast.error(apiErrorMessage(err, 'Failed to create request'));
      }
    });
  }

  closeForm(): void {
    this.showForm.set(false);
    this.formResetKey.update(k => k + 1);
  }
}
