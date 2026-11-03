import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatTableDataSource } from '@angular/material/table';
import { RentalService } from '../../../core/services/rental.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { RentalRate } from '../../../core/models/rental.model';
import { AuthService } from '../../../core/services/auth.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-rental-rates',
  templateUrl: './rental-rates.component.html',
  styleUrls: ['./rental-rates.component.scss']
})
export class RentalRatesComponent implements OnInit {
  displayedColumns = ['vehicle', 'rateType', 'amount', 'depositAmount', 'currency', 'isActive', 'actions'];
  dataSource = new MatTableDataSource<RentalRate>();
  loading = true;
  saving = false;
  error: string | null = null;
  form: FormGroup;
  vehicles: Array<{ id: number; name?: string; registrationNumber?: string }> = [];
  rateTypes = ['Hourly', 'Daily', 'Weekly', 'Monthly'];

  constructor(
    private fb: FormBuilder,
    private rentalService: RentalService,
    private vehicleService: VehicleService,
    private toast: UiToastService,
    public auth: AuthService
  ) {
    this.form = this.fb.group({
      vehicleId: [null],
      rateType: ['Daily', Validators.required],
      amount: [0, [Validators.required, Validators.min(0)]],
      depositAmount: [0, Validators.min(0)],
      currency: ['PKR'],
      notes: ['']
    });
  }

  get canManageRates(): boolean {
    return this.auth.hasPermission('Rental.Rates') || this.auth.hasPermission('Rental.Manage');
  }

  ngOnInit(): void {
    this.vehicleService.getAll(1, 200).subscribe({
      next: r => { this.vehicles = r.items as any[]; },
      error: () => undefined
    });
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.rentalService.getRates().subscribe({
      next: r => { this.dataSource.data = r.items; this.loading = false; },
      error: () => {
        this.loading = false;
        this.error = 'Unable to load rental rates';
        this.toast.error('Failed to load rates');
      }
    });
  }

  retryLoad(): void { this.load(); }

  trackById(_index: number, row: RentalRate): number {
    return row.id;
  }

  activeStatusClass(isActive: boolean): string {
    return isActive ? 'ops-status--green' : 'ops-status--cancelled';
  }

  create(): void {
    if (this.form.invalid || this.saving) return;
    this.saving = true;
    const v = this.form.value;
    this.rentalService.createRate({
      vehicleId: v.vehicleId || null,
      rateType: v.rateType,
      amount: +v.amount,
      depositAmount: +v.depositAmount || 0,
      currency: v.currency || 'PKR',
      notes: v.notes || null
    }).subscribe({
      next: () => {
        this.saving = false;
        this.toast.success('Rate created');
        this.form.patchValue({ amount: 0, depositAmount: 0, notes: '' });
        this.load();
      },
      error: e => {
        this.saving = false;
        this.toast.error(e?.error?.message || 'Create failed');
      }
    });
  }

  deactivate(row: RentalRate): void {
    this.rentalService.updateRate(row.id, {
      vehicleId: row.vehicleId ?? null,
      rateType: row.rateType,
      amount: row.amount,
      depositAmount: row.depositAmount,
      currency: row.currency,
      isActive: false,
      notes: row.notes
    }).subscribe({
      next: () => { this.toast.success('Rate deactivated'); this.load(); },
      error: () => this.toast.error('Update failed')
    });
  }

  remove(row: RentalRate): void {
    this.rentalService.deleteRate(row.id).subscribe({
      next: () => { this.toast.success('Rate deleted'); this.load(); },
      error: () => this.toast.error('Delete failed')
    });
  }
}
