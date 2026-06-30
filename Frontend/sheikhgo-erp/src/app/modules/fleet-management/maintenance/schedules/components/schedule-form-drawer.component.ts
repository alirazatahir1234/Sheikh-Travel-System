import { ChangeDetectionStrategy, Component, effect, inject, input, OnInit, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { UiDrawerComponent } from '../../../../../shared/components/ui/drawer/ui-drawer.component';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { MaintenanceService } from '../../../../../core/services/maintenance.service';
import {
  CreateMaintenanceSchedulePayload,
  MaintenanceScheduleListItem,
  MaintenanceScheduleTemplate,
  RescheduleMaintenanceSchedulePayload,
  ServiceType
} from '../../../../../core/models/maintenance.model';
import { VehicleListItem } from '../../../../../core/models/vehicle.model';
import { apiErrorMessage } from '../../../../../core/utils/api-error.util';
import { buildCreateMaintenanceSchedulePayload } from '../utils/schedule-form.util';

export type ScheduleDrawerMode = 'create' | 'reschedule';

@Component({
  selector: 'schedule-form-drawer',
  standalone: true,
  imports: [CommonModule, FormsModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer
      [open]="open()"
      [title]="mode() === 'create' ? 'Schedule Service' : 'Reschedule Service'"
      (closed)="onClose()">
      <form class="form" (ngSubmit)="submit()">
        @if (mode() === 'create') {
          <label>Vehicle
            <select [(ngModel)]="form.vehicleId" name="vehicleId" required>
              <option [ngValue]="0">Select vehicle</option>
              @for (v of vehicles(); track v.id) {
                <option [ngValue]="v.id">{{ v.name }}</option>
              }
            </select>
          </label>
        }

        <label>Service Type
          <select [(ngModel)]="form.serviceTypeName" name="serviceType" required (ngModelChange)="onServiceTypeChange($event)">
            <option value="">Select or type below</option>
            @for (st of serviceTypes(); track st.id) {
              <option [value]="st.name">{{ st.name }}</option>
            }
          </select>
          <input [(ngModel)]="form.serviceTypeName" name="serviceTypeCustom" placeholder="Custom service name" />
        </label>

        <label>Interval Type
          <select [(ngModel)]="form.intervalType" name="intervalType">
            <option value="Mileage">Mileage (km)</option>
            <option value="Months">Months</option>
            <option value="Days">Days</option>
            <option value="EngineHours">Engine Hours</option>
          </select>
        </label>

        <label>Interval Value
          <input type="number" [(ngModel)]="form.intervalValue" name="intervalValue" required min="1" />
        </label>

        @if (form.intervalType === 'Mileage') {
          <label>Last Service Mileage (km)
            <input type="number" [(ngModel)]="form.lastServiceMileage" name="lastMileage" />
          </label>
        }
        @if (form.intervalType === 'EngineHours') {
          <label>Last Service Engine Hours
            <input type="number" [(ngModel)]="form.lastServiceEngineHours" name="lastHours" />
          </label>
        }
        @if (form.intervalType === 'Months' || form.intervalType === 'Days') {
          <label>Last Service Date
            <input type="date" [(ngModel)]="form.lastServiceDate" name="lastDate" />
          </label>
        }

        <label>Priority
          <select [(ngModel)]="form.priority" name="priority">
            <option>Low</option><option>Medium</option><option>High</option><option>Critical</option>
          </select>
        </label>

        @if (mode() === 'create' && templates().length) {
          <div class="templates">
            <span class="templates__label">Quick templates</span>
            <div class="templates__chips">
              @for (t of templates(); track t.serviceTypeName) {
                <button type="button" class="chip" (click)="applyTemplate(t)">{{ t.serviceTypeName }} ({{ t.intervalValue | number }} km)</button>
              }
            </div>
          </div>
        }

        <footer class="form__footer">
          <button type="button" class="btn-muted" (click)="onClose()">Cancel</button>
          <button type="submit" class="btn-primary" [disabled]="saving()">
            {{ saving() ? 'Saving…' : (mode() === 'create' ? 'Schedule' : 'Reschedule') }}
          </button>
        </footer>
      </form>
    </ui-drawer>
  `,
  styles: [`
    .form { display: grid; gap: 0.875rem; }
    label { display: grid; gap: 0.35rem; font-size: 0.8125rem; font-weight: 600; color: #334155; }
    input, select {
      padding: 0.5rem 0.625rem;
      border: 1px solid #e2e8f0;
      border-radius: 8px;
      font-size: 0.875rem;
    }
    .templates__label { font-size: 0.75rem; font-weight: 700; color: #64748b; }
    .templates__chips { display: flex; flex-wrap: wrap; gap: 0.35rem; margin-top: 0.35rem; }
    .chip {
      border: 1px solid #cbd5e1;
      background: #f8fafc;
      border-radius: 999px;
      padding: 0.25rem 0.625rem;
      font-size: 0.75rem;
      cursor: pointer;
    }
    .chip:hover { border-color: #0B6B50; color: #0B6B50; }
    .form__footer { display: flex; justify-content: flex-end; gap: 0.5rem; margin-top: 0.5rem; }
    .btn-primary { background: #0B6B50; color: #fff; border: none; border-radius: 8px; padding: 0.5rem 1rem; font-weight: 600; cursor: pointer; }
    .btn-muted { background: #f1f5f9; color: #475569; border: none; border-radius: 8px; padding: 0.5rem 1rem; cursor: pointer; }
    .btn-primary:disabled { opacity: 0.6; cursor: not-allowed; }
  `]
})
export class ScheduleFormDrawerComponent implements OnInit {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly toast = inject(UiToastService);

  readonly open = input(false);
  readonly mode = input<ScheduleDrawerMode>('create');
  readonly schedule = input<MaintenanceScheduleListItem | null>(null);
  readonly vehicles = input<VehicleListItem[]>([]);
  readonly closed = output<void>();
  readonly saved = output<void>();

  readonly serviceTypes = signal<ServiceType[]>([]);
  readonly templates = signal<MaintenanceScheduleTemplate[]>([]);
  readonly saving = signal(false);

  form: CreateMaintenanceSchedulePayload & { serviceTypeId?: number | null } = {
    vehicleId: 0,
    serviceTypeName: '',
    intervalType: 'Mileage',
    intervalValue: 5000,
    priority: 'Medium',
    lastServiceMileage: undefined,
    lastServiceEngineHours: undefined,
    lastServiceDate: undefined
  };

  constructor() {
    effect(() => {
      if (!this.open()) return;
      const s = this.schedule();
      if (this.mode() === 'reschedule' && s) {
        this.form = {
          vehicleId: s.vehicleId,
          serviceTypeName: s.serviceTypeName,
          intervalType: s.intervalType,
          intervalValue: s.intervalValue,
          priority: s.priority,
          lastServiceMileage: s.lastServiceMileage ?? s.currentMileage,
          lastServiceEngineHours: s.lastServiceEngineHours ?? s.currentEngineHours ?? undefined,
          lastServiceDate: s.lastServiceDate?.slice(0, 10) ?? undefined
        };
      } else if (this.mode() === 'create') {
        this.form = {
          vehicleId: 0,
          serviceTypeName: '',
          intervalType: 'Mileage',
          intervalValue: 5000,
          priority: 'Medium'
        };
      }
    }, { allowSignalWrites: true });
  }

  ngOnInit(): void {
    this.maintenanceService.getServiceTypes().subscribe(st => this.serviceTypes.set(st));
    this.maintenanceService.getScheduleTemplates().subscribe(t => this.templates.set(t));
  }

  onServiceTypeChange(name: string): void {
    const match = this.serviceTypes().find(s => s.name === name);
    this.form.serviceTypeId = match?.id ?? null;
  }

  applyTemplate(t: MaintenanceScheduleTemplate): void {
    this.form.serviceTypeName = t.serviceTypeName;
    this.form.intervalType = t.intervalType;
    this.form.intervalValue = t.intervalValue;
  }

  submit(): void {
    if (this.mode() === 'reschedule' && this.schedule()) {
      this.saving.set(true);
      const body: RescheduleMaintenanceSchedulePayload = {
        lastServiceDate: this.form.lastServiceDate || null,
        lastServiceMileage: this.form.lastServiceMileage ?? null,
        lastServiceEngineHours: this.form.lastServiceEngineHours ?? null,
        intervalType: this.form.intervalType,
        intervalValue: this.form.intervalValue
      };
      this.maintenanceService.rescheduleSchedule(this.schedule()!.id, body).subscribe({
        next: () => { this.saving.set(false); this.saved.emit(); this.onClose(); },
        error: err => {
          this.saving.set(false);
          this.toast.error(apiErrorMessage(err, 'Reschedule failed'));
        }
      });
      return;
    }

    const payload = buildCreateMaintenanceSchedulePayload(this.form);
    if (!payload) {
      this.toast.error('Select a vehicle, service type, and interval value of at least 1.');
      return;
    }

    this.saving.set(true);
    this.maintenanceService.createSchedule(payload).subscribe({
      next: () => { this.saving.set(false); this.saved.emit(); this.onClose(); },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to schedule'));
      }
    });
  }

  onClose(): void {
    this.closed.emit();
  }
}
