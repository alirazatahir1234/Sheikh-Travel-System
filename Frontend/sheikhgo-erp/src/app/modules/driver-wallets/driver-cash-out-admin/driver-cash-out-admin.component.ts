import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup } from '@angular/forms';
import { SelectionModel } from '@angular/cdk/collections';
import { DriverWalletService } from '../../../core/services/driver-wallet.service';
import { DriverService } from '../../../core/services/driver.service';
import { DriverCashOutRequest, DriverWallet, DriverWalletPolicy } from '../../../core/models/driver-wallet.model';
import { DriverListItem } from '../../../core/models/driver.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  standalone: false,
  selector: 'app-driver-cash-out-admin',
  templateUrl: './driver-cash-out-admin.component.html',
  styleUrls: ['./driver-cash-out-admin.component.scss']
})
export class DriverCashOutAdminComponent implements OnInit {
  filterForm: FormGroup;
  wallets: DriverWallet[] = [];
  requests: DriverCashOutRequest[] = [];
  drivers: DriverListItem[] = [];
  policy: DriverWalletPolicy | null = null;
  loading = true;
  processing = false;
  selection = new SelectionModel<number>(true, []);

  statusOptions = [
    { value: 'Pending', label: 'Pending' },
    { value: 'Paid', label: 'Paid' },
    { value: 'Rejected', label: 'Rejected' },
    { value: '', label: 'All' }
  ];

  constructor(
    private fb: FormBuilder,
    private walletService: DriverWalletService,
    private driverService: DriverService,
    private toast: UiToastService,
    private auth: AuthService
  ) {
    this.filterForm = this.fb.group({ driverId: [null], status: ['Pending'] });
  }

  get canManage(): boolean {
    return this.auth.hasPermission('DriverWallet.Manage');
  }

  ngOnInit(): void {
    this.walletService.getPolicy().subscribe({ next: p => { this.policy = p; }, error: () => undefined });
    this.driverService.getAll(1, 200).subscribe({ next: r => { this.drivers = r.items; }, error: () => undefined });
    this.load();
  }

  load(): void {
    this.loading = true;
    const v = this.filterForm.value;
    this.walletService.getAdminWallets(1, 50, v.driverId).subscribe({
      next: r => { this.wallets = r.items; },
      error: () => undefined
    });
    this.walletService.getAdminCashOutRequests(1, 100, v.driverId, v.status || null).subscribe({
      next: r => {
        this.requests = r.items;
        this.selection.clear();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.toast.error('Failed to load cash-out queue.');
      }
    });
  }

  toggleRow(id: number): void {
    this.selection.toggle(id);
  }

  isAllPendingSelected(): boolean {
    const pending = this.requests.filter(r => r.status === 'Pending');
    return pending.length > 0 && pending.every(r => this.selection.isSelected(r.id));
  }

  masterToggle(): void {
    const pendingIds = this.requests.filter(r => r.status === 'Pending').map(r => r.id);
    if (this.isAllPendingSelected()) {
      pendingIds.forEach(id => this.selection.deselect(id));
    } else {
      pendingIds.forEach(id => this.selection.select(id));
    }
  }

  bulkApprove(): void {
    if (!this.canManage || this.processing) return;
    const ids = this.selection.selected;
    if (!ids.length) {
      this.toast.warning('Select pending requests to approve.');
      return;
    }
    this.processing = true;
    this.walletService.bulkApprove(ids).subscribe({
      next: count => {
        this.processing = false;
        this.toast.success(`${count} payout(s) processed.`);
        this.load();
      },
      error: () => {
        this.processing = false;
        this.toast.error('Bulk approve failed.');
      }
    });
  }

  reject(row: DriverCashOutRequest): void {
    if (!this.canManage) return;
    const reason = window.prompt('Rejection reason?');
    if (!reason?.trim()) return;
    this.walletService.reject(row.id, reason.trim()).subscribe({
      next: () => {
        this.toast.success('Request rejected.');
        this.load();
      },
      error: () => this.toast.error('Reject failed.')
    });
  }

  formatMoney(n: number): string {
    return `${n.toLocaleString(undefined, { minimumFractionDigits: 2 })} PKR`;
  }
}
