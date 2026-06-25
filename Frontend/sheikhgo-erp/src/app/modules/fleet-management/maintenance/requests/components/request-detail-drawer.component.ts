import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { UiToastService } from '../../../../../shared/components/ui/toast/ui-toast.service';
import { Router } from '@angular/router';
import { UiDrawerComponent } from '../../../../../shared/components/ui/drawer/ui-drawer.component';
import { MaintenanceService } from '../../../../../core/services/maintenance.service';
import { AuthService } from '../../../../../core/services/auth.service';
import { MaintenanceRequest } from '../../../../../core/models/maintenance.model';
import { finalize } from 'rxjs/operators';
import { apiErrorMessage } from '../../../../../core/utils/api-error.util';
import { resolveUploadUrl } from '../../../../../core/utils/upload-url.util';

@Component({
  selector: 'request-detail-drawer',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePipe, MatIconModule, UiDrawerComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ui-drawer [open]="!!requestId()" [title]="title()" (closed)="onClose()" width="460px" [padded]="false">
      @if (loading()) {
        <div class="pad"><p class="muted">Loading…</p></div>
      } @else if (req()) {
        <div class="drawer-body">
          <div class="pad header-meta">
            <p class="created">Created {{ req()!.createdAt | date:'medium' }}</p>
          </div>

          <div class="pad section">
            <h4>Vehicle & Driver</h4>
            <dl>
              <div><dt>Vehicle</dt><dd>{{ req()!.vehicleName }} @if (req()!.vehicleRegistration) { ({{ req()!.vehicleRegistration }}) }</dd></div>
              <div><dt>Driver</dt><dd>{{ req()!.driverName || '—' }}</dd></div>
              <div><dt>Branch</dt><dd>{{ req()!.branchName || '—' }}</dd></div>
              <div><dt>Department</dt><dd>{{ req()!.departmentName || '—' }}</dd></div>
            </dl>
          </div>

          <div class="pad section issue">
            <div class="badges">
              <span class="cat">{{ req()!.issueCategory }}</span>
              <span class="prio prio--{{ (req()!.priority || 'medium').toLowerCase() }}">{{ req()!.priority }}</span>
              <span class="status">{{ req()!.status }}</span>
            </div>
            <p class="desc">{{ req()!.description }}</p>
            @if (req()!.driverRemarks) { <p class="remarks">{{ req()!.driverRemarks }}</p> }
            @if (req()!.rejectionReason) { <p class="reject-reason">Rejection: {{ req()!.rejectionReason }}</p> }
          </div>

          <div class="pad section">
            <h4>Attachments</h4>
            <div class="attachments">
              @for (url of photoUrls(); track url) {
                <a [href]="url" target="_blank" rel="noopener"><img [src]="url" alt="Attachment" /></a>
              }
              @if (canApprove()) {
                <label class="add-btn">
                  <mat-icon>add</mat-icon>
                  <input type="file" accept="image/*,.pdf" (change)="onFile($event, req()!.id)" hidden />
                </label>
              }
            </div>
          </div>

          <footer class="footer">
            @if (req()!.status === 'Approved' && canManageWo() && !req()!.workOrderId) {
              <p class="next-step">Request approved — create a work order to begin maintenance.</p>
            }
            @if (canManageWo()) {
              <button type="button" class="btn-primary" (click)="createWorkOrder(req()!)" [disabled]="saving() || !!req()!.workOrderId">
                {{ req()!.workOrderId ? 'Work Order Created' : 'Create Work Order' }}
              </button>
            }
            @if (canApprove() && (req()!.status === 'Open' || req()!.status === 'PendingApproval')) {
              <button type="button" class="btn-approve" (click)="approve(req()!.id)" [disabled]="saving()">
                {{ saving() ? 'Approving…' : 'Approve' }}
              </button>
              <button type="button" class="btn-reject" (click)="showReject.set(true)" [disabled]="saving()">Reject</button>
            }
          </footer>

          @if (showReject()) {
            <div class="reject-panel pad">
              <textarea [(ngModel)]="rejectReason" rows="3" placeholder="Rejection reason…"></textarea>
              <div class="reject-actions">
                <button type="button" (click)="showReject.set(false)">Cancel</button>
                <button type="button" class="btn-reject" (click)="reject(req()!.id)">Confirm Reject</button>
              </div>
            </div>
          }
        </div>
      }
    </ui-drawer>
  `,
  styles: [`
    .pad { padding: 1rem 1.25rem; }
    .drawer-body { display: flex; flex-direction: column; min-height: 100%; }
    .header-meta { border-bottom: 1px solid #f1f5f9; }
    .created { margin: 0; font-size: 0.75rem; color: #94a3b8; }
    h4 { margin: 0 0 0.625rem; font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; color: #64748b; }
    dl { margin: 0; display: grid; gap: 0.5rem; }
    dl div { display: grid; grid-template-columns: 100px 1fr; gap: 0.5rem; font-size: 0.8125rem; }
    dt { color: #94a3b8; }
    dd { margin: 0; font-weight: 600; }
    .section { border-bottom: 1px solid #f1f5f9; }
    .badges { display: flex; gap: 0.5rem; flex-wrap: wrap; margin-bottom: 0.75rem; }
    .cat { background: #0b6b50; color: #fff; padding: 0.25rem 0.625rem; border-radius: 6px; font-size: 0.75rem; font-weight: 700; }
    .prio { font-size: 0.6875rem; font-weight: 700; text-transform: uppercase; padding: 0.25rem 0.5rem; border-radius: 6px; }
    .prio--critical { background: #fee2e2; color: #dc2626; }
    .prio--high { background: #fef3c7; color: #f59e0b; }
    .prio--medium { background: #e8f5f0; color: #0b6b50; }
    .status { background: #f1f5f9; color: #475569; padding: 0.25rem 0.5rem; border-radius: 6px; font-size: 0.6875rem; font-weight: 700; }
    .desc { margin: 0; font-size: 0.875rem; line-height: 1.5; }
    .remarks, .reject-reason { margin: 0.5rem 0 0; font-size: 0.8125rem; color: #64748b; }
    .reject-reason { color: #dc2626; }
    .attachments { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .attachments img { width: 72px; height: 72px; object-fit: cover; border-radius: 8px; border: 1px solid #e2e8f0; }
    .add-btn {
      width: 72px; height: 72px; border: 2px dashed #cbd5e1; border-radius: 8px;
      display: grid; place-items: center; cursor: pointer; color: #94a3b8;
    }
    .footer {
      margin-top: auto; display: flex; gap: 0.5rem; flex-wrap: wrap; padding: 1rem 1.25rem;
      border-top: 1px solid #e2e8f0; background: #f8faf9; position: sticky; bottom: 0;
    }
    button { border: 1px solid #e2e8f0; background: #fff; border-radius: 8px; padding: 0.625rem 1rem; font-weight: 700; font-size: 0.8125rem; cursor: pointer; }
    .btn-primary { background: #0b6b50; color: #fff; border-color: #0b6b50; flex: 1; }
    .btn-approve { color: #0b6b50; border-color: #0b6b50; }
    .btn-reject { color: #dc2626; border-color: #fecaca; background: #fef2f2; }
    .next-step {
      flex: 1 1 100%;
      margin: 0 0 0.25rem;
      font-size: 0.8125rem;
      color: #0b6b50;
      font-weight: 600;
    }
    button:disabled { opacity: 0.6; cursor: not-allowed; }
    .reject-panel textarea { width: 100%; border: 1px solid #e2e8f0; border-radius: 8px; padding: 0.5rem; font-size: 0.8125rem; }
    .reject-actions { display: flex; gap: 0.5rem; margin-top: 0.5rem; justify-content: flex-end; }
    .muted { color: #94a3b8; }
    @media (max-width: 768px) {
      :host ::ng-deep ui-drawer aside { width: 100% !important; max-height: 90vh; top: auto; bottom: 0; border-radius: 16px 16px 0 0; }
    }
  `]
})
export class RequestDetailDrawerComponent {
  private readonly maintenanceService = inject(MaintenanceService);
  private readonly auth = inject(AuthService);
  private readonly toast = inject(UiToastService);
  private readonly router = inject(Router);

  readonly requestId = input<number | null>(null);
  readonly closed = output<void>();
  readonly changed = output<void>();

  readonly req = signal<MaintenanceRequest | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly showReject = signal(false);
  rejectReason = '';

  constructor() {
    effect(() => {
      const id = this.requestId();
      if (id) this.load(id);
      else { this.req.set(null); this.showReject.set(false); }
    }, { allowSignalWrites: true });
  }

  title(): string {
    const r = this.req();
    return r ? `#${r.requestNumber}` : 'Request';
  }

  photoUrls(): string[] {
    const r = this.req();
    if (!r?.photosJson) return [];
    try {
      const urls = JSON.parse(r.photosJson) as string[];
      return urls.map(u => resolveUploadUrl(u)).filter((u): u is string => !!u);
    } catch { return []; }
  }

  canApprove(): boolean {
    return this.auth.hasPermission('Maintenance.Request.Approve');
  }

  canManageWo(): boolean {
    return this.auth.hasPermission('Maintenance.WorkOrder.Manage');
  }

  onClose(): void {
    this.closed.emit();
  }

  load(id: number): void {
    this.loading.set(true);
    this.maintenanceService.getRequestById(id).subscribe({
      next: r => { this.req.set(r); this.loading.set(false); },
      error: err => {
        this.loading.set(false);
        this.toast.error(apiErrorMessage(err, 'Failed to load request'));
      }
    });
  }

  approve(id: number): void {
    if (this.saving()) return;
    this.saving.set(true);
    this.maintenanceService.approveRequest(id).pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: () => {
        this.load(id);
        this.changed.emit();
        this.toast.success('Request approved. You can now create a work order.');
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not approve this request'))
    });
  }

  reject(id: number): void {
    if (this.saving()) return;
    if (!this.rejectReason.trim()) {
      this.toast.warning('Rejection reason is required');
      return;
    }
    this.saving.set(true);
    this.maintenanceService.rejectRequest(id, this.rejectReason).pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: () => {
        this.showReject.set(false);
        this.rejectReason = '';
        this.load(id);
        this.changed.emit();
        this.toast.success('Request rejected');
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not reject this request'))
    });
  }

  createWorkOrder(r: MaintenanceRequest): void {
    if (this.saving()) return;
    if (r.workOrderId) {
      this.toast.info('A work order already exists for this request.');
      return;
    }
    if (!['Open', 'Approved', 'InProgress'].includes(r.status)) {
      this.toast.warning('This request must be approved before creating a work order.');
      return;
    }
    this.saving.set(true);
    this.maintenanceService.convertRequest(r.id).pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: woId => {
        this.changed.emit();
        this.toast.success('Work order created');
        this.router.navigate(['/fleet/maintenance/work-orders'], { queryParams: { wo: woId } });
      },
      error: err => this.toast.error(apiErrorMessage(err, 'Could not create work order'))
    });
  }

  onFile(event: Event, id: number): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.maintenanceService.uploadRequestAttachment(id, file).subscribe({
      next: () => { this.load(id); this.toast.success('Attachment uploaded'); },
      error: err => this.toast.error(apiErrorMessage(err, 'Upload failed'))
    });
    input.value = '';
  }
}
