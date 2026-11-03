import { Component, OnInit } from '@angular/core';
import { AbstractControl, FormBuilder, FormGroup, ValidationErrors, Validators } from '@angular/forms';
import { MatTableDataSource } from '@angular/material/table';
import { Router } from '@angular/router';
import { RentalService } from '../../../core/services/rental.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { CustomerService } from '../../../core/services/customer.service';
import { RentalAvailability, RentalBookingListItem } from '../../../core/models/rental.model';
import { AuthService } from '../../../core/services/auth.service';
import { UiToastService } from '../../../shared/components/ui/toast/ui-toast.service';

@Component({
  standalone: false,
  selector: 'app-rental-list',
  templateUrl: './rental-list.component.html',
  styleUrls: ['./rental-list.component.scss']
})
export class RentalListComponent implements OnInit {
  displayedColumns = ['rentalNumber', 'customerName', 'vehicleName', 'rateType', 'window', 'totalAmount', 'status', 'paymentStatus', 'actions'];
  dataSource = new MatTableDataSource<RentalBookingListItem>();
  loading = true;
  error: string | null = null;
  creating = false;
  checkingAvailability = false;
  createSubmitted = false;
  filterForm: FormGroup;
  createForm: FormGroup;
  vehicles: Array<{ id: number; name?: string; registrationNumber?: string }> = [];
  customers: Array<{ id: number; fullName?: string }> = [];
  quoteText: string | null = null;
  availabilityResult: RentalAvailability | null = null;

  statusOptions = [
    { value: '', label: 'All statuses' },
    { value: 'Draft', label: 'Draft' },
    { value: 'Confirmed', label: 'Confirmed' },
    { value: 'PickedUp', label: 'Picked up' },
    { value: 'Returned', label: 'Returned' },
    { value: 'Closed', label: 'Closed' },
    { value: 'Cancelled', label: 'Cancelled' }
  ];

  rateTypes = ['Hourly', 'Daily', 'Weekly', 'Monthly'];

  constructor(
    private fb: FormBuilder,
    private rentalService: RentalService,
    private vehicleService: VehicleService,
    private customerService: CustomerService,
    private router: Router,
    private toast: UiToastService,
    public auth: AuthService
  ) {
    this.filterForm = this.fb.group({ status: [''], vehicleId: [null] });
    this.createForm = this.fb.group(
      {
        customerId: [null, Validators.required],
        vehicleId: [null, Validators.required],
        rateType: ['Daily', Validators.required],
        startDate: ['', Validators.required],
        startTime: ['', Validators.required],
        endDate: ['', Validators.required],
        endTime: ['', Validators.required],
        pickupLocation: [''],
        returnLocation: [''],
        discountAmount: [0],
        notes: ['']
      },
      { validators: (group) => this.rentalPeriodValidator(group) }
    );
  }

  get canManage(): boolean {
    return this.auth.hasPermission('Rental.Manage');
  }

  get filtersActive(): boolean {
    const v = this.filterForm.value;
    return !!(v.status || v.vehicleId);
  }

  ngOnInit(): void {
    this.vehicleService.getAll(1, 200).subscribe({
      next: r => { this.vehicles = r.items as any[]; },
      error: () => undefined
    });
    this.customerService.getAll(1, 200).subscribe({
      next: r => { this.customers = r.items as any[]; },
      error: () => undefined
    });
    this.load();
  }

  load(page = 1): void {
    this.loading = true;
    this.error = null;
    const v = this.filterForm.value;
    this.rentalService.getBookings(page, 20, {
      status: v.status || null,
      vehicleId: v.vehicleId || null
    }).subscribe({
      next: r => { this.dataSource.data = r.items; this.loading = false; },
      error: () => { this.loading = false; this.error = 'Unable to load rentals'; }
    });
  }

  retryLoad(): void { this.load(1); }

  applyFilter(): void { this.load(1); }

  clearFilters(): void {
    this.filterForm.reset({ status: '', vehicleId: null });
    this.load(1);
  }

  open(id: number): void { void this.router.navigate(['/rental', id]); }

  trackById(_index: number, row: RentalBookingListItem): number {
    return row.id;
  }

  focusCreateForm(): void {
    const el = document.getElementById('createRentalSection');
    el?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    if (!this.canManage) return;
    const first = el?.querySelector<HTMLElement>('select.stb-select, input.stb-input');
    first?.focus();
  }

  refreshQuote(): void {
    const v = this.createForm.value;
    const start = this.combineDateTime(v.startDate, v.startTime);
    const end = this.combineDateTime(v.endDate, v.endTime);
    if (!v.vehicleId || !start || !end || !v.rateType) {
      this.quoteText = null;
      return;
    }
    if (new Date(end) <= new Date(start)) {
      this.quoteText = null;
      return;
    }
    this.rentalService.quote(+v.vehicleId, v.rateType, start, end, +v.discountAmount || 0).subscribe({
      next: q => {
        this.quoteText = `${q.billableUnits} × ${q.rateAmount} = ${q.baseCharge} − discount ${q.discountAmount} → ${q.estimatedTotal} ${q.currency} (deposit ${q.depositAmount})`;
      },
      error: () => { this.quoteText = 'No rate found for quotation.'; }
    });
  }

