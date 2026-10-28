import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatTableDataSource } from '@angular/material/table';
import { Router } from '@angular/router';
import { RentalService } from '../../../core/services/rental.service';
import { VehicleService } from '../../../core/services/vehicle.service';
import { CustomerService } from '../../../core/services/customer.service';
import { RentalBookingListItem } from '../../../core/models/rental.model';
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
  filterForm: FormGroup;
  createForm: FormGroup;
  vehicles: Array<{ id: number; name?: string; registrationNumber?: string }> = [];
  customers: Array<{ id: number; fullName?: string }> = [];
  quoteText: string | null = null;

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

  statusColors: Record<string, string> = {
    Draft: '#757575',
    Confirmed: '#1565c0',
    PickedUp: '#6a1b9a',
    Returned: '#ef6c00',
    Closed: '#2e7d32',
    Cancelled: '#c62828'
  };

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
    this.createForm = this.fb.group({
      customerId: [null, Validators.required],
      vehicleId: [null, Validators.required],
      rateType: ['Daily', Validators.required],
      startAt: ['', Validators.required],
      endAt: ['', Validators.required],
      pickupLocation: [''],
      returnLocation: [''],
      discountAmount: [0],
      notes: ['']
    });
  }

  get canManage(): boolean {
    return this.auth.hasPermission('Rental.Manage');
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
      error: () => { this.loading = false; this.error = 'Failed to load rentals.'; }
    });
  }

  applyFilter(): void { this.load(1); }

  open(id: number): void { void this.router.navigate(['/rental', id]); }

  refreshQuote(): void {
    const v = this.createForm.value;
    if (!v.vehicleId || !v.startAt || !v.endAt || !v.rateType) {
      this.quoteText = null;
      return;
    }
    const start = new Date(v.startAt).toISOString();
    const end = new Date(v.endAt).toISOString();
    this.rentalService.quote(+v.vehicleId, v.rateType, start, end, +v.discountAmount || 0).subscribe({
      next: q => {
        this.quoteText = `${q.billableUnits} × ${q.rateAmount} = ${q.baseCharge} − discount ${q.discountAmount} → ${q.estimatedTotal} ${q.currency} (deposit ${q.depositAmount})`;
      },
      error: () => { this.quoteText = 'No rate found for quotation.'; }
    });
  }

  create(): void {
    if (this.createForm.invalid || this.creating) return;
    this.creating = true;
    const v = this.createForm.value;
    this.rentalService.createBooking({
      customerId: +v.customerId,
      vehicleId: +v.vehicleId,
      rateType: v.rateType,
      startAt: new Date(v.startAt).toISOString(),
      endAt: new Date(v.endAt).toISOString(),
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
}
