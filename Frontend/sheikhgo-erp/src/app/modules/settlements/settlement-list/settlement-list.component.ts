import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatTableDataSource } from '@angular/material/table';
import { Router } from '@angular/router';
import { SettlementService } from '../../../core/services/settlement.service';
import { DriverService } from '../../../core/services/driver.service';
import { Settlement } from '../../../core/models/settlement.model';
import { DriverListItem } from '../../../core/models/driver.model';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  standalone: false,
  selector: 'app-settlement-list',
  templateUrl: './settlement-list.component.html',
  styleUrls: ['./settlement-list.component.scss']
})
export class SettlementListComponent implements OnInit {
  displayedColumns = ['driverName', 'period', 'grossCollected', 'fuelDeducted', 'payable', 'status', 'actions'];
  dataSource = new MatTableDataSource<Settlement>();
  drivers: DriverListItem[] = [];
  loading = true;
  error: string | null = null;
  generating = false;
  filterForm: FormGroup;
  draftForm: FormGroup;

  statusOptions = [
    { value: '', label: 'All' },
    { value: 'Draft', label: 'Draft' },
    { value: 'Approved', label: 'Approved' },
    { value: 'Paid', label: 'Paid' }
  ];

  statusColors: Record<string, string> = {
    Draft: '#757575',
    Approved: '#1565c0',
    Paid: '#2e7d32'
  };

  constructor(
    private fb: FormBuilder,
    private settlementService: SettlementService,
    private driverService: DriverService,
    private router: Router,
    private toast: UiToastService,
    private auth: AuthService
  ) {
    this.filterForm = this.fb.group({ driverId: [null], status: [''] });
    this.draftForm = this.fb.group({
      driverId: [null, Validators.required],
      periodStart: ['', Validators.required],
      periodEnd: ['', Validators.required],
      adjustments: [0]
    });
  }

  get canManage(): boolean {
    return this.auth.hasPermission('Settlement.Manage');
  }

  ngOnInit(): void {
    this.driverService.getAll(1, 200).subscribe({
      next: r => { this.drivers = r.items; },
      error: () => undefined
    });
    this.load();
  }

  load(page = 1): void {
    this.loading = true;
    this.error = null;
    const v = this.filterForm.value;
    this.settlementService.getAll(page, 20, {
      driverId: v.driverId || null,
      status: v.status || null
    }).subscribe({
      next: r => { this.dataSource.data = r.items; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Failed to load settlements.'; }
    });
  }

  applyFilter(): void { this.load(1); }

  open(id: number): void { void this.router.navigate(['/settlements', id]); }

  generate(): void {
    if (this.draftForm.invalid || this.generating) return;
    this.generating = true;
    const v = this.draftForm.value;
    this.settlementService.generateDraft({
      driverId: +v.driverId,
      periodStart: v.periodStart,
      periodEnd: v.periodEnd,
      adjustments: +v.adjustments || 0
    }).subscribe({
      next: id => {
        this.generating = false;
        this.toast.success('Draft settlement created');
        void this.router.navigate(['/settlements', id]);
      },
      error: () => { this.generating = false; this.toast.error('Could not generate settlement'); }
    });
  }
}