  onCreateFieldChange(): void {
    this.availabilityResult = null;
    this.refreshQuote();
  }

  checkAvailability(): void {
    this.createSubmitted = true;
    const v = this.createForm.value;
    const start = this.combineDateTime(v.startDate, v.startTime);
    const end = this.combineDateTime(v.endDate, v.endTime);
    if (!v.vehicleId || !start || !end) {
      this.toast.error('Select vehicle and rental period to check availability.');
      return;
    }
    if (new Date(end) <= new Date(start)) {
      this.toast.error('End date and time must be after start.');
      return;
    }
    this.checkingAvailability = true;
    this.availabilityResult = null;
    this.rentalService.checkAvailability(+v.vehicleId, start, end).subscribe({
      next: r => {
        this.availabilityResult = r;
        this.checkingAvailability = false;
        if (r.isAvailable) {
          this.toast.success('Vehicle is available for the selected period.');
        } else {
          this.toast.error('Vehicle is not available for the selected period.');
        }
      },
      error: e => {
        this.checkingAvailability = false;
        this.toast.error(e?.error?.message || 'Availability check failed');
      }
    });
  }

  create(): void {
    this.createSubmitted = true;
    if (this.createForm.invalid || this.creating) return;
    this.creating = true;
    const v = this.createForm.value;
    const start = this.combineDateTime(v.startDate, v.startTime);
    const end = this.combineDateTime(v.endDate, v.endTime);
    if (!start || !end) {
      this.creating = false;
      return;
    }
    this.rentalService.createBooking({
      customerId: +v.customerId,
      vehicleId: +v.vehicleId,
      rateType: v.rateType,
      startAt: start,
      endAt: end,
      pickupLocation: v.pickupLocation || null,
      returnLocation: v.returnLocation || null,
      discountAmount: +v.discountAmount || 0,
      notes: v.notes || null
    }).subscribe({
      next: id => {
        this.creating = false;
        this.toast.success('Rental booking created');
        void this.router.navigate(['/rental', id]);
      },
      error: err => {
        this.creating = false;
        this.toast.error(err?.error?.message || 'Could not create rental');
      }
    });
  }

  showFieldError(field: string): boolean {
    const control = this.createForm.get(field);
    if (!control) return false;
    if (control.invalid && (control.touched || this.createSubmitted)) return true;
    if ((field === 'endDate' || field === 'endTime') && this.createForm.hasError('dateRange') && this.createSubmitted) {
      return true;
    }
    return false;
  }

  fieldErrorMessage(field: string): string {
    const control = this.createForm.get(field);
    if (!control) return '';
    if (control.hasError('required')) {
      return 'This field is required.';
    }
    if ((field === 'endDate' || field === 'endTime') && this.createForm.hasError('dateRange')) {
      return 'End must be after start.';
    }
    return '';
  }

  statusClass(status: string): string {
    const key = (status || '').toLowerCase();
    const map: Record<string, string> = {
      draft: 'ops-status--draft',
      confirmed: 'ops-status--confirmed',
      pickedup: 'ops-status--assigned',
      returned: 'ops-status--delayed',
      closed: 'ops-status--green',
      cancelled: 'ops-status--cancelled'
    };
    return map[key] ?? 'ops-status--draft';
  }

  statusLabel(status: string): string {
    if (status === 'PickedUp') return 'Picked up';
    return status;
  }

  paymentStatusClass(status: string): string {
    const key = (status || '').toLowerCase();
    if (key === 'paid') return 'ops-status--green';
    if (key === 'partial') return 'ops-status--pending';
    if (key === 'unpaid') return 'ops-status--red';
    return 'ops-status--draft';
  }

  private combineDateTime(date: string, time: string): string | null {
    if (!date || !time) return null;
    const parsed = new Date(`${date}T${time}`);
    if (Number.isNaN(parsed.getTime())) return null;
    return parsed.toISOString();
  }

  private rentalPeriodValidator(group: AbstractControl): ValidationErrors | null {
    const startDate = group.get('startDate')?.value as string;
    const startTime = group.get('startTime')?.value as string;
    const endDate = group.get('endDate')?.value as string;
    const endTime = group.get('endTime')?.value as string;
    const start = startDate && startTime ? new Date(`${startDate}T${startTime}`) : null;
    const end = endDate && endTime ? new Date(`${endDate}T${endTime}`) : null;
    if (start && end && !Number.isNaN(start.getTime()) && !Number.isNaN(end.getTime()) && end <= start) {
      return { dateRange: true };
    }
    return null;
  }
}
