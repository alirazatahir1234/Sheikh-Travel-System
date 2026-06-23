import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { VehicleService } from '../../../../core/services/vehicle.service';
import { MaintenanceContextService } from '../maintenance-context.service';
import {
  MaintenanceScheduleCalendarItem,
  MaintenanceScheduleListItem,
  ScheduleStatus
} from '../../../../core/models/maintenance.model';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { ScheduleViewToggleComponent, ScheduleView } from './components/schedule-view-toggle.component';
import { ScheduleListViewComponent } from './components/schedule-list-view.component';
import { ScheduleCalendarViewComponent } from './components/schedule-calendar-view.component';
import { ScheduleTimelineViewComponent } from './components/schedule-timeline-view.component';
import { ScheduleFormDrawerComponent, ScheduleDrawerMode } from './components/schedule-form-drawer.component';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

@Component({
  selector: 'app-service-scheduler-page',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ScheduleViewToggleComponent,
    ScheduleListViewComponent,
    ScheduleCalendarViewComponent,
    ScheduleTimelineViewComponent,
    ScheduleFormDrawerComponent
  ],
  templateUrl: './service-scheduler-page.component.html',
  styleUrls: ['./service-scheduler-page.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ServiceSchedulerPageComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly vehicleService = inject(VehicleService);
  private readonly toast = inject(UiToastService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  readonly ctx = inject(MaintenanceContextService);

  readonly schedules = signal<MaintenanceScheduleListItem[]>([]);
  readonly calendarItems = signal<MaintenanceScheduleCalendarItem[]>([]);
  readonly vehicles = signal<VehicleListItem[]>([]);
  readonly view = signal<ScheduleView>('list');
  readonly statusFilter = signal<ScheduleStatus | ''>('');
  readonly drawerOpen = signal(false);
  readonly drawerMode = signal<ScheduleDrawerMode>('create');
  readonly selectedSchedule = signal<MaintenanceScheduleListItem | null>(null);
  readonly loading = signal(true);

  readonly statusOptions: { value: ScheduleStatus | ''; label: string }[] = [
    { value: '', label: 'All statuses' },
    { value: 'Upcoming', label: 'Upcoming' },
    { value: 'DueSoon', label: 'Due Soon' },
    { value: 'Overdue', label: 'Overdue' }
  ];

  constructor() {
    if (typeof window !== 'undefined' && window.matchMedia('(max-width: 767px)').matches) {
      this.view.set('list');
    }

    effect(() => {
      this.ctx.searchTerm();
      this.statusFilter();
      this.loadList();
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    this.vehicleService.getAll(1, 500).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: r => this.vehicles.set(r.items),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load vehicles'))
    });
    this.loadCalendarRange(this.monthRange());
  }

  onViewChange(v: ScheduleView): void {
    this.view.set(v);
    if (v === 'calendar') this.loadCalendarRange(this.monthRange());
    if (v === 'timeline') this.loadCalendarRange(this.weekRange());
  }

  openCreate(): void {
    this.drawerMode.set('create');
    this.selectedSchedule.set(null);
    this.drawerOpen.set(true);
  }

  openReschedule(s: MaintenanceScheduleListItem): void {
    this.drawerMode.set('reschedule');
    this.selectedSchedule.set(s);
    this.drawerOpen.set(true);
  }

  createWorkOrder(s: MaintenanceScheduleListItem): void {
    this.maintenanceService.createWorkOrderFromSchedule(s.id).subscribe({
      next: id => this.router.navigate(['/fleet/maintenance/work-orders'], { queryParams: { wo: id } }),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to create work order'))
    });
  }

  onCalendarRange(range: { from: string; to: string }): void {
    this.loadCalendarRange(range);
  }

  onSaved(): void {
    this.loadList();
    const v = this.view();
    if (v === 'calendar') this.loadCalendarRange(this.monthRange());
    if (v === 'timeline') this.loadCalendarRange(this.weekRange());
    this.toast.success('Schedule saved');
  }

  private loadList(): void {
    const search = this.ctx.searchTerm() || undefined;
    const status = this.statusFilter() || undefined;
    this.loading.set(true);
    this.maintenanceService.getSchedulesEnriched({ search, status }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: rows => { this.schedules.set(rows); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to load schedules'));
      }
    });
  }

  private loadCalendarRange(range: { from: string; to: string }): void {
    this.maintenanceService.getScheduleCalendar(range.from, range.to).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: items => this.calendarItems.set(items),
      error: err => this.toast.error(apiErrorMessage(err, 'Failed to load calendar'))
    });
  }

  private monthRange(): { from: string; to: string } {
    const now = new Date();
    const start = new Date(now.getFullYear(), now.getMonth(), 1);
    const end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
    return { from: start.toISOString(), to: end.toISOString() };
  }

  private weekRange(): { from: string; to: string } {
    const now = new Date();
    const start = new Date(now);
    start.setDate(start.getDate() - start.getDay());
    const end = new Date(start);
    end.setDate(end.getDate() + 6);
    return { from: start.toISOString(), to: end.toISOString() };
  }
}
