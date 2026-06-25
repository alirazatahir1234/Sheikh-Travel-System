import {
  ChangeDetectionStrategy, Component, effect, inject, input, output, signal
} from '@angular/core';
import { CommonModule, CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { finalize } from 'rxjs/operators';
import { UiToastService } from '../../../../shared/components/ui/toast/ui-toast.service';
import { UiDrawerComponent } from '../../../../shared/components/ui/drawer/ui-drawer.component';
import { MaintenanceService } from '../../../../core/services/maintenance.service';
import { AuthService } from '../../../../core/services/auth.service';
import {
  WorkOrderDetail, WorkOrderStatusLabels, Workshop, CreateWorkOrderPayload,
  TechnicianListItem, WORK_ORDER_WORKFLOW_STEPS
} from '../../../../core/models/maintenance.model';
import { VehicleListItem } from '../../../../core/models/vehicle.model';
import { parseServiceItems, workflowStepIndex, woActualCost } from './utils/wo.util';
import { WoCreateFormComponent } from './components/wo-create-form.component';
import { apiErrorMessage } from '../../../../core/utils/api-error.util';

@Component({
  selector: 'work-order-detail-drawer',
  standalone: true,
  imports: [CommonModule, FormsModule, CurrencyPipe, DatePipe, MatIconModule, UiDrawerComponent, WoCreateFormComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './work-order-detail-drawer.component.html',
  styleUrls: ['./work-order-detail-drawer.component.scss']
})
export class WorkOrderDetailDrawerComponent {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(UiToastService);

  readonly workOrderId = input<number | null>(null);
  readonly createMode = input(false);
  readonly createResetKey = input(0);
  readonly vehicles = input<VehicleListItem[]>([]);
  readonly workshops = input<Workshop[]>([]);

  readonly closed = output<void>();
  readonly changed = output<void>();
  readonly created = output<void>();

  readonly wo = signal<WorkOrderDetail | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly assignOpen = signal(false);
  readonly technicians = signal<TechnicianListItem[]>([]);

  readonly workflowSteps = WORK_ORDER_WORKFLOW_STEPS;

  assignWorkshopId = 0;
  assignTechnicianId = 0;
  partId = 0;
  partQty = 1;

  constructor() {
    effect(() => {
      const id = this.workOrderId();
      if (id) this.load(id);
      else this.wo.set(null);
    }, { allowSignalWrites: true });
  }

  detailTitle(): string {
    const w = this.wo();
    return w ? `Work Order #${w.workOrderNumber}` : 'Work Order';
  }

  canManage(): boolean {
    return this.auth.hasPermission('Maintenance.WorkOrder.Manage');
  }

  serviceItems(wo: WorkOrderDetail): string[] {
    return parseServiceItems(wo.serviceTypeName);
  }

  stepIndex(status: string): number {
    return workflowStepIndex(status);
  }

  statusLabel(s: string): string {
    return (WorkOrderStatusLabels as Record<string, string>)[s] ?? s;
  }

  statusTone(s: string): string {
    if (s === 'InProgress' || s === 'WaitingParts') return 'warning';
    if (s === 'Open' || s === 'Assigned' || s === 'Draft') return 'info';
    if (s === 'Completed' || s === 'Closed') return 'done';
    if (s === 'Cancelled') return 'danger';
    return 'muted';
  }

  estimatedTotal(wo: WorkOrderDetail): number {
    return (wo.estimatedLaborCost ?? wo.laborCost) + (wo.estimatedPartsCost ?? wo.partsCost);
  }

  taxAmount(): number { return 0; }

  onClose(): void { this.closed.emit(); }

  onCreateClose(): void {
    this.closed.emit();
  }

  openAssign(): void {
    const w = this.wo();
    this.assignWorkshopId = w?.workshopId ?? 0;
    this.assignTechnicianId = w?.technicianId ?? 0;
    this.loadTechnicians(this.assignWorkshopId || undefined);
    this.assignOpen.set(true);
  }

  onAssignWorkshopChange(id: number): void {
    this.assignWorkshopId = +id;
    this.assignTechnicianId = 0;
    this.loadTechnicians(this.assignWorkshopId || undefined);
  }

  loadTechnicians(workshopId?: number): void {
    this.maintenanceService.getTechnicians(workshopId).subscribe({
      next: t => this.technicians.set(t),
      error: () => this.technicians.set([])
    });
  }

  submitAssign(): void {
    const id = this.wo()?.id;
    if (!id || !this.assignWorkshopId) return;
    this.saving.set(true);
    this.maintenanceService.updateWorkOrder(id, {
      workshopId: this.assignWorkshopId,
      technicianId: this.assignTechnicianId || null
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.assignOpen.set(false);
        this.load(id);
        this.changed.emit();
        this.toast.success('Workshop assigned');
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Assignment failed'));
      }
    });
  }

  uploadInvoice(): void {
    this.toast.warning('Invoice upload will be available when document storage is wired.');
  }

  load(id: number): void {
    this.loading.set(true);
    this.maintenanceService.getWorkOrderById(id).subscribe({
      next: w => { this.wo.set(w); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to load work order'));
      }
    });
  }

  setStatus(id: number, status: string): void {
    if (this.saving()) return;
    this.saving.set(true);
    this.maintenanceService.updateWorkOrderStatus(id, status).pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: () => {
        this.load(id);
        this.changed.emit();
        this.toast.success(`Work order marked as ${this.statusLabel(status)}`);
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Status update failed'))
    });
  }

  recordPart(workOrderId: number): void {
    if (!this.partId || this.partQty < 1) return;
    this.saving.set(true);
    this.maintenanceService.recordPartUsage(workOrderId, this.partId, this.partQty).subscribe({
      next: () => {
        this.saving.set(false);
        this.partId = 0;
        this.partQty = 1;
        this.load(workOrderId);
        this.changed.emit();
        this.toast.success('Part recorded');
      },
      error: err => {
        this.saving.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to record part'));
      }
    });
  }

  submitCreate(payload: CreateWorkOrderPayload): void {
    if (this.saving()) return;
    this.saving.set(true);
    this.maintenanceService.createWorkOrder(payload).pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: () => this.created.emit(),
      error: err => {
        const message = apiErrorMessage(err, 'Failed to create work order');
        if (message !== 'An unexpected error occurred.') {
          this.toast.error(message);
          return;
        }
        this.toast.error('Could not create the work order. Please select a vehicle and at least one service item, then try again.');
      }
    });
  }

  priorityClass(p: string | null | undefined): string {
    if (p === 'Critical') return 'danger';
    if (p === 'High') return 'warning';
    if (p === 'Medium') return 'info';
    return 'muted';
  }

  maintTypeClass(t: string | null | undefined): string {
    if (t === 'Emergency') return 'danger';
    if (t === 'Corrective') return 'warning';
    return 'done';
  }

  actualCost = woActualCost;
}
